namespace StVrainToICSFunctionApp.Models;

public sealed class LandingSchoolRequest
{
    public string BuildingId { get; set; } = string.Empty;

    public string DistrictId { get; set; } = string.Empty;

    public string? Name { get; set; }
}

public sealed class LandingDayRequest
{
    public string? Date { get; set; }

    public List<LandingSchoolRequest> Schools { get; set; } = [];
}

public sealed class LandingMealDto
{
    public string ServingSession { get; set; } = string.Empty;

    public string PlanName { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public List<string> Items { get; set; } = [];
}

public sealed class LandingSchoolDayDto
{
    public string BuildingId { get; set; } = string.Empty;

    public string DistrictId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public List<LandingMealDto> Meals { get; set; } = [];
}

public sealed class LandingDayDto
{
    public string Date { get; set; } = string.Empty;

    public string DateLabel { get; set; } = string.Empty;

    public string RelativeLabel { get; set; } = string.Empty;

    public List<LandingSchoolDayDto> Schools { get; set; } = [];
}

public sealed class LandingDayResponse
{
    public string Today { get; set; } = string.Empty;

    public LandingDayDto Primary { get; set; } = new();

    public LandingDayDto? Upcoming { get; set; }

    public string? PreviousDate { get; set; }

    public string? NextDate { get; set; }

    public bool IsShowingNextAvailable { get; set; }
}
