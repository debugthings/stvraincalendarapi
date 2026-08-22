using StVrainToICSFunctionApp.Models;
using StVrainToICSFunctionApp.Services;
using Xunit;

namespace StVrainToICSFunctionApp.E2ETests;

public sealed class MenuDayExtractorTests
{
    [Fact]
    public void ExtractForDate_includes_breakfast_lunch_and_non_meal_categories()
    {
        Menu menu = BuildMenu();
        IReadOnlyList<MenuDayExtractor.DayMealGroup> meals =
            MenuDayExtractor.ExtractForDate(menu, new DateOnly(2026, 8, 25));

        Assert.Contains(meals, m => m.ServingSession == "Breakfast" && m.Title == "Breakfast");
        Assert.Contains(meals, m => m.ServingSession == "Lunch" && m.Title == "1st Choice");
        Assert.Contains(meals, m => m.ServingSession == "Lunch" && m.Title == "Salad Bar");
        Assert.DoesNotContain(meals, m => m.Title.Contains("Super Snack", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Filter_keeps_only_selected_session_plan_meals()
    {
        Menu menu = BuildMenu();
        IReadOnlyList<MenuDayExtractor.DayMealGroup> all =
            MenuDayExtractor.ExtractForDate(menu, new DateOnly(2026, 8, 25));
        CalendarSectionFilter filter = new()
        {
            Plans =
            [
                new IncludedPlanFilter
                {
                    ServingSession = "Breakfast",
                    PlanName = "Breakfast in Cafe",
                    MealNames = ["Breakfast"],
                },
                new IncludedPlanFilter
                {
                    ServingSession = "Lunch",
                    PlanName = "Elementary Lunch 26/27",
                    MealNames = ["Salad Bar"],
                },
            ],
        };

        IReadOnlyList<MenuDayExtractor.DayMealGroup> filtered = MenuDayExtractor.Filter(all, filter);

        Assert.Equal(2, filtered.Count);
        Assert.Contains(filtered, m => m.Title == "Breakfast");
        Assert.Contains(filtered, m => m.Title == "Salad Bar");
        Assert.DoesNotContain(filtered, m => m.Title == "1st Choice");
    }

    [Fact]
    public void ExtractAvailableDates_spans_sessions()
    {
        Menu menu = BuildMenu();
        IReadOnlyList<DateOnly> dates = MenuDayExtractor.ExtractAvailableDates(menu);
        Assert.Equal([new DateOnly(2026, 8, 25)], dates);
    }

    private static Menu BuildMenu() =>
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
                                        Meal("Salad Bar", "Vegetable", "Carrots"),
                                        Meal("Super Snack", "Meal", "Chips"),
                                    ],
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
