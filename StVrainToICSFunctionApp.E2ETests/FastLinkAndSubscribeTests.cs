using Microsoft.Extensions.Logging.Abstractions;
using StVrainToICSFunctionApp.Formatters;
using StVrainToICSFunctionApp.Middleware;
using StVrainToICSFunctionApp.Models;
using StVrainToICSFunctionApp.Services;
using Xunit;

namespace StVrainToICSFunctionApp.E2ETests;

public sealed class FastLinkAndSubscribeTests
{
    [Fact]
    public void Slug_generator_produces_valid_two_word_slug()
    {
        FastLinkSlugGenerator generator = new();
        for (int i = 0; i < 20; i++)
        {
            string slug = generator.Generate();
            Assert.True(FastLinkSlugGenerator.IsValidSlug(slug), slug);
            Assert.Contains('-', slug);
            Assert.DoesNotContain("--", slug);
        }
    }

    [Theory]
    [InlineData("fast-lynx", true)]
    [InlineData("describe-concise", true)]
    [InlineData("rhe", false)]
    [InlineData("fast-lynx-extra", false)]
    [InlineData("Fast-Lynx", false)]
    [InlineData("fast_lynx", false)]
    public void Slug_validation(string slug, bool expected) =>
        Assert.Equal(expected, FastLinkSlugGenerator.IsValidSlug(slug));

    [Fact]
    public void Proxy_recognizes_fastlink_paths()
    {
        Assert.True(CalendarProxyMiddleware.IsMenuIcsPath("/fast-lynx"));
        Assert.True(CalendarProxyMiddleware.IsMenuIcsPath("/fast-lynx.ics"));
        Assert.False(CalendarProxyMiddleware.IsMenuIcsPath("/rhe"));
        Assert.False(CalendarProxyMiddleware.IsMenuIcsPath("/today"));
        Assert.True(CalendarProxyMiddleware.IsMenuIcsPath("/rhe/lunchmenu"));
    }

    [Fact]
    public void Section_extractor_returns_all_sessions_and_non_meal_lines()
    {
        Menu menu = BuildMultiSessionMenu();
        IReadOnlyList<MenuSessionSectionDto> sessions = MenuSectionExtractor.ExtractSections(menu, lunchDefaultDisplayTime: 1130);

        Assert.Equal(3, sessions.Count);
        Assert.Contains(sessions, s => s.ServingSession == "Breakfast" && s.DefaultDisplayTimeHhmm == 830);
        Assert.Contains(sessions, s => s.ServingSession == "Lunch" && s.DefaultDisplayTimeHhmm == 1130);
        Assert.Contains(sessions, s => s.ServingSession == "PK Afternoon Snack" && s.DefaultDisplayTimeHhmm == 1430);

        MenuSessionSectionDto lunch = sessions.Single(s => s.ServingSession == "Lunch");
        Assert.Contains("1st Choice", lunch.Plans[0].MealNames);
        Assert.Contains("Salad Bar", lunch.Plans[0].MealNames);
        Assert.DoesNotContain(lunch.Plans, p => p.PlanName.Contains("Super Snack", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Section_filter_limits_ics_events()
    {
        Menu menu = BuildMultiSessionMenu();
        CalendarSectionFilter filter = new()
        {
            Plans =
            [
                new IncludedPlanFilter
                {
                    ServingSession = "Lunch",
                    PlanName = "Elementary Lunch 26/27",
                    MealNames = ["1st Choice"],
                    DisplayTimeHhmm = 1130,
                },
            ],
        };

        string ics = ICSTextOutputFormatter.FormatMenuToICSCore(
            menu,
            Session.Lunch,
            1130,
            filter,
            NullLogger.Instance);

        Assert.Contains("SUMMARY:1st Choice", ics);
        Assert.DoesNotContain("SUMMARY:2nd Choice", ics);
        Assert.DoesNotContain("SUMMARY:Breakfast", ics);
        Assert.DoesNotContain("Super Snack", ics);
    }

    [Fact]
    public void Multi_session_filter_emits_breakfast_and_lunch_at_different_times()
    {
        Menu menu = BuildMultiSessionMenu();
        CalendarSectionFilter filter = new()
        {
            Plans =
            [
                new IncludedPlanFilter
                {
                    ServingSession = "Breakfast",
                    PlanName = "Breakfast in Cafe",
                    MealNames = ["Breakfast"],
                    DisplayTimeHhmm = 830,
                },
                new IncludedPlanFilter
                {
                    ServingSession = "Lunch",
                    PlanName = "Elementary Lunch 26/27",
                    MealNames = ["Salad Bar"],
                    DisplayTimeHhmm = 1200,
                },
            ],
        };

        string ics = ICSTextOutputFormatter.FormatMenuToICSCore(
            menu,
            Session.Lunch,
            1130,
            filter,
            NullLogger.Instance);

        Assert.Contains("SUMMARY:Breakfast", ics);
        Assert.Contains("SUMMARY:Salad Bar", ics);
        Assert.Contains("DTSTART:20260825T083000", ics);
        Assert.Contains("DTSTART:20260825T120000", ics);
        Assert.DoesNotContain("SUMMARY:Snack", ics);
    }

    [Fact]
    public void Legacy_filter_json_without_serving_session_defaults_to_lunch()
    {
        string json = """[{"planName":"Elementary Lunch 26/27","mealNames":["1st Choice"]}]""";
        CalendarSectionFilter filter = FastLinkStore.DeserializeFilter(json);

        Assert.True(filter.AllowsMeal("Lunch", "Elementary Lunch 26/27", "1st Choice"));
        Assert.False(filter.AllowsMeal("Breakfast", "Elementary Lunch 26/27", "1st Choice"));

        string ics = ICSTextOutputFormatter.FormatMenuToICSCore(
            BuildMultiSessionMenu(),
            Session.Lunch,
            1130,
            filter,
            NullLogger.Instance);
        Assert.Contains("SUMMARY:1st Choice", ics);
        Assert.DoesNotContain("SUMMARY:Breakfast", ics);
    }

    [Fact]
    public void Without_filter_ics_includes_all_non_snack_meals_for_session()
    {
        Menu menu = BuildMultiSessionMenu();
        string ics = ICSTextOutputFormatter.FormatMenuToICSCore(
            menu,
            Session.Lunch,
            1200,
            sectionFilter: null,
            NullLogger.Instance);

        Assert.Contains("SUMMARY:1st Choice", ics);
        Assert.Contains("SUMMARY:2nd Choice", ics);
        Assert.DoesNotContain("SUMMARY:Salad Bar", ics); // Meal-category only without filter
        Assert.DoesNotContain("Super Snack", ics);
        Assert.DoesNotContain("SUMMARY:Breakfast", ics);
    }

    private static Menu BuildMultiSessionMenu() =>
        new()
        {
            FamilyMenuSessions =
            [
                new FamilyMenuSession
                {
                    ServingSession = "Breakfast",
                    MenuPlans =
                    [
                        new MenuPlan
                        {
                            MenuPlanName = "Breakfast in Cafe",
                            Days =
                            [
                                new MenuDay
                                {
                                    Date = "8/25/2026",
                                    MenuMeals =
                                    [
                                        Meal("Breakfast", "Meal", "Pancakes"),
                                        Meal("Cereal", "Meal", "Cheerios"),
                                    ],
                                },
                            ],
                        },
                    ],
                },
                new FamilyMenuSession
                {
                    ServingSession = "Lunch",
                    MenuPlans =
                    [
                        new MenuPlan
                        {
                            MenuPlanName = "Elementary Lunch 26/27",
                            Days =
                            [
                                new MenuDay
                                {
                                    Date = "8/25/2026",
                                    MenuMeals =
                                    [
                                        Meal("1st Choice", "Meal", "Tacos"),
                                        Meal("2nd Choice", "Meal", "Pizza"),
                                        Meal("Salad Bar", "Vegetable", "Carrots"),
                                        Meal("Super Snack", "Meal", "Chips"),
                                    ],
                                },
                            ],
                        },
                        new MenuPlan
                        {
                            MenuPlanName = "PK Super Snack 26/27",
                            Days =
                            [
                                new MenuDay
                                {
                                    Date = "8/25/2026",
                                    MenuMeals = [Meal("Super Snack", "Meal", "Crackers")],
                                },
                            ],
                        },
                    ],
                },
                new FamilyMenuSession
                {
                    ServingSession = "PK Afternoon Snack",
                    MenuPlans =
                    [
                        new MenuPlan
                        {
                            MenuPlanName = "PK All Day Snack 26/27",
                            Days =
                            [
                                new MenuDay
                                {
                                    Date = "8/25/2026",
                                    MenuMeals = [Meal("Snack", "Meal", "Apple")],
                                },
                            ],
                        },
                    ],
                },
            ],
        };

    private static MenuMeal Meal(string name, string category, string recipe) =>
        new()
        {
            MenuMealName = name,
            RecipeCategories =
            [
                new RecipeCategory
                {
                    CategoryName = category,
                    Recipes = [new Recipe { RecipeName = recipe }],
                },
            ],
        };
}
