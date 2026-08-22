using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StVrainToICSFunctionApp.Data;
using StVrainToICSFunctionApp.Models;
using StVrainToICSFunctionApp.Options;
using StVrainToICSFunctionApp.Services;
using Xunit;

namespace StVrainToICSFunctionApp.E2ETests;

public sealed class LandingDayServiceTests
{
    [Fact]
    public async Task BuildAsync_returns_primary_and_next_for_requested_schools()
    {
        await using Harness harness = await Harness.CreateAsync();
        LandingDayResponse response = await harness.Service.BuildAsync(new LandingDayRequest
        {
            Date = "2026-08-25",
            Schools =
            [
                new LandingSchoolRequest
                {
                    BuildingId = "b1",
                    DistrictId = "d1",
                    Name = "School One",
                },
            ],
        });

        Assert.Equal("2026-08-25", response.Primary.Date);
        Assert.Single(response.Primary.Schools);
        Assert.Equal("School One", response.Primary.Schools[0].Name);
        Assert.Contains(response.Primary.Schools[0].Meals, m => m.Title == "1st Choice");
        Assert.Equal("2026-08-26", response.NextDate);
        Assert.NotNull(response.Upcoming);
        Assert.Equal("2026-08-26", response.Upcoming!.Date);
    }

    private sealed class FakeLinq : ILinqMenuClient
    {
        public Task<Menu> GetFamilyMenuAsync(
            string buildingId,
            string districtId,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Menu
            {
                FamilyMenuSessions =
                [
                    new FamilyMenuSession
                    {
                        ServingSession = "Lunch",
                        MenuPlans =
                        [
                            new MenuPlan
                            {
                                MenuPlanName = "Lunch Plan",
                                Days =
                                [
                                    Day("8/25/2026", "1st Choice"),
                                    Day("8/26/2026", "2nd Choice"),
                                ],
                            },
                        ],
                    },
                ],
            });

        public Task<FamilyMenuIdentifierResponse> GetFamilyMenuIdentifiersAsync(
            string identifier,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new FamilyMenuIdentifierResponse());

        private static MenuDay Day(string date, string meal) =>
            new()
            {
                Date = date,
                MenuMeals =
                [
                    new MenuMeal
                    {
                        MenuMealName = meal,
                        RecipeCategories =
                        [
                            new RecipeCategory
                            {
                                CategoryName = "Meal",
                                Recipes = [new Recipe { RecipeName = "Item" }],
                            },
                        ],
                    },
                ],
            };
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly string _dbPath;
        public required LandingDayService Service { get; init; }
        public required MenuCacheDbContext Db { get; init; }

        private Harness(string dbPath) => _dbPath = dbPath;

        public static async Task<Harness> CreateAsync()
        {
            string dbPath = Path.Combine(Path.GetTempPath(), $"landing-{Guid.NewGuid():N}.db");
            DbContextOptions<MenuCacheDbContext> options = new DbContextOptionsBuilder<MenuCacheDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;
            MenuCacheDbContext db = new(options);
            await db.Database.EnsureCreatedAsync();

            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DefaultStartOffset"] = "-30",
                    ["DefaultEndOffset"] = "30",
                })
                .Build();

            MenuCacheService cache = new(
                db,
                new FakeLinq(),
                Microsoft.Extensions.Options.Options.Create(new CacheOptions { Enabled = true, TtlMinutes = 360 }),
                NullLogger<MenuCacheService>.Instance);

            return new Harness(dbPath)
            {
                Db = db,
                Service = new LandingDayService(cache, config),
            };
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            try { File.Delete(_dbPath); } catch (IOException) { }
        }
    }
}
