using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StVrainToICSFunctionApp.Data;
using StVrainToICSFunctionApp.Models;
using StVrainToICSFunctionApp.Options;

namespace StVrainToICSFunctionApp.Services;

public sealed class MenuCacheService : IMenuCacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly MenuCacheDbContext _db;
    private readonly ILinqMenuClient _linq;
    private readonly IOptions<CacheOptions> _cacheOptions;
    private readonly ILogger<MenuCacheService> _logger;

    public MenuCacheService(
        MenuCacheDbContext db,
        ILinqMenuClient linq,
        IOptions<CacheOptions> cacheOptions,
        ILogger<MenuCacheService> logger)
    {
        _db = db;
        _linq = linq;
        _cacheOptions = cacheOptions;
        _logger = logger;
    }

    public static string BuildCacheKey(string buildingId, string districtId, DateTime startDate, DateTime endDate) =>
        $"{buildingId}|{districtId}|{startDate:M-dd-yyyy}|{endDate:M-dd-yyyy}";

    public async Task<Menu> GetMenuAsync(
        string buildingId,
        string districtId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        CacheOptions cache = _cacheOptions.Value;
        string key = BuildCacheKey(buildingId, districtId, startDate, endDate);
        DateTime now = DateTime.UtcNow;

        if (cache.Enabled)
        {
            MenuCacheEntry? existing = await _db.MenuCacheEntries
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.CacheKey == key, cancellationToken)
                .ConfigureAwait(false);

            if (existing is not null && existing.ExpiresAt > now)
            {
                _logger.LogInformation("Menu cache hit for {CacheKey} (expires {ExpiresAt:o})", key, existing.ExpiresAt);
                return Deserialize(existing.MenuJson);
            }

            try
            {
                Menu fresh = await _linq.GetFamilyMenuAsync(buildingId, districtId, startDate, endDate, cancellationToken)
                    .ConfigureAwait(false);
                await UpsertAsync(key, fresh, now, cache.TtlMinutes, cancellationToken).ConfigureAwait(false);
                return fresh;
            }
            catch (Exception ex) when (existing is not null)
            {
                _logger.LogWarning(ex, "LINQ fetch failed for {CacheKey}; serving stale cache from {FetchedAt:o}", key, existing.FetchedAt);
                return Deserialize(existing.MenuJson);
            }
        }

        return await _linq.GetFamilyMenuAsync(buildingId, districtId, startDate, endDate, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task UpsertAsync(string key, Menu menu, DateTime now, int ttlMinutes, CancellationToken cancellationToken)
    {
        int ttl = ttlMinutes <= 0 ? 360 : ttlMinutes;
        string json = JsonSerializer.Serialize(menu, JsonOptions);
        MenuCacheEntry? row = await _db.MenuCacheEntries
            .FirstOrDefaultAsync(e => e.CacheKey == key, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            _db.MenuCacheEntries.Add(new MenuCacheEntry
            {
                CacheKey = key,
                MenuJson = json,
                FetchedAt = now,
                ExpiresAt = now.AddMinutes(ttl),
            });
        }
        else
        {
            row.MenuJson = json;
            row.FetchedAt = now;
            row.ExpiresAt = now.AddMinutes(ttl);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Menu cache stored for {CacheKey} until {ExpiresAt:o}", key, now.AddMinutes(ttl));
    }

    private static Menu Deserialize(string json) =>
        JsonSerializer.Deserialize<Menu>(json, JsonOptions)
        ?? throw new InvalidOperationException("Cached menu JSON could not be deserialized.");
}
