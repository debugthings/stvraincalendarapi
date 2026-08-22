using StVrainToICSFunctionApp.Models;
using StVrainToICSFunctionApp.Services;
using Xunit;

namespace StVrainToICSFunctionApp.E2ETests;

public sealed class MenuLunchExtractorTests
{
    [Fact]
    public void ExtractForDate_returns_meals_grouped_by_title()
    {
        Menu menu = BuildSampleMenu();

        IReadOnlyList<MenuLunchExtractor.LunchMealGroup> meals =
            MenuLunchExtractor.ExtractForDate(menu, new DateOnly(2026, 8, 22));

        Assert.Equal(2, meals.Count);
        Assert.Equal("1st Choice", meals[0].Title);
        Assert.Equal(["Chicken Tenders", "Mashed Potatoes"], meals[0].Items);
        Assert.Equal("2nd Choice", meals[1].Title);
        Assert.Equal(["Cheese Pizza"], meals[1].Items);
    }

    [Fact]
    public void ExtractForDate_returns_empty_for_days_without_lunch()
    {
        Menu menu = BuildSampleMenu();

        IReadOnlyList<MenuLunchExtractor.LunchMealGroup> meals =
            MenuLunchExtractor.ExtractForDate(menu, new DateOnly(2026, 8, 24));

        Assert.Empty(meals);
    }

    [Fact]
    public void FormatDisplayTime_formats_hhmm()
    {
        Assert.Equal("11:30 AM", TodayMenuPageService.FormatDisplayTime(1130));
        Assert.Equal("12:00 PM", TodayMenuPageService.FormatDisplayTime(1200));
    }

    [Fact]
    public void ExtractAvailableDates_skips_super_snack_and_breakfast()
    {
        Menu menu = BuildSampleMenu();

        IReadOnlyList<DateOnly> dates = MenuLunchExtractor.ExtractAvailableDates(menu);

        Assert.Equal(
            [new DateOnly(2026, 8, 22), new DateOnly(2026, 8, 23)],
            dates);
    }

    [Fact]
    public void ResolveSelectedDate_skips_weekend_to_next_lunch()
    {
        IReadOnlyList<DateOnly> dates =
        [
            new DateOnly(2026, 8, 21),
            new DateOnly(2026, 8, 24),
            new DateOnly(2026, 8, 25),
        ];

        DateOnly selected = TodayMenuPageService.ResolveSelectedDate(
            dates,
            today: new DateOnly(2026, 8, 22),
            requestedDate: null);

        Assert.Equal(new DateOnly(2026, 8, 24), selected);
    }

    [Fact]
    public void ResolveSelectedDate_uses_today_when_lunch_exists()
    {
        IReadOnlyList<DateOnly> dates =
        [
            new DateOnly(2026, 8, 21),
            new DateOnly(2026, 8, 24),
        ];

        DateOnly selected = TodayMenuPageService.ResolveSelectedDate(
            dates,
            today: new DateOnly(2026, 8, 24),
            requestedDate: null);

        Assert.Equal(new DateOnly(2026, 8, 24), selected);
    }

    [Fact]
    public void Next_and_previous_lunch_dates_walk_available_days()
    {
        IReadOnlyList<DateOnly> dates =
        [
            new DateOnly(2026, 8, 24),
            new DateOnly(2026, 8, 25),
            new DateOnly(2026, 8, 26),
        ];

        Assert.Equal(new DateOnly(2026, 8, 25), TodayMenuPageService.NextLunchDate(dates, new DateOnly(2026, 8, 24)));
        Assert.Null(TodayMenuPageService.NextLunchDate(dates, new DateOnly(2026, 8, 26)));
        Assert.Equal(new DateOnly(2026, 8, 24), TodayMenuPageService.PreviousLunchDate(dates, new DateOnly(2026, 8, 25)));
        Assert.Null(TodayMenuPageService.PreviousLunchDate(dates, new DateOnly(2026, 8, 24)));
    }

    private static Menu BuildSampleMenu() =>
        new()
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
                            MenuPlanName = "Elementary Lunch 26/27",
                            Days =
                            [
                                new MenuDay
                                {
                                    Date = "8/22/2026",
                                    MenuMeals =
                                    [
                                        new MenuMeal
                                        {
                                            MenuMealName = "1st Choice",
                                            RecipeCategories =
                                            [
                                                new RecipeCategory
                                                {
                                                    CategoryName = "Meal",
                                                    Recipes =
                                                    [
                                                        new Recipe { RecipeName = "Chicken Tenders" },
                                                        new Recipe { RecipeName = "Mashed Potatoes" },
                                                    ],
                                                },
                                            ],
                                        },
                                        new MenuMeal
                                        {
                                            MenuMealName = "2nd Choice",
                                            RecipeCategories =
                                            [
                                                new RecipeCategory
                                                {
                                                    CategoryName = "Meal",
                                                    Recipes = [new Recipe { RecipeName = "Cheese Pizza" }],
                                                },
                                            ],
                                        },
                                        new MenuMeal
                                        {
                                            MenuMealName = "Super Snack",
                                            RecipeCategories =
                                            [
                                                new RecipeCategory
                                                {
                                                    CategoryName = "Meal",
                                                    Recipes = [new Recipe { RecipeName = "Goldfish" }],
                                                },
                                            ],
                                        },
                                    ],
                                },
                                new MenuDay
                                {
                                    Date = "8/23/2026",
                                    MenuMeals =
                                    [
                                        new MenuMeal
                                        {
                                            MenuMealName = "1st Choice",
                                            RecipeCategories =
                                            [
                                                new RecipeCategory
                                                {
                                                    CategoryName = "Meal",
                                                    Recipes = [new Recipe { RecipeName = "Tomorrow Only" }],
                                                },
                                            ],
                                        },
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
                                    Date = "8/22/2026",
                                    MenuMeals =
                                    [
                                        new MenuMeal
                                        {
                                            MenuMealName = "Snack",
                                            RecipeCategories =
                                            [
                                                new RecipeCategory
                                                {
                                                    CategoryName = "Meal",
                                                    Recipes = [new Recipe { RecipeName = "Crackers" }],
                                                },
                                            ],
                                        },
                                    ],
                                },
                            ],
                        },
                    ],
                },
                new FamilyMenuSession
                {
                    ServingSession = "Breakfast",
                    MenuPlans =
                    [
                        new MenuPlan
                        {
                            MenuPlanName = "Elementary Breakfast 26/27",
                            Days =
                            [
                                new MenuDay
                                {
                                    Date = "8/22/2026",
                                    MenuMeals =
                                    [
                                        new MenuMeal
                                        {
                                            MenuMealName = "Breakfast",
                                            RecipeCategories =
                                            [
                                                new RecipeCategory
                                                {
                                                    CategoryName = "Meal",
                                                    Recipes = [new Recipe { RecipeName = "Cereal" }],
                                                },
                                            ],
                                        },
                                    ],
                                },
                            ],
                        },
                    ],
                },
            ],
        };
}
