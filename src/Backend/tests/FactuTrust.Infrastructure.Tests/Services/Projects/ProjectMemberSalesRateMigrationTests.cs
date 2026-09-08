using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services.Projects;

/// <summary>
/// Documents and verifies the SQL backfill rules applied by
/// 20260906120000_SplitProjectMemberSalesRate_Tenant.
/// </summary>
public sealed class ProjectMemberSalesRateMigrationTests
{
    private sealed record MemberRow(decimal? DailyRate, decimal? HourlyCost, decimal? SalesRate);

    private static MemberRow ApplyBackfill(MemberRow row)
    {
        var salesRate = row.SalesRate;
        var hourlyCost = row.HourlyCost;

        if (salesRate is null && row.DailyRate is > 0)
            salesRate = row.DailyRate;

        if (salesRate is null && hourlyCost is > 0)
            salesRate = hourlyCost * 8;

        if ((hourlyCost is null or <= 0) && row.DailyRate is > 0)
            hourlyCost = decimal.Round(row.DailyRate.Value / 8m, 3);

        return row with { SalesRate = salesRate, HourlyCost = hourlyCost };
    }

    [Fact]
    public void Backfill_WhenDailyRateAndHourlyCostBothSet_PreservesBillingAndCost()
    {
        var result = ApplyBackfill(new MemberRow(320m, 100m, null));

        Assert.Equal(320m, result.SalesRate);
        Assert.Equal(100m, result.HourlyCost);
        Assert.Equal(40m, decimal.Round(result.SalesRate!.Value / 8m, 3));
    }

    [Fact]
    public void Backfill_WhenOnlyDailyRate_PreservesSingleRateBehavior()
    {
        var result = ApplyBackfill(new MemberRow(320m, null, null));

        Assert.Equal(320m, result.SalesRate);
        Assert.Equal(40m, result.HourlyCost);
    }

    [Fact]
    public void Backfill_WhenOnlyHourlyCost_PreservesSingleRateBehavior()
    {
        var result = ApplyBackfill(new MemberRow(null, 100m, null));

        Assert.Equal(800m, result.SalesRate);
        Assert.Equal(100m, result.HourlyCost);
    }
}
