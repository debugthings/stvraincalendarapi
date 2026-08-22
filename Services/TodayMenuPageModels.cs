namespace StVrainToICSFunctionApp.Services;

public sealed record SchoolTodayMenu(
    string SchoolCode,
    string DisplayName,
    string LunchTime,
    string CalendarUrl,
    IReadOnlyList<MenuLunchExtractor.LunchMealGroup> Meals);

public sealed record LunchDayView(
    DateOnly Date,
    string DateLabel,
    string RelativeLabel,
    IReadOnlyList<SchoolTodayMenu> Schools);

public sealed record TodayMenuPageModel(
    DateOnly Today,
    LunchDayView Primary,
    LunchDayView? Upcoming,
    string? PreviousDateUrl,
    string? NextDateUrl,
    bool IsShowingNextAvailable);
