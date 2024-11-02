namespace StVrainToICSFunctionApp.Models
{

    public class Allergies
    {
        public Allergies[]? Collection { get; set; }
    }

    public class Allergy
    {
        public string? AllergyId { get; set; }
        public int SortOrder { get; set; }
        public string? Name { get; set; }
    }

}
