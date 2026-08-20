namespace StVrainToICSFunctionApp.Options;

public sealed class SchoolShortcut
{
    public string BuildingId { get; set; } = string.Empty;

    public string DistrictId { get; set; } = string.Empty;

    /// <summary>Absolute HHmm display time, e.g. 1130 or 1200.</summary>
    public int DefaultDisplayTime { get; set; }
}
