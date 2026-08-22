using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StVrainToICSFunctionApp.Data;
using StVrainToICSFunctionApp.Models;

namespace StVrainToICSFunctionApp.Services;

public interface IFastLinkStore
{
    Task<FastLinkEntry?> GetAsync(string slug, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string slug, CancellationToken cancellationToken = default);

    Task<FastLinkEntry> CreateAsync(
        string slug,
        string buildingId,
        string districtId,
        string schoolName,
        string session,
        int displayTimeHhmm,
        IReadOnlyList<IncludedPlanFilter> plans,
        CancellationToken cancellationToken = default);
}

public sealed class FastLinkStore : IFastLinkStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly MenuCacheDbContext _db;

    public FastLinkStore(MenuCacheDbContext db)
    {
        _db = db;
    }

    public Task<FastLinkEntry?> GetAsync(string slug, CancellationToken cancellationToken = default) =>
        _db.FastLinks.AsNoTracking().FirstOrDefaultAsync(e => e.Slug == slug, cancellationToken);

    public Task<bool> ExistsAsync(string slug, CancellationToken cancellationToken = default) =>
        _db.FastLinks.AsNoTracking().AnyAsync(e => e.Slug == slug, cancellationToken);

    public async Task<FastLinkEntry> CreateAsync(
        string slug,
        string buildingId,
        string districtId,
        string schoolName,
        string session,
        int displayTimeHhmm,
        IReadOnlyList<IncludedPlanFilter> plans,
        CancellationToken cancellationToken = default)
    {
        FastLinkEntry entry = new()
        {
            Slug = slug,
            BuildingId = buildingId,
            DistrictId = districtId,
            SchoolName = schoolName,
            Session = session,
            DisplayTimeHhmm = displayTimeHhmm,
            IncludedPlansJson = JsonSerializer.Serialize(plans, JsonOptions),
            CreatedAtUtc = DateTime.UtcNow,
        };

        _db.FastLinks.Add(entry);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entry;
    }

    public static CalendarSectionFilter DeserializeFilter(string includedPlansJson)
    {
        List<IncludedPlanFilter>? plans = JsonSerializer.Deserialize<List<IncludedPlanFilter>>(includedPlansJson, JsonOptions);
        return new CalendarSectionFilter { Plans = plans ?? [] };
    }
}
