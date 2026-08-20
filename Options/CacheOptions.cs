namespace StVrainToICSFunctionApp.Options;

public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    public bool Enabled { get; set; } = true;

    public int TtlMinutes { get; set; } = 360;

    public string DatabasePath { get; set; } = "data/menu-cache.db";
}
