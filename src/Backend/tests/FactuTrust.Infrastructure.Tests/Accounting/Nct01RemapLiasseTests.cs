using FactuTrust.Infrastructure.Accounting;
using FactuTrust.Infrastructure.Services;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Accounting;

public sealed class Nct01RemapLiasseTests
{
    [Fact]
    public void AfterRemap_LandBalancesSitOnCorporealLine_NotIntangible()
    {
        var remap = SceChartCatalog.LoadRemap();
        var frenchChart = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["211"] = 10000m
        };
        var nctChart = frenchChart
            .ToDictionary(kv => remap.Rewrite(kv.Key), kv => kv.Value, StringComparer.Ordinal);

        var frenchLiasse = NctStatementBuilder.Build(2026, frenchChart, new Dictionary<string, decimal>(), enabled: true);
        var nctLiasse = NctStatementBuilder.Build(2026, nctChart, new Dictionary<string, decimal>(), enabled: true);

        Assert.Equal("221", remap.Rewrite("211"));
        Assert.Equal(10000m, frenchLiasse.BalanceSheet.Assets.Single(l => l.Code == "ANC1").Amount);
        Assert.Equal(0m, frenchLiasse.BalanceSheet.Assets.Single(l => l.Code == "ANC2").Amount);

        Assert.Equal(0m, nctLiasse.BalanceSheet.Assets.Single(l => l.Code == "ANC1").Amount);
        Assert.Equal(10000m, nctLiasse.BalanceSheet.Assets.Single(l => l.Code == "ANC2").Amount);
    }
}
