using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Services;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class DepreciationEngineTests
{
    private readonly DepreciationEngine _engine = new();
    private static readonly Guid CategoryId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0007");

    private static FixedAsset CreateAsset(
        decimal cost,
        decimal rate,
        DateTime acquisition,
        DateTime inService,
        DateTime? disposal = null,
        decimal residual = 0m,
        DepreciationMethod method = DepreciationMethod.Linear,
        decimal coefficient = 1m)
    {
        var asset = FixedAsset.Create(
            "IMMO-2026-0001",
            "Machine test",
            CategoryId,
            rate,
            100m / rate,
            "213",
            "2813",
            "6813",
            cost,
            0m,
            residual,
            acquisition,
            depreciationMethod: method,
            accelerationCoefficient: coefficient).Value;

        asset.PutInService(inService, "404");
        if (disposal is not null)
            asset.Dispose(disposal.Value, 0m, "5321");

        return asset;
    }

    [Fact]
    public void GenerateSchedule_MarchInService_15Percent_ShouldProrateFirstYear()
    {
        var asset = CreateAsset(30_000m, 15m, new DateTime(2026, 1, 10), new DateTime(2026, 3, 1));
        var lines = _engine.GenerateSchedule(asset, throughFiscalYear: 2026);

        Assert.Single(lines);
        Assert.Equal(3_750m, lines[0].DepreciationAmount);
        Assert.Equal(26_250m, lines[0].ClosingNbv);
    }

    [Fact]
    public void GenerateSchedule_FullLife_ShouldReachDepreciableBase()
    {
        var asset = CreateAsset(10_000m, 20m, new DateTime(2026, 1, 1), new DateTime(2026, 1, 1));
        var lines = _engine.GenerateSchedule(asset);

        Assert.True(lines.Count >= 5);
        Assert.Equal(10_000m, lines[^1].AccumulatedDepreciation);
        Assert.Equal(0m, lines[^1].ClosingNbv);
    }

    [Fact]
    public void GenerateSchedule_WithResidual_ShouldStopAtResidualNbv()
    {
        var asset = CreateAsset(10_000m, 20m, new DateTime(2026, 1, 1), new DateTime(2026, 1, 1), residual: 1m);
        var lines = _engine.GenerateSchedule(asset);

        Assert.Equal(9_999m, lines[^1].AccumulatedDepreciation);
        Assert.Equal(1m, lines[^1].ClosingNbv);
    }

    [Fact]
    public void GenerateSchedule_NonDepreciable_ShouldReturnEmpty()
    {
        var asset = FixedAsset.Create(
            "IMMO-2026-0002",
            "Terrain",
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0001"),
            0m,
            0m,
            "211",
            "2811",
            "6811",
            50_000m,
            0m,
            0m,
            new DateTime(2026, 1, 1)).Value;
        asset.PutInService(new DateTime(2026, 1, 1), "404");

        var lines = _engine.GenerateSchedule(asset);
        Assert.Empty(lines);
    }

    [Fact]
    public void GenerateSchedule_DisposalMidYear_ShouldProrateExitYear()
    {
        var asset = CreateAsset(
            24_000m,
            15m,
            new DateTime(2026, 1, 1),
            new DateTime(2026, 1, 1),
            disposal: new DateTime(2028, 6, 30));

        var lines = _engine.GenerateSchedule(asset);
        var year2028 = lines.First(l => l.FiscalYear == 2028);

        Assert.Equal(1_800m, year2028.DepreciationAmount);
    }

    [Fact]
    public void Round3_ShouldUseMillimes()
    {
        Assert.Equal(333.333m, DepreciationEngine.Round3(1000m / 3m));
    }

    // ------------------------------------------------------------------
    // Prorata temporis en jours, base 360 (norme tunisienne)
    // ------------------------------------------------------------------

    [Fact]
    public void GenerateSchedule_OfficialExample_4000Over5Years_OctoberInService_ShouldMatchNormTable()
    {
        // Exemple officiel : ordinateur 4000 TND, 5 ans, 20 %, mise en service 01/10
        // → 200 / 800 / 800 / 800 / 800 / 600, total = 4000.
        var asset = CreateAsset(4_000m, 20m, new DateTime(2010, 8, 1), new DateTime(2010, 10, 1));
        var lines = _engine.GenerateSchedule(asset);

        Assert.Equal(6, lines.Count);
        Assert.Equal(200m, lines[0].DepreciationAmount);      // 800 × 90/360
        Assert.Equal(800m, lines[1].DepreciationAmount);
        Assert.Equal(800m, lines[2].DepreciationAmount);
        Assert.Equal(800m, lines[3].DepreciationAmount);
        Assert.Equal(800m, lines[4].DepreciationAmount);
        Assert.Equal(600m, lines[^1].DepreciationAmount);     // complément de la dernière année
        Assert.Equal(4_000m, lines[^1].AccumulatedDepreciation);
        Assert.Equal(0m, lines[^1].ClosingNbv);
        Assert.Equal(4_000m, lines.Sum(l => l.DepreciationAmount));
    }

    [Fact]
    public void GenerateSchedule_Days360_AugustFirstInService_ShouldProrate150Days()
    {
        // Mise en service 01/08 → jours restants = 360 - 211 + 1 = 150 → 800 × 150/360 = 333,333.
        var asset = CreateAsset(4_000m, 20m, new DateTime(2010, 8, 1), new DateTime(2010, 8, 1));
        var lines = _engine.GenerateSchedule(asset, throughFiscalYear: 2010);

        Assert.Single(lines);
        Assert.Equal(333.333m, lines[0].DepreciationAmount);
    }

    [Fact]
    public void GenerateSchedule_Days360_FullSchedule_ShouldAlwaysSumToBase()
    {
        var asset = CreateAsset(4_000m, 20m, new DateTime(2010, 8, 1), new DateTime(2010, 8, 1));
        var lines = _engine.GenerateSchedule(asset);

        Assert.Equal(4_000m, lines.Sum(l => l.DepreciationAmount));
        Assert.Equal(0m, lines[^1].ClosingNbv);
    }

    [Fact]
    public void GenerateSchedule_DisposalSameYearAsInService_ShouldProrateBetweenDates()
    {
        // Mise en service 01/03, cession 30/06 même année → 120 jours/360 → 4500 × 120/360 = 1500.
        var asset = CreateAsset(
            30_000m,
            15m,
            new DateTime(2026, 1, 10),
            new DateTime(2026, 3, 1),
            disposal: new DateTime(2026, 6, 30));

        var lines = _engine.GenerateSchedule(asset);

        Assert.Single(lines);
        Assert.Equal(1_500m, lines[0].DepreciationAmount);
    }

    // ------------------------------------------------------------------
    // Dotation prorata de l'année de cession (T4, B1) — CalculateDisposalYearDepreciation
    // ------------------------------------------------------------------

    [Fact]
    public void CalculateDisposalYearDepreciation_MidYear_ShouldProrate30_360()
    {
        // 40 000 × 20 % = 8 000/an ; 2026-2027 pleins (16 000) ; cession 30/06/2028 → 180 j/360 → 4 000.
        var asset = CreateAsset(
            40_000m,
            20m,
            new DateTime(2026, 1, 1),
            new DateTime(2026, 1, 1),
            disposal: new DateTime(2028, 6, 30));

        var amount = _engine.CalculateDisposalYearDepreciation(asset, fiscalYear: 2028, priorAccumulatedDepreciation: 16_000m);

        Assert.Equal(4_000m, amount);
    }

    [Fact]
    public void CalculateDisposalYearDepreciation_SameYearAsInService_ShouldProrateBetweenDates()
    {
        // Mise en service 01/03, cession 30/06 même année → 120 j/360 → 4 500 × 120/360 = 1 500.
        var asset = CreateAsset(
            30_000m,
            15m,
            new DateTime(2026, 1, 10),
            new DateTime(2026, 3, 1),
            disposal: new DateTime(2026, 6, 30));

        var amount = _engine.CalculateDisposalYearDepreciation(asset, fiscalYear: 2026, priorAccumulatedDepreciation: 0m);

        Assert.Equal(1_500m, amount);
    }

    [Fact]
    public void CalculateDisposalYearDepreciation_Accelerated_ShouldProrateEffectiveRate()
    {
        // 40 000, 15 % × 1,5 = 22,5 % → 9 000/an ; 2026 plein (9 000) ; cession 30/06/2027 → 4 500.
        var asset = CreateAsset(
            40_000m,
            15m,
            new DateTime(2026, 1, 1),
            new DateTime(2026, 1, 1),
            disposal: new DateTime(2027, 6, 30),
            method: DepreciationMethod.Accelerated,
            coefficient: 1.5m);

        var amount = _engine.CalculateDisposalYearDepreciation(asset, fiscalYear: 2027, priorAccumulatedDepreciation: 9_000m);

        Assert.Equal(4_500m, amount);
    }

    [Fact]
    public void CalculateDisposalYearDepreciation_December31_ShouldBeFullAnnuity()
    {
        // Cession au 31/12 → 360 j/360 → annuité pleine (8 000).
        var asset = CreateAsset(
            40_000m,
            20m,
            new DateTime(2026, 1, 1),
            new DateTime(2026, 1, 1),
            disposal: new DateTime(2028, 12, 31));

        var amount = _engine.CalculateDisposalYearDepreciation(asset, fiscalYear: 2028, priorAccumulatedDepreciation: 16_000m);

        Assert.Equal(8_000m, amount);
    }

    [Fact]
    public void CalculateDisposalYearDepreciation_Integral_ShouldReturnFullBase()
    {
        // Bien de faible valeur : dotation unique dès l'année de mise en service (sans prorata).
        var asset = CreateAsset(
            150m,
            100m,
            new DateTime(2026, 5, 10),
            new DateTime(2026, 9, 15),
            disposal: new DateTime(2026, 12, 31),
            method: DepreciationMethod.Integral);

        var amount = _engine.CalculateDisposalYearDepreciation(asset, fiscalYear: 2026, priorAccumulatedDepreciation: 0m);

        Assert.Equal(150m, amount);
    }

    [Fact]
    public void CalculateDisposalYearDepreciation_NearlyFullyDepreciated_ShouldClampToRemainingBase()
    {
        // Base 10 000, 20 % ; 4 ans pleins (8 000) ; cession mi-année 5 → prorata 1 000 mais il ne
        // reste que 2 000 de base → clamé à 2 000 (sécurité anti-dépassement).
        var asset = CreateAsset(
            10_000m,
            20m,
            new DateTime(2026, 1, 1),
            new DateTime(2026, 1, 1),
            disposal: new DateTime(2030, 6, 30));

        var amount = _engine.CalculateDisposalYearDepreciation(asset, fiscalYear: 2030, priorAccumulatedDepreciation: 8_000m);

        Assert.True(amount <= 2_000m);
        Assert.True(amount > 0m);
    }

    // ------------------------------------------------------------------
    // Amortissement accéléré (Décret 2008-492 art. 2)
    // ------------------------------------------------------------------

    [Fact]
    public void GenerateSchedule_Accelerated_Coefficient15_ShouldProduceConstantAnnuities()
    {
        // Exemple officiel n°1 : 40 000 TND, taux linéaire 15 %, coefficient 1,5 → 22,5 % → 9 000/an.
        var asset = CreateAsset(
            40_000m,
            15m,
            new DateTime(2026, 1, 1),
            new DateTime(2026, 1, 1),
            method: DepreciationMethod.Accelerated,
            coefficient: 1.5m);

        var lines = _engine.GenerateSchedule(asset);

        Assert.True(lines.Count >= 4);
        Assert.Equal(9_000m, lines[0].DepreciationAmount);
        Assert.Equal(9_000m, lines[1].DepreciationAmount);
        Assert.Equal(9_000m, lines[2].DepreciationAmount);
        Assert.Equal(40_000m, lines.Sum(l => l.DepreciationAmount));
        Assert.Equal(0m, lines[^1].ClosingNbv);
    }

    [Fact]
    public void GenerateSchedule_Accelerated_Coefficient2_WithProrata_ShouldSumToBase()
    {
        // 10 000, taux 20 %, coefficient 2 → taux effectif 40 % → annuité 4 000 ; 1er juillet → prorata 180/360.
        var asset = CreateAsset(
            10_000m,
            20m,
            new DateTime(2026, 1, 1),
            new DateTime(2026, 7, 1),
            method: DepreciationMethod.Accelerated,
            coefficient: 2m);

        var lines = _engine.GenerateSchedule(asset);

        Assert.True(lines.Count >= 2);
        Assert.Equal(2_000m, lines[0].DepreciationAmount);
        Assert.Equal(10_000m, lines.Sum(l => l.DepreciationAmount));
        Assert.Equal(0m, lines[^1].ClosingNbv);
    }

    // ------------------------------------------------------------------
    // Amortissement intégral
    // ------------------------------------------------------------------

    [Fact]
    public void GenerateSchedule_Integral_LowValueAsset_ShouldDepreciateEverythingFirstYear()
    {
        // Bien de faible valeur ≤ 200 DT (Décret 2008-492 art. 4).
        var asset = CreateAsset(
            150m,
            100m,
            new DateTime(2026, 5, 10),
            new DateTime(2026, 9, 15),
            method: DepreciationMethod.Integral);

        var lines = _engine.GenerateSchedule(asset);

        Assert.Single(lines);
        Assert.Equal(2026, lines[0].FiscalYear);
        Assert.Equal(150m, lines[0].DepreciationAmount);
        Assert.Equal(0m, lines[0].ClosingNbv);
    }

    [Fact]
    public void GenerateSchedule_Integral_ShouldDepreciateEverythingFirstYear()
    {
        var asset = CreateAsset(
            450m,
            100m,
            new DateTime(2026, 5, 10),
            new DateTime(2026, 9, 15),
            method: DepreciationMethod.Integral);

        var lines = _engine.GenerateSchedule(asset);

        Assert.Single(lines);
        Assert.Equal(2026, lines[0].FiscalYear);
        Assert.Equal(450m, lines[0].DepreciationAmount);
        Assert.Equal(0m, lines[0].ClosingNbv);
    }

    // ------------------------------------------------------------------
    // Convention 30/360
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(2026, 1, 1, 360)]   // 1er janvier → année complète
    [InlineData(2026, 3, 1, 300)]   // 1er mars → 10 mois
    [InlineData(2026, 10, 1, 90)]   // 1er octobre → 3 mois
    [InlineData(2026, 12, 31, 1)]   // 31 décembre (jour plafonné à 30) → 1 jour
    public void Days360RemainingInYear_ShouldFollowConvention(int y, int m, int d, int expected)
    {
        Assert.Equal(expected, DepreciationEngine.Days360RemainingInYear(new DateTime(y, m, d)));
    }

    [Fact]
    public void Days360Between_SameYearDates_ShouldCountInclusive()
    {
        Assert.Equal(120m, DepreciationEngine.Days360Between(new DateTime(2026, 3, 1), new DateTime(2026, 6, 30)));
    }
}