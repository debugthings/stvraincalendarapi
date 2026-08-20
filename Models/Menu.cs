using Microsoft.Azure.Functions.Worker.Converters;
using StVrainToICSFunctionApp.Formatters;

namespace StVrainToICSFunctionApp.Models
{
    [InputConverter(typeof(SessionInputConverter))]
    public enum Session
    {
        None = 0,
        Academic = 1,
        Breakfast = 8,
        Lunch = 11
    }

    public class Menu
    {
        public FamilyMenuSession[]? FamilyMenuSessions { get; set; }
        public AcademicCalendar[]? AcademicCalendars { get; set; }
    }

    public class FamilyMenuSession
    {
        public string? ServingSessionKey { get; set; }
        public string? ServingSessionId { get; set; }
        public string? ServingSession { get; set; }
        public MenuPlan[]? MenuPlans { get; set; }
    }

    public class MenuPlan
    {
        public string? MenuPlanName { get; set; }
        public string? MenuPlanId { get; set; }
        public MenuDay[]? Days { get; set; }
        public string? AcademicCalenderId { get; set; }
    }

    public class MenuDay
    {
        public string? Date { get; set; }
        public MenuMeal[]? MenuMeals { get; set; }
    }

    public class MenuMeal
    {
        public string? MenuPlanName { get; set; }
        public string? MenuMealName { get; set; }
        public string? MenuMealId { get; set; }
        public RecipeCategory[]? RecipeCategories { get; set; }
    }

    public class RecipeCategory
    {
        public string? CategoryName { get; set; }
        public string? Color { get; set; }
        public Recipe[]? Recipes { get; set; }
    }

    public class Recipe
    {
        public string? ItemId { get; set; }
        public string? RecipeIdentifier { get; set; }
        public string? RecipeName { get; set; }
        public string? ServingSize { get; set; }
        public float GramPerServing { get; set; }
        public Nutrient[]? Nutrients { get; set; }
        public string[]? Allergens { get; set; }
        public object[]? ReligiousRestrictions { get; set; }
        public string[]? DietaryRestrictions { get; set; }
        public bool HasNutrients { get; set; }
    }

    public class Nutrient
    {
        public string? Name { get; set; }
        public int Value { get; set; }
        public bool HasMissingNutrients { get; set; }
        public string? Unit { get; set; }
        public string? Abbreviation { get; set; }
    }

    public class AcademicCalendar
    {
        public string? AcademicCalendarId { get; set; }
        public AcademicDay[]? Days { get; set; }
    }

    public class AcademicDay
    {
        public string? Date { get; set; }
        public string? Note { get; set; }
    }
}
