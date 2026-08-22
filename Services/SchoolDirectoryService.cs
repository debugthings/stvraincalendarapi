using Microsoft.Extensions.Caching.Memory;
using StVrainToICSFunctionApp.Models;

namespace StVrainToICSFunctionApp.Services;

public interface ISchoolDirectoryService
{
    Task<IReadOnlyList<SubscribeSchoolDto>> GetSchoolsAsync(CancellationToken cancellationToken = default);
}

public sealed class SchoolDirectoryService : ISchoolDirectoryService
{
    private const string CacheKey = "subscribe-schools";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);

    private readonly ILinqMenuClient _linq;
    private readonly IMemoryCache _cache;
    private readonly SchoolShortcutCatalog _shortcuts;
    private readonly IConfiguration _configuration;

    public SchoolDirectoryService(
        ILinqMenuClient linq,
        IMemoryCache cache,
        SchoolShortcutCatalog shortcuts,
        IConfiguration configuration)
    {
        _linq = linq;
        _cache = cache;
        _shortcuts = shortcuts;
        _configuration = configuration;
    }

    public async Task<IReadOnlyList<SubscribeSchoolDto>> GetSchoolsAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<SubscribeSchoolDto>? cached) && cached is not null)
        {
            return cached;
        }

        string identifier = _configuration["LinqDistrictMenuCode"]
            ?? Environment.GetEnvironmentVariable("LinqDistrictMenuCode")
            ?? "DCN3CB";

        FamilyMenuIdentifierResponse response = await _linq
            .GetFamilyMenuIdentifiersAsync(identifier, cancellationToken)
            .ConfigureAwait(false);

        string districtId = response.DistrictId ?? string.Empty;
        Dictionary<string, string> buildingToShortcut = [];
        foreach (KeyValuePair<string, Options.SchoolShortcut> pair in _shortcuts.All())
        {
            if (!string.IsNullOrWhiteSpace(pair.Value.BuildingId))
            {
                buildingToShortcut[pair.Value.BuildingId] = pair.Key;
            }
        }

        List<SubscribeSchoolDto> schools = (response.Buildings ?? [])
            .Where(b => !string.IsNullOrWhiteSpace(b.BuildingId) && !string.IsNullOrWhiteSpace(b.Name))
            .Select(b => new SubscribeSchoolDto
            {
                BuildingId = b.BuildingId!,
                DistrictId = districtId,
                Name = b.Name!,
                Shortcut = buildingToShortcut.TryGetValue(b.BuildingId!, out string? code) ? code : null,
            })
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _cache.Set(CacheKey, schools, CacheTtl);
        return schools;
    }
}
