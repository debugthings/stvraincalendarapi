namespace StVrainToICSFunctionApp.Data;

public sealed class MenuCacheEntry
{
    public required string CacheKey { get; set; }

    public required string MenuJson { get; set; }

    public DateTime FetchedAt { get; set; }

    public DateTime ExpiresAt { get; set; }
}
