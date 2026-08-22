using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StVrainToICSFunctionApp.Data;
using StVrainToICSFunctionApp.Models;
using StVrainToICSFunctionApp.Options;
using StVrainToICSFunctionApp.Services;
using Xunit;

namespace StVrainToICSFunctionApp.E2ETests;

public sealed class MenuCacheServiceTests
{
    private static readonly DateTime Start = new(2026, 8, 12);
    private static readonly DateTime End = new(2026, 9, 18);
    private const string BuildingId = "building";
    private const string DistrictId = "district";

    [Fact]
    public async Task Cache_hit_does_not_call_LINQ()
    {
        await using CacheHarness harness = await CacheHarness.CreateAsync();
        harness.Linq.Menu = SampleMenu("fresh");
        await harness.SeedAsync(expiresAt: DateTime.UtcNow.AddHours(1), jsonName: "cached");

        Menu result = await harness.Service.GetMenuAsync(BuildingId, DistrictId, Start, End);

        Assert.Equal("cached", result.FamilyMenuSessions![0].ServingSession);
        Assert.Equal(0, harness.Linq.Calls);
    }

    [Fact]
    public async Task Expired_cache_refreshes_from_LINQ()
    {
        await using CacheHarness harness = await CacheHarness.CreateAsync();
        harness.Linq.Menu = SampleMenu("refreshed");
        await harness.SeedAsync(expiresAt: DateTime.UtcNow.AddHours(-1), jsonName: "stale");

        Menu result = await harness.Service.GetMenuAsync(BuildingId, DistrictId, Start, End);

        Assert.Equal("refreshed", result.FamilyMenuSessions![0].ServingSession);
        Assert.Equal(1, harness.Linq.Calls);
    }

    [Fact]
    public async Task LINQ_failure_returns_stale_row()
    {
        await using CacheHarness harness = await CacheHarness.CreateAsync();
        harness.Linq.Fail = new HttpRequestException("LINQ down");
        await harness.SeedAsync(expiresAt: DateTime.UtcNow.AddHours(-1), jsonName: "stale");

        Menu result = await harness.Service.GetMenuAsync(BuildingId, DistrictId, Start, End);

        Assert.Equal("stale", result.FamilyMenuSessions![0].ServingSession);
        Assert.Equal(1, harness.Linq.Calls);
    }

    [Fact]
    public async Task LINQ_failure_without_cache_rethrows()
    {
        await using CacheHarness harness = await CacheHarness.CreateAsync();
        harness.Linq.Fail = new HttpRequestException("LINQ down");

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            harness.Service.GetMenuAsync(BuildingId, DistrictId, Start, End));
    }

    private static Menu SampleMenu(string sessionName) => new()
    {
        FamilyMenuSessions =
        [
            new FamilyMenuSession { ServingSession = sessionName },
        ],
    };

    private sealed class FakeLinqMenuClient : ILinqMenuClient
    {
        public int Calls { get; private set; }
        public Exception? Fail { get; set; }
        public Menu Menu { get; set; } = SampleMenu("live");

        public Task<Menu> GetFamilyMenuAsync(
            string buildingId,
            string districtId,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            if (Fail is not null)
            {
                throw Fail;
            }

            return Task.FromResult(Menu);
        }

        public Task<FamilyMenuIdentifierResponse> GetFamilyMenuIdentifiersAsync(
            string identifier,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new FamilyMenuIdentifierResponse
            {
                DistrictId = DistrictId,
                Buildings =
                [
                    new FamilyMenuBuilding { BuildingId = BuildingId, Name = "Test School" },
                ],
            });
    }

    private sealed class CacheHarness : IAsyncDisposable
    {
        private readonly string _dbPath;
        public required MenuCacheDbContext Db { get; init; }
        public required FakeLinqMenuClient Linq { get; init; }
        public required MenuCacheService Service { get; init; }

        private CacheHarness(string dbPath)
        {
            _dbPath = dbPath;
        }

        public static async Task<CacheHarness> CreateAsync()
        {
            string dbPath = Path.Combine(Path.GetTempPath(), $"menu-cache-{Guid.NewGuid():N}.db");
            DbContextOptions<MenuCacheDbContext> options = new DbContextOptionsBuilder<MenuCacheDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;
            MenuCacheDbContext db = new(options);
            await db.Database.EnsureCreatedAsync();

            FakeLinqMenuClient linq = new();
            MenuCacheService service = new(
                db,
                linq,
                Microsoft.Extensions.Options.Options.Create(new CacheOptions { Enabled = true, TtlMinutes = 360 }),
                NullLogger<MenuCacheService>.Instance);

            return new CacheHarness(dbPath)
            {
                Db = db,
                Linq = linq,
                Service = service,
            };
        }

        public async Task SeedAsync(DateTime expiresAt, string jsonName)
        {
            string key = MenuCacheService.BuildCacheKey(BuildingId, DistrictId, Start, End);
            Db.MenuCacheEntries.Add(new MenuCacheEntry
            {
                CacheKey = key,
                MenuJson = $$"""{"FamilyMenuSessions":[{"ServingSession":"{{jsonName}}"}]}""",
                FetchedAt = DateTime.UtcNow.AddHours(-2),
                ExpiresAt = expiresAt,
            });
            await Db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            try
            {
                File.Delete(_dbPath);
            }
            catch (IOException)
            {
            }
        }
    }
}
