using System.Globalization;

namespace StVrainToICSFunctionApp.Services;

public sealed class TodayMenuPageService
{
    private static readonly TimeZoneInfo DenverTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("America/Denver");

    private readonly IMenuCacheService _menuCache;
    private readonly SchoolShortcutCatalog _schools;
    private readonly IConfiguration _configuration;

    public TodayMenuPageService(
        IMenuCacheService menuCache,
        SchoolShortcutCatalog schools,
        IConfiguration configuration)
    {
        _menuCache = menuCache;
        _schools = schools;
        _configuration = configuration;
    }

    public async Task<TodayMenuPageModel> BuildAsync(
        DateOnly? requestedDate = null,
        CancellationToken cancellationToken = default)
    {
        DateTime nowDenver = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, DenverTimeZone);
        DateOnly today = DateOnly.FromDateTime(nowDenver);
        double startOffset = _configuration.GetValue<double?>("DefaultStartOffset") ?? -7.0;
        double endOffset = _configuration.GetValue<double?>("DefaultEndOffset") ?? 30.0;
        DateTime startDate = nowDenver.AddDays(startOffset);
        DateTime endDate = nowDenver.AddDays(endOffset);

        List<(string Code, Options.SchoolShortcut Shortcut, Models.Menu Menu)> schoolMenus = [];
        SortedSet<DateOnly> lunchDates = [];

        foreach (KeyValuePair<string, Options.SchoolShortcut> pair in _schools.All())
        {
            Models.Menu menu = await _menuCache
                .GetMenuAsync(pair.Value.BuildingId, pair.Value.DistrictId, startDate, endDate, cancellationToken)
                .ConfigureAwait(false);

            schoolMenus.Add((pair.Key, pair.Value, menu));
            foreach (DateOnly date in MenuLunchExtractor.ExtractAvailableDates(menu))
            {
                lunchDates.Add(date);
            }
        }

        IReadOnlyList<DateOnly> availableDates = lunchDates.ToList();
        DateOnly selected = ResolveSelectedDate(availableDates, today, requestedDate);
        DateOnly? upcoming = NextLunchDate(availableDates, selected);
        DateOnly? previous = PreviousLunchDate(availableDates, selected);
        bool skippedToNext = requestedDate is null
            && selected > today
            && !availableDates.Contains(today);

        LunchDayView primary = BuildDayView(schoolMenus, selected, today, isUpcoming: false);
        LunchDayView? upcomingView = upcoming is DateOnly next
            ? BuildDayView(schoolMenus, next, today, isUpcoming: true)
            : null;

        return new TodayMenuPageModel(
            today,
            primary,
            upcomingView,
            previous is DateOnly prev ? $"/?date={prev:yyyy-MM-dd}" : null,
            upcoming is DateOnly nextUrl ? $"/?date={nextUrl:yyyy-MM-dd}" : null,
            skippedToNext);
    }

    internal static DateOnly ResolveSelectedDate(
        IReadOnlyList<DateOnly> availableDates,
        DateOnly today,
        DateOnly? requestedDate)
    {
        if (availableDates.Count == 0)
        {
            return requestedDate ?? today;
        }

        if (requestedDate is DateOnly requested)
        {
            if (availableDates.Contains(requested))
            {
                return requested;
            }

            foreach (DateOnly date in availableDates)
            {
                if (date >= requested)
                {
                    return date;
                }
            }

            return availableDates[^1];
        }

        foreach (DateOnly date in availableDates)
        {
            if (date >= today)
            {
                return date;
            }
        }

        return availableDates[^1];
    }

    internal static DateOnly? NextLunchDate(IReadOnlyList<DateOnly> availableDates, DateOnly selected)
    {
        for (int i = 0; i < availableDates.Count; i++)
        {
            if (availableDates[i] == selected && i + 1 < availableDates.Count)
            {
                return availableDates[i + 1];
            }
        }

        return null;
    }

    internal static DateOnly? PreviousLunchDate(IReadOnlyList<DateOnly> availableDates, DateOnly selected)
    {
        for (int i = 0; i < availableDates.Count; i++)
        {
            if (availableDates[i] == selected && i > 0)
            {
                return availableDates[i - 1];
            }
        }

        return null;
    }

    private static LunchDayView BuildDayView(
        List<(string Code, Options.SchoolShortcut Shortcut, Models.Menu Menu)> schoolMenus,
        DateOnly date,
        DateOnly today,
        bool isUpcoming)
    {
        List<SchoolTodayMenu> schools = [];
        foreach ((string code, Options.SchoolShortcut shortcut, Models.Menu menu) in schoolMenus)
        {
            string displayName = string.IsNullOrWhiteSpace(shortcut.DisplayName)
                ? code.ToUpperInvariant()
                : shortcut.DisplayName;

            schools.Add(new SchoolTodayMenu(
                code,
                displayName,
                FormatDisplayTime(shortcut.DefaultDisplayTime),
                $"/subscribe?school={code}",
                MenuLunchExtractor.ExtractForDate(menu, date)));
        }

        return new LunchDayView(
            date,
            date.ToString("dddd, MMMM d, yyyy", CultureInfo.InvariantCulture),
            RelativeLabel(date, today, isUpcoming),
            schools);
    }

    internal static string RelativeLabel(DateOnly date, DateOnly today, bool isUpcoming)
    {
        if (isUpcoming)
        {
            return "Next lunch";
        }

        if (date == today)
        {
            return "Today's lunch";
        }

        if (date > today)
        {
            return "Upcoming lunch";
        }

        return "Recent lunch";
    }

    internal static string FormatDisplayTime(int hhmm)
    {
        int hours = hhmm / 100;
        int minutes = hhmm % 100;
        if (hours is < 0 or > 23 || minutes is < 0 or > 59)
        {
            return hhmm.ToString(CultureInfo.InvariantCulture);
        }

        DateTime clock = new(2000, 1, 1, hours, minutes, 0);
        return clock.ToString("h:mm tt", CultureInfo.InvariantCulture);
    }
}
