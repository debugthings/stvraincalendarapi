namespace StVrainToICSFunctionApp.Data;

public sealed class FastLinkEntry
{
    public required string Slug { get; set; }

    public required string BuildingId { get; set; }

    public required string DistrictId { get; set; }

    public string SchoolName { get; set; } = string.Empty;

    /// <summary>Session name, e.g. Lunch.</summary>
    public string Session { get; set; } = "Lunch";

    public int DisplayTimeHhmm { get; set; }

    /// <summary>JSON list of { planName, mealNames }.</summary>
    public required string IncludedPlansJson { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
