namespace StVrainToICSFunctionApp.Models;

public sealed class FamilyMenuIdentifierResponse
{
    public string? DistrictId { get; set; }

    public string? DistrictName { get; set; }

    public FamilyMenuBuilding[]? Buildings { get; set; }
}

public sealed class FamilyMenuBuilding
{
    public string? BuildingId { get; set; }

    public string? Name { get; set; }
}

public sealed class SubscribeSchoolDto
{
    public string BuildingId { get; set; } = string.Empty;

    public string DistrictId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Shortcut { get; set; }
}

public sealed class MenuPlanSectionDto
{
    public string PlanName { get; set; } = string.Empty;

    public List<string> MealNames { get; set; } = [];
}

public sealed class MenuSessionSectionDto
{
    public string ServingSession { get; set; } = string.Empty;

    public int DefaultDisplayTimeHhmm { get; set; }

    public List<MenuPlanSectionDto> Plans { get; set; } = [];
}

public sealed class CreateFastLinkRequest
{
    public string BuildingId { get; set; } = string.Empty;

    public string DistrictId { get; set; } = string.Empty;

    public string SchoolName { get; set; } = string.Empty;

    /// <summary>Fallback lunch display time when a plan omits DisplayTimeHhmm.</summary>
    public int DisplayTimeHhmm { get; set; }

    public List<IncludedPlanFilter> Plans { get; set; } = [];
}

public sealed class CreateFastLinkResponse
{
    public string Slug { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string IcsUrl { get; set; } = string.Empty;
}
