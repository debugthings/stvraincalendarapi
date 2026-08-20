using Microsoft.Extensions.Configuration;
using StVrainToICSFunctionApp.Formatters;
using StVrainToICSFunctionApp.Models;
using StVrainToICSFunctionApp.Services;
using Xunit;

namespace StVrainToICSFunctionApp.E2ETests;

public sealed class SchoolShortcutAndFilterTests
{
    [Fact]
    public void Catalog_resolves_rhe_and_ems()
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SchoolShortcuts:rhe:BuildingId"] = "67673211-c4be-ed11-82b1-880d996bcdd8",
                ["SchoolShortcuts:rhe:DistrictId"] = "55485575-09b2-ed11-8e69-f29174b2df22",
                ["SchoolShortcuts:rhe:DefaultDisplayTime"] = "1130",
                ["SchoolShortcuts:ems:BuildingId"] = "3805e0fd-bdbe-ed11-82b1-880d996bcdd8",
                ["SchoolShortcuts:ems:DistrictId"] = "55485575-09b2-ed11-8e69-f29174b2df22",
                ["SchoolShortcuts:ems:DefaultDisplayTime"] = "1200",
            })
            .Build();

        SchoolShortcutCatalog catalog = new(config);

        Assert.True(catalog.TryGet("RHE", out var rhe));
        Assert.Equal(1130, rhe.DefaultDisplayTime);
        Assert.True(catalog.TryGet("ems", out var ems));
        Assert.Equal(1200, ems.DefaultDisplayTime);
        Assert.False(catalog.TryGet("nope", out _));
    }

    [Fact]
    public void Super_snack_plan_and_meal_are_detected()
    {
        Assert.True(ICSTextOutputFormatter.IsSuperSnack(new MenuPlan { MenuPlanName = "PK Super Snack 26/27" }));
        Assert.False(ICSTextOutputFormatter.IsSuperSnack(new MenuPlan { MenuPlanName = "Elementary Lunch 26/27" }));
        Assert.True(ICSTextOutputFormatter.IsSuperSnack(new MenuMeal { MenuMealName = "Super Snack" }));
        Assert.False(ICSTextOutputFormatter.IsSuperSnack(new MenuMeal { MenuMealName = "1st Choice" }));
    }
}
