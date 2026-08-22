using StVrainToICSFunctionApp.Formatters;
using StVrainToICSFunctionApp.Models;

namespace StVrainToICSFunctionApp.Services;

public static class MenuSectionExtractor
{
    public static IReadOnlyList<MenuSessionSectionDto> ExtractSections(
        Menu menu,
        int lunchDefaultDisplayTime = 1130)
    {
        // Preserve first-seen order of sessions from the API.
        List<string> sessionOrder = [];
        Dictionary<string, Dictionary<string, SortedSet<string>>> bySession = new(StringComparer.OrdinalIgnoreCase);

        foreach (FamilyMenuSession familyMenuSession in menu.FamilyMenuSessions ?? [])
        {
            string? servingSession = familyMenuSession.ServingSession;
            if (string.IsNullOrWhiteSpace(servingSession))
            {
                continue;
            }

            if (!bySession.ContainsKey(servingSession))
            {
                sessionOrder.Add(servingSession);
                bySession[servingSession] = new Dictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);
            }

            Dictionary<string, SortedSet<string>> byPlan = bySession[servingSession];

            foreach (MenuPlan menuPlan in familyMenuSession.MenuPlans ?? [])
            {
                if (ICSTextOutputFormatter.IsSuperSnack(menuPlan) || string.IsNullOrEmpty(menuPlan.MenuPlanName))
                {
                    continue;
                }

                if (!byPlan.TryGetValue(menuPlan.MenuPlanName, out SortedSet<string>? meals))
                {
                    meals = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                    byPlan[menuPlan.MenuPlanName] = meals;
                }

                foreach (MenuDay day in menuPlan.Days ?? [])
                {
                    foreach (MenuMeal menuMeal in day.MenuMeals ?? [])
                    {
                        if (ICSTextOutputFormatter.IsSuperSnack(menuMeal)
                            || string.IsNullOrEmpty(menuMeal.MenuMealName)
                            || MenuLunchExtractor.GetAllRecipeNames(menuMeal).Count == 0)
                        {
                            continue;
                        }

                        meals.Add(menuMeal.MenuMealName);
                    }
                }
            }
        }

        List<MenuSessionSectionDto> result = [];
        foreach (string sessionName in sessionOrder)
        {
            Dictionary<string, SortedSet<string>> byPlan = bySession[sessionName];
            List<MenuPlanSectionDto> plans = byPlan
                .Where(pair => pair.Value.Count > 0)
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => new MenuPlanSectionDto
                {
                    PlanName = pair.Key,
                    MealNames = pair.Value.ToList(),
                })
                .ToList();

            if (plans.Count == 0)
            {
                continue;
            }

            result.Add(new MenuSessionSectionDto
            {
                ServingSession = sessionName,
                DefaultDisplayTimeHhmm = DefaultDisplayTime(sessionName, lunchDefaultDisplayTime),
                Plans = plans,
            });
        }

        return result;
    }

    /// <summary>Backward-compatible lunch-only helper.</summary>
    public static IReadOnlyList<MenuPlanSectionDto> ExtractLunchSections(Menu menu) =>
        ExtractSections(menu)
            .FirstOrDefault(s => s.ServingSession.Equals("Lunch", StringComparison.OrdinalIgnoreCase))
            ?.Plans
        ?? [];

    internal static int DefaultDisplayTime(string servingSession, int lunchDefaultDisplayTime)
    {
        if (servingSession.Contains("Breakfast", StringComparison.OrdinalIgnoreCase))
        {
            return 830;
        }

        if (servingSession.Contains("Lunch", StringComparison.OrdinalIgnoreCase))
        {
            return lunchDefaultDisplayTime is > 0 and <= 2359 ? lunchDefaultDisplayTime : 1130;
        }

        return 1430;
    }

    internal static bool IsDefaultCheckedSession(string servingSession) =>
        servingSession.Contains("Breakfast", StringComparison.OrdinalIgnoreCase)
        || servingSession.Contains("Lunch", StringComparison.OrdinalIgnoreCase);
}
