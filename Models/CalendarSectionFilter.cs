namespace StVrainToICSFunctionApp.Models;

public sealed class IncludedPlanFilter
{
    /// <summary>Serving session name from LINQ, e.g. Breakfast or Lunch. Empty means Lunch (legacy).</summary>
    public string ServingSession { get; set; } = string.Empty;

    public string PlanName { get; set; } = string.Empty;

    public List<string> MealNames { get; set; } = [];

    /// <summary>Optional HHmm start time for this plan's session.</summary>
    public int? DisplayTimeHhmm { get; set; }

    public string EffectiveServingSession =>
        string.IsNullOrWhiteSpace(ServingSession) ? "Lunch" : ServingSession.Trim();
}

public sealed class CalendarSectionFilter
{
    public IReadOnlyList<IncludedPlanFilter> Plans { get; init; } = [];

    public bool AllowsSession(string? servingSession)
    {
        if (string.IsNullOrEmpty(servingSession) || Plans.Count == 0)
        {
            return false;
        }

        return Plans.Any(p =>
            string.Equals(p.EffectiveServingSession, servingSession, StringComparison.OrdinalIgnoreCase));
    }

    public bool AllowsPlan(string? servingSession, string? planName)
    {
        if (string.IsNullOrEmpty(servingSession) || string.IsNullOrEmpty(planName) || Plans.Count == 0)
        {
            return false;
        }

        return Plans.Any(p =>
            string.Equals(p.EffectiveServingSession, servingSession, StringComparison.OrdinalIgnoreCase)
            && string.Equals(p.PlanName, planName, StringComparison.OrdinalIgnoreCase));
    }

    public bool AllowsMeal(string? servingSession, string? planName, string? mealName)
    {
        if (string.IsNullOrEmpty(servingSession)
            || string.IsNullOrEmpty(planName)
            || string.IsNullOrEmpty(mealName)
            || Plans.Count == 0)
        {
            return false;
        }

        IncludedPlanFilter? plan = Plans.FirstOrDefault(p =>
            string.Equals(p.EffectiveServingSession, servingSession, StringComparison.OrdinalIgnoreCase)
            && string.Equals(p.PlanName, planName, StringComparison.OrdinalIgnoreCase));
        if (plan is null || plan.MealNames.Count == 0)
        {
            return false;
        }

        return plan.MealNames.Any(m => string.Equals(m, mealName, StringComparison.OrdinalIgnoreCase));
    }

    public int? DisplayTimeFor(string? servingSession, string? planName, int? fallbackHhmm)
    {
        IncludedPlanFilter? plan = Plans.FirstOrDefault(p =>
            string.Equals(p.EffectiveServingSession, servingSession, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrEmpty(planName)
                || string.Equals(p.PlanName, planName, StringComparison.OrdinalIgnoreCase))
            && p.DisplayTimeHhmm is int);

        if (plan?.DisplayTimeHhmm is int fromPlan)
        {
            return fromPlan;
        }

        IncludedPlanFilter? sessionPlan = Plans.FirstOrDefault(p =>
            string.Equals(p.EffectiveServingSession, servingSession, StringComparison.OrdinalIgnoreCase)
            && p.DisplayTimeHhmm is int);
        if (sessionPlan?.DisplayTimeHhmm is int fromSession)
        {
            return fromSession;
        }

        return fallbackHhmm;
    }
}
