using System.Globalization;
using StVrainToICSFunctionApp.Formatters;
using StVrainToICSFunctionApp.Models;

namespace StVrainToICSFunctionApp.Services;

public static class MenuLunchExtractor
{
    public sealed record LunchMealGroup(string Title, IReadOnlyList<string> Items);

    public static IReadOnlyList<LunchMealGroup> ExtractForDate(Menu menu, DateOnly targetDate)
    {
        List<LunchMealGroup> groups = [];

        foreach (FamilyMenuSession familyMenuSession in menu.FamilyMenuSessions ?? [])
        {
            if (!Enum.TryParse(familyMenuSession.ServingSession, out Session session) || session != Session.Lunch)
            {
                continue;
            }

            foreach (MenuPlan menuPlan in familyMenuSession.MenuPlans ?? [])
            {
                if (ICSTextOutputFormatter.IsSuperSnack(menuPlan))
                {
                    continue;
                }

                foreach (MenuDay day in menuPlan.Days ?? [])
                {
                    if (string.IsNullOrEmpty(day.Date))
                    {
                        continue;
                    }

                    DateOnly dayDate = DateOnly.FromDateTime(
                        DateTime.Parse(day.Date, CultureInfo.InvariantCulture));
                    if (dayDate != targetDate)
                    {
                        continue;
                    }

                    foreach (MenuMeal menuMeal in day.MenuMeals ?? [])
                    {
                        if (ICSTextOutputFormatter.IsSuperSnack(menuMeal))
                        {
                            continue;
                        }

                        string? title = menuMeal.MenuMealName;
                        if (string.IsNullOrEmpty(title))
                        {
                            continue;
                        }

                        IReadOnlyList<string> items = GetMealRecipeNames(menuMeal);
                        if (items.Count == 0)
                        {
                            continue;
                        }

                        groups.Add(new LunchMealGroup(title, items));
                    }
                }
            }
        }

        return groups;
    }

    /// <summary>
    /// Returns sorted distinct lunch dates that have at least one non–Super Snack meal.
    /// </summary>
    public static IReadOnlyList<DateOnly> ExtractAvailableDates(Menu menu)
    {
        SortedSet<DateOnly> dates = [];

        foreach (FamilyMenuSession familyMenuSession in menu.FamilyMenuSessions ?? [])
        {
            if (!Enum.TryParse(familyMenuSession.ServingSession, out Session session) || session != Session.Lunch)
            {
                continue;
            }

            foreach (MenuPlan menuPlan in familyMenuSession.MenuPlans ?? [])
            {
                if (ICSTextOutputFormatter.IsSuperSnack(menuPlan))
                {
                    continue;
                }

                foreach (MenuDay day in menuPlan.Days ?? [])
                {
                    if (string.IsNullOrEmpty(day.Date))
                    {
                        continue;
                    }

                    DateOnly dayDate = DateOnly.FromDateTime(
                        DateTime.Parse(day.Date, CultureInfo.InvariantCulture));

                    foreach (MenuMeal menuMeal in day.MenuMeals ?? [])
                    {
                        if (ICSTextOutputFormatter.IsSuperSnack(menuMeal))
                        {
                            continue;
                        }

                        if (string.IsNullOrEmpty(menuMeal.MenuMealName))
                        {
                            continue;
                        }

                        if (GetMealRecipeNames(menuMeal).Count == 0)
                        {
                            continue;
                        }

                        dates.Add(dayDate);
                        break;
                    }
                }
            }
        }

        return dates.ToList();
    }

    internal static IReadOnlyList<string> GetMealRecipeNames(MenuMeal? menuMeal)
    {
        IEnumerable<Recipe[]?> recipeMeals = menuMeal?.RecipeCategories?
            .Where(rc => !string.IsNullOrEmpty(rc.CategoryName)
                && rc.CategoryName.Equals("Meal", StringComparison.OrdinalIgnoreCase))
            .Select(rc => rc.Recipes)
            ?? [];

        List<string> recipeNames = [];
        foreach (Recipe[]? recipes in recipeMeals)
        {
            for (int i = 0; i < (recipes?.Length ?? 0); i++)
            {
                string recipeName = recipes?[i]?.RecipeName ?? "Item Name Empty";
                recipeNames.Add(recipeName);
            }
        }

        return recipeNames;
    }

    internal static IReadOnlyList<string> GetAllRecipeNames(MenuMeal? menuMeal)
    {
        List<string> recipeNames = [];
        foreach (RecipeCategory category in menuMeal?.RecipeCategories ?? [])
        {
            Recipe[]? recipes = category.Recipes;
            for (int i = 0; i < (recipes?.Length ?? 0); i++)
            {
                string recipeName = recipes?[i]?.RecipeName ?? "Item Name Empty";
                recipeNames.Add(recipeName);
            }
        }

        return recipeNames;
    }

    /// <summary>Prefer Meal-category recipes; fall back to all categories.</summary>
    internal static IReadOnlyList<string> GetRecipeNamesForCalendar(MenuMeal? menuMeal)
    {
        IReadOnlyList<string> mealOnly = GetMealRecipeNames(menuMeal);
        return mealOnly.Count > 0 ? mealOnly : GetAllRecipeNames(menuMeal);
    }
}
