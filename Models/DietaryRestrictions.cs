namespace StVrainToICSFunctionApp.Models
{

    public class DietaryRestrictions
    {
        public DietaryRestriction[]? Collection { get; set; }
    }

    public class DietaryRestriction
    {
        public string? DietaryRestrictionId { get; set; }
        public int SortOrder { get; set; }
        public string? Name { get; set; }
    }

}
