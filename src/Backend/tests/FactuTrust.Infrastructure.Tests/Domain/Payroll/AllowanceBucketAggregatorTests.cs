using FactuTrust.Domain.Services.Payroll;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Payroll;

public sealed class AllowanceBucketAggregatorTests
{
    [Fact]
    public void Aggregate_QuadrantMatrixEnabled_SplitsFourBuckets()
    {
        var lines = new[]
        {
            new AllowanceLineInput("Prime A", 100m, true, true),
            new AllowanceLineInput("Prime B", 50m, true, false),
            new AllowanceLineInput("Prime C", 30m, false, true),
            new AllowanceLineInput("Prime D", 20m, false, false)
        };

        var result = AllowanceBucketAggregator.Aggregate(lines, enableQuadrantMatrix: true);

        Assert.Equal(100m, result.TaxableCnssable);
        Assert.Equal(50m, result.TaxableOnly);
        Assert.Equal(30m, result.CnssOnly);
        Assert.Equal(20m, result.NonTaxable);
        Assert.Equal(4, result.Lines.Count);
    }

    [Fact]
    public void Aggregate_SimplifiedModel_TaxableAllowancesStillTaxed_R25()
    {
        // R-25 : en mode legacy (matrice quadrant désactivée), une indemnité imposable mais non
        // soumise à la CNSS (Taxable=true, Cnss=false) doit aller en taxableOnly — elle reste
        // imposée à l'IRPP — et non plus en nonTaxable (sous-taxation silencieuse d'origine).
        // Les indemnités non imposables (Taxable=false) restent en nonTaxable.
        var lines = new[]
        {
            new AllowanceLineInput("Récurrente", 100m, true, true),
            new AllowanceLineInput("Transport", 40m, false, false),
            new AllowanceLineInput("Mixte", 25m, true, false)
        };

        var result = AllowanceBucketAggregator.Aggregate(lines, enableQuadrantMatrix: false);

        Assert.Equal(100m, result.TaxableCnssable);
        Assert.Equal(25m, result.TaxableOnly);
        Assert.Equal(0m, result.CnssOnly);
        Assert.Equal(40m, result.NonTaxable);
    }

    [Fact]
    public void Aggregate_ContractAndVariableLines_AreSummedTogether()
    {
        var lines = new[]
        {
            new AllowanceLineInput("Prime contrat", 200m, true, true),
            new AllowanceLineInput("Prime rendement", 150m, true, true)
        };

        var result = AllowanceBucketAggregator.Aggregate(lines, enableQuadrantMatrix: true);

        Assert.Equal(350m, result.TaxableCnssable);
        Assert.Equal(2, result.Lines.Count);
    }
}
