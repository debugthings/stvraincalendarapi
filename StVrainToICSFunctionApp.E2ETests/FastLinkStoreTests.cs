using Microsoft.EntityFrameworkCore;
using StVrainToICSFunctionApp.Data;
using StVrainToICSFunctionApp.Models;
using StVrainToICSFunctionApp.Services;
using Xunit;

namespace StVrainToICSFunctionApp.E2ETests;

public sealed class FastLinkStoreTests
{
    [Fact]
    public async Task Create_and_get_round_trips_filter()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"fastlinks-{Guid.NewGuid():N}.db");
        try
        {
            DbContextOptions<MenuCacheDbContext> options = new DbContextOptionsBuilder<MenuCacheDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;
            await using MenuCacheDbContext db = new(options);
            await db.Database.EnsureCreatedAsync();
            OriginServiceCollectionExtensions.EnsureFastLinksTable(db);

            FastLinkStore store = new(db);
            await store.CreateAsync(
                "bright-hawk",
                "building",
                "district",
                "Red Hawk",
                "Lunch",
                1130,
                [
                    new IncludedPlanFilter
                    {
                        PlanName = "Elementary Lunch",
                        MealNames = ["1st Choice", "Pizza Line"],
                    },
                ]);

            FastLinkEntry? loaded = await store.GetAsync("bright-hawk");
            Assert.NotNull(loaded);
            Assert.Equal(1130, loaded!.DisplayTimeHhmm);
            CalendarSectionFilter filter = FastLinkStore.DeserializeFilter(loaded.IncludedPlansJson);
            Assert.True(filter.AllowsMeal("Lunch", "Elementary Lunch", "1st Choice"));
            Assert.False(filter.AllowsMeal("Lunch", "Elementary Lunch", "Bistro Box"));
            Assert.True(await store.ExistsAsync("bright-hawk"));
            Assert.False(await store.ExistsAsync("missing-slug"));
        }
        finally
        {
            try { File.Delete(dbPath); } catch (IOException) { }
        }
    }
}
