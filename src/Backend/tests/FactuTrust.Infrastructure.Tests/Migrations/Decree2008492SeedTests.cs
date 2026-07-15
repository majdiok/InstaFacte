using Xunit;

namespace FactuTrust.Infrastructure.Tests.Migrations;

public sealed class Decree2008492SeedTests
{
    private static readonly string[] InitialCategoryCodes =
    {
        "LAND", "PRELIM", "PATENT", "BLDG_PERM", "BLDG_LIGHT", "BLDG_TEMP",
        "TECH_INST", "MACH_GEN", "MACH_AGRI", "IT_EQUIP", "OFF_FURN",
        "VEH_UTIL", "VEH_PASS", "AGRI_TREE", "AGRI_IRR", "OTHER"
    };

    private static readonly string[] Decree2008492CategoryCodes =
    {
        "BLDG_BRIDGE", "BLDG_DUR",
        "MAJOR_REP_3", "MAJOR_REP_4", "MAJOR_REP_6", "MAJOR_REP_8",
        "SHELVING", "CONTAINER", "TANK",
        "RAIL_20", "RAIL_30", "RAIL_15", "AIR_TRANS", "SEA_TRANS", "ROAD_TRANS", "PUB_WORKS",
        "UTIL_GAS",
        "HOTEL_KITCH", "HOTEL_DISH", "HOTEL_LINEN",
        "AGRI_TRACT", "AGRI_OLIVE_30", "AGRI_OLIVE_20", "AGRI_VINE", "AGRI_CITRUS", "AGRI_WELL", "AGRI_IRRIG"
    };

    [Fact]
    public void Decree2008492Seed_ShouldDefineAtLeast27AdditionalCategories()
    {
        Assert.Equal(27, Decree2008492CategoryCodes.Length);
    }

    [Fact]
    public void CombinedReferential_ShouldHaveAtLeast40Categories()
    {
        var all = InitialCategoryCodes.Concat(Decree2008492CategoryCodes).ToList();
        Assert.Equal(all.Count, all.Distinct().Count());
        Assert.True(all.Count >= 40, $"Expected >= 40 unique category codes, got {all.Count}");
    }
}