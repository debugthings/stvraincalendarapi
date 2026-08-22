using StVrainToICSFunctionApp.Formatters;
using StVrainToICSFunctionApp.Models;

namespace StVrainToICSFunctionApp.Services;

public interface ISubscribeService
{
    Task<IReadOnlyList<SubscribeSchoolDto>> GetSchoolsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MenuSessionSectionDto>> GetSectionsAsync(
        string buildingId,
        string districtId,
        CancellationToken cancellationToken = default);

    Task<CreateFastLinkResponse> CreateFastLinkAsync(
        CreateFastLinkRequest request,
        string absoluteBaseUrl,
        CancellationToken cancellationToken = default);
}

public sealed class SubscribeService : ISubscribeService
{
    private const int MaxSlugAttempts = 32;

    private readonly ISchoolDirectoryService _schools;
    private readonly IMenuCacheService _menuCache;
    private readonly IFastLinkStore _fastLinks;
    private readonly IFastLinkSlugGenerator _slugs;
    private readonly SchoolShortcutCatalog _shortcuts;
    private readonly IConfiguration _configuration;

    public SubscribeService(
        ISchoolDirectoryService schools,
        IMenuCacheService menuCache,
        IFastLinkStore fastLinks,
        IFastLinkSlugGenerator slugs,
        SchoolShortcutCatalog shortcuts,
        IConfiguration configuration)
    {
        _schools = schools;
        _menuCache = menuCache;
        _fastLinks = fastLinks;
        _slugs = slugs;
        _shortcuts = shortcuts;
        _configuration = configuration;
    }

    public Task<IReadOnlyList<SubscribeSchoolDto>> GetSchoolsAsync(CancellationToken cancellationToken = default) =>
        _schools.GetSchoolsAsync(cancellationToken);

    public async Task<IReadOnlyList<MenuSessionSectionDto>> GetSectionsAsync(
        string buildingId,
        string districtId,
        CancellationToken cancellationToken = default)
    {
        DateTime now = DateTime.Now;
        double startOffset = _configuration.GetValue<double?>("DefaultStartOffset") ?? -7.0;
        double endOffset = _configuration.GetValue<double?>("DefaultEndOffset") ?? 30.0;
        Menu menu = await _menuCache
            .GetMenuAsync(buildingId, districtId, now.AddDays(startOffset), now.AddDays(endOffset), cancellationToken)
            .ConfigureAwait(false);

        int lunchDefault = 1130;
        foreach (KeyValuePair<string, Options.SchoolShortcut> pair in _shortcuts.All())
        {
            if (string.Equals(pair.Value.BuildingId, buildingId, StringComparison.OrdinalIgnoreCase)
                && pair.Value.DefaultDisplayTime != 0)
            {
                lunchDefault = pair.Value.DefaultDisplayTime;
                break;
            }
        }

        return MenuSectionExtractor.ExtractSections(menu, lunchDefault);
    }

    public async Task<CreateFastLinkResponse> CreateFastLinkAsync(
        CreateFastLinkRequest request,
        string absoluteBaseUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.BuildingId) || string.IsNullOrWhiteSpace(request.DistrictId))
        {
            throw new ArgumentException("Building and district are required.");
        }

        int fallbackTime = request.DisplayTimeHhmm != 0
            && ICSTextOutputFormatter.TryGetClockTime(request.DisplayTimeHhmm, out _, out _)
                ? request.DisplayTimeHhmm
                : 1130;

        List<IncludedPlanFilter> plans = (request.Plans ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p.PlanName) && p.MealNames is { Count: > 0 })
            .Select(p =>
            {
                string session = string.IsNullOrWhiteSpace(p.ServingSession) ? "Lunch" : p.ServingSession.Trim();
                int time = p.DisplayTimeHhmm is int hhmm
                    && ICSTextOutputFormatter.TryGetClockTime(hhmm, out _, out _)
                        ? hhmm
                        : MenuSectionExtractor.DefaultDisplayTime(session, fallbackTime);

                return new IncludedPlanFilter
                {
                    ServingSession = session,
                    PlanName = p.PlanName.Trim(),
                    DisplayTimeHhmm = time,
                    MealNames = p.MealNames
                        .Where(m => !string.IsNullOrWhiteSpace(m))
                        .Select(m => m.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                };
            })
            .Where(p => p.MealNames.Count > 0)
            .ToList();

        if (plans.Count == 0)
        {
            throw new ArgumentException("Select at least one menu meal.");
        }

        string? slug = null;
        for (int attempt = 0; attempt < MaxSlugAttempts; attempt++)
        {
            string candidate = _slugs.Generate();
            if (!FastLinkSlugGenerator.IsValidSlug(candidate))
            {
                continue;
            }

            if (!await _fastLinks.ExistsAsync(candidate, cancellationToken).ConfigureAwait(false))
            {
                slug = candidate;
                break;
            }
        }

        if (slug is null)
        {
            throw new InvalidOperationException("Unable to allocate a unique fastlink slug.");
        }

        string primarySession = plans
            .Select(p => p.EffectiveServingSession)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() == 1
                ? plans[0].EffectiveServingSession
                : "Multi";

        await _fastLinks.CreateAsync(
            slug,
            request.BuildingId.Trim(),
            request.DistrictId.Trim(),
            request.SchoolName?.Trim() ?? string.Empty,
            primarySession,
            fallbackTime,
            plans,
            cancellationToken).ConfigureAwait(false);

        string baseUrl = absoluteBaseUrl.TrimEnd('/');
        return new CreateFastLinkResponse
        {
            Slug = slug,
            Url = $"{baseUrl}/{slug}",
            IcsUrl = $"{baseUrl}/{slug}.ics",
        };
    }
}
