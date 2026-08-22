using System.Globalization;
using StVrainToICSFunctionApp.Models;

namespace StVrainToICSFunctionApp.Services;

public interface ILandingDayService
{
    Task<LandingDayResponse> BuildAsync(LandingDayRequest request, CancellationToken cancellationToken = default);
}

public sealed class LandingDayService : ILandingDayService
{
    private static readonly TimeZoneInfo DenverTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("America/Denver");

    private readonly IMenuCacheService _menuCache;
    private readonly IConfiguration _configuration;

    public LandingDayService(IMenuCacheService menuCache, IConfiguration configuration)
    {
        _menuCache = menuCache;
        _configuration = configuration;
    }

    public async Task<LandingDayResponse> BuildAsync(
        LandingDayRequest request,
        CancellationToken cancellationToken = default)
    {
        DateTime nowDenver = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, DenverTimeZone);
        DateOnly today = DateOnly.FromDateTime(nowDenver);
        DateOnly? requestedDate = null;
        if (!string.IsNullOrWhiteSpace(request.Date)
            && DateOnly.TryParse(request.Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly parsed))
        {
            requestedDate = parsed;
        }

        double startOffset = _configuration.GetValue<double?>("DefaultStartOffset") ?? -7.0;
        double endOffset = _configuration.GetValue<double?>("DefaultEndOffset") ?? 30.0;
        DateTime startDate = nowDenver.AddDays(startOffset);
        DateTime endDate = nowDenver.AddDays(endOffset);

        List<(LandingSchoolRequest School, Menu Menu)> loaded = [];
        SortedSet<DateOnly> availableDates = [];

        foreach (LandingSchoolRequest school in request.Schools ?? [])
        {
            if (string.IsNullOrWhiteSpace(school.BuildingId) || string.IsNullOrWhiteSpace(school.DistrictId))
            {
                continue;
            }

            Menu menu = await _menuCache
                .GetMenuAsync(school.BuildingId, school.DistrictId, startDate, endDate, cancellationToken)
                .ConfigureAwait(false);
            loaded.Add((school, menu));
            foreach (DateOnly date in MenuDayExtractor.ExtractAvailableDates(menu))
            {
                availableDates.Add(date);
            }
        }

        IReadOnlyList<DateOnly> dates = availableDates.ToList();
        DateOnly selected = TodayMenuPageService.ResolveSelectedDate(dates, today, requestedDate);
        DateOnly? upcoming = TodayMenuPageService.NextLunchDate(dates, selected);
        DateOnly? previous = TodayMenuPageService.PreviousLunchDate(dates, selected);
        bool skipped = requestedDate is null
            && selected > today
            && !dates.Contains(today);

        return new LandingDayResponse
        {
            Today = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Primary = BuildDay(loaded, selected, today, isUpcoming: false),
            Upcoming = upcoming is DateOnly next
                ? BuildDay(loaded, next, today, isUpcoming: true)
                : null,
            PreviousDate = previous?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            NextDate = upcoming?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            IsShowingNextAvailable = skipped,
        };
    }

    private static LandingDayDto BuildDay(
        List<(LandingSchoolRequest School, Menu Menu)> loaded,
        DateOnly date,
        DateOnly today,
        bool isUpcoming)
    {
        List<LandingSchoolDayDto> schools = [];
        foreach ((LandingSchoolRequest school, Menu menu) in loaded)
        {
            IReadOnlyList<MenuDayExtractor.DayMealGroup> meals =
                MenuDayExtractor.ExtractForDate(menu, date);

            schools.Add(new LandingSchoolDayDto
            {
                BuildingId = school.BuildingId,
                DistrictId = school.DistrictId,
                Name = string.IsNullOrWhiteSpace(school.Name) ? school.BuildingId : school.Name!,
                Meals = meals.Select(m => new LandingMealDto
                {
                    ServingSession = m.ServingSession,
                    PlanName = m.PlanName,
                    Title = m.Title,
                    Items = m.Items.ToList(),
                }).ToList(),
            });
        }

        return new LandingDayDto
        {
            Date = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateLabel = date.ToString("dddd, MMMM d, yyyy", CultureInfo.InvariantCulture),
            RelativeLabel = TodayMenuPageService.RelativeLabel(date, today, isUpcoming)
                .Replace("lunch", "menu", StringComparison.OrdinalIgnoreCase),
            Schools = schools,
        };
    }
}
