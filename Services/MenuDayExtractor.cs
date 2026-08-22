using System.Globalization;
using StVrainToICSFunctionApp.Formatters;
using StVrainToICSFunctionApp.Models;

namespace StVrainToICSFunctionApp.Services;

public static class MenuDayExtractor
{
    public sealed record DayMealGroup(
        string ServingSession,
        string PlanName,
        string Title,
        IReadOnlyList<string> Items);

    /// <summary>
    /// All meal lines for a date across every serving session (excludes Super Snack).
    /// Uses Meal-category recipes when present, otherwise all recipe categories.
    /// </summary>
    public static IReadOnlyList<DayMealGroup> ExtractForDate(Menu menu, DateOnly targetDate)
    {
        List<DayMealGroup> groups = [];

        foreach (FamilyMenuSession familyMenuSession in menu.FamilyMenuSessions ?? [])
        {
            string? servingSession = familyMenuSession.ServingSession;
            if (string.IsNullOrWhiteSpace(servingSession))
            {
                continue;
            }

            foreach (MenuPlan menuPlan in familyMenuSession.MenuPlans ?? [])
            {
                if (ICSTextOutputFormatter.IsSuperSnack(menuPlan) || string.IsNullOrEmpty(menuPlan.MenuPlanName))
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

                        IReadOnlyList<string> items = MenuLunchExtractor.GetRecipeNamesForCalendar(menuMeal);
                        if (items.Count == 0)
                        {
                            continue;
                        }

                        groups.Add(new DayMealGroup(servingSession, menuPlan.MenuPlanName, title, items));
                    }
                }
            }
        }

        return groups;
    }

    public static IReadOnlyList<DayMealGroup> Filter(
        IReadOnlyList<DayMealGroup> groups,
        CalendarSectionFilter? filter)
    {
        if (filter is null || filter.Plans.Count == 0)
        {
            return groups;
        }

        return groups
            .Where(g => filter.AllowsMeal(g.ServingSession, g.PlanName, g.Title))
            .ToList();
    }

    /// <summary>
    /// Distinct dates that have at least one non–Super Snack meal line with recipes.
    /// </summary>
    public static IReadOnlyList<DateOnly> ExtractAvailableDates(Menu menu)
    {
        SortedSet<DateOnly> dates = [];

        foreach (FamilyMenuSession familyMenuSession in menu.FamilyMenuSessions ?? [])
        {
            if (string.IsNullOrWhiteSpace(familyMenuSession.ServingSession))
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
                        if (ICSTextOutputFormatter.IsSuperSnack(menuMeal)
                            || string.IsNullOrEmpty(menuMeal.MenuMealName)
                            || MenuLunchExtractor.GetRecipeNamesForCalendar(menuMeal).Count == 0)
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
}
