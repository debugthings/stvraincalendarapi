namespace StVrainToICSFunctionApp.Options;

public sealed class SchoolShortcut
{
    public string DisplayName { get; set; } = string.Empty;

    public string BuildingId { get; set; } = string.Empty;

    public string DistrictId { get; set; } = string.Empty;

    /// <summary>Absolute HHmm lunch display time, e.g. 1130 or 1200.</summary>
    public int DefaultDisplayTime { get; set; }

    /// <summary>Absolute HHmm breakfast display time, e.g. 830.</summary>
    public int DefaultBreakfastDisplayTime { get; set; } = 830;
}
