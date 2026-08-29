using FactuTrust.Application.Common.Fiscal;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Services;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Golden tests du moteur d'amortissement pour les exercices décalés (plan « Exercices décalés »,
/// P2) : (1) parité stricte janvier — <c>fiscalYearStartMonth=1</c> produit les mêmes résultats que
/// la norme (bit-à-bit identique à l'existant) ; (2) cas décalés (linéaire/accéléré/intégral,
/// première année, cession en exercice décalé, mise en service + cession même exercice).
/// </summary>
public sealed class DepreciationEngineOffsetFiscalYearTests
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
            "68113",
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

    // ------------------------------------------------------------------
    // Parité stricte janvier (startMonth = 1 explicite) — golden tests
    // ------------------------------------------------------------------

    [Fact]
    public void Parity_CivilStart_OfficialExample_ShouldMatchNormTable()
    {
        // Exemple officiel : ordinateur 4000 TND, 5 ans, 20 %, mise en service 01/10
        // → 200 / 800 / 800 / 800 / 800 / 600, total = 4000 (identique à DepreciationEngineTests).
        var asset = CreateAsset(4_000m, 20m, new DateTime(2010, 8, 1), new DateTime(2010, 10, 1));
        var lines = _engine.GenerateSchedule(asset, fiscalYearStartMonth: 1);

        Assert.Equal(6, lines.Count);
        Assert.Equal(200m, lines[0].DepreciationAmount);
        Assert.Equal(800m, lines[1].DepreciationAmount);
        Assert.Equal(800m, lines[2].DepreciationAmount);
        Assert.Equal(800m, lines[3].DepreciationAmount);
        Assert.Equal(800m, lines[4].DepreciationAmount);
        Assert.Equal(600m, lines[^1].DepreciationAmount);
        Assert.Equal(4_000m, lines[^1].AccumulatedDepreciation);
        Assert.Equal(0m, lines[^1].ClosingNbv);
        Assert.Equal(4_000m, lines.Sum(l => l.DepreciationAmount));
    }

    [Fact]
    public void Parity_CivilStart_DisposalProrata_ShouldMatchExisting()
    {
        // 40 000 × 20 % = 8 000/an ; cession 30/06/2028 → 180 j/360 → 4 000 (identique à l'existant).
        var asset = CreateAsset(
            40_000m, 20m, new DateTime(2026, 1, 1), new DateTime(2026, 1, 1),
            disposal: new DateTime(2028, 6, 30));

        var amount = _engine.CalculateDisposalYearDepreciation(
            asset, fiscalYear: 2028, priorAccumulatedDepreciation: 16_000m, fiscalYearStartMonth: 1);

        Assert.Equal(4_000m, amount);
    }

    [Fact]
    public void Parity_CivilStart_FirstDayOfYear_ShouldBeFullAnnuity()
    {
        // Mise en service le 1er jour de l'exercice civil → annuité pleine (360 j/360).
        var asset = CreateAsset(10_000m, 20m, new DateTime(2026, 1, 1), new DateTime(2026, 1, 1));
        var lines = _engine.GenerateSchedule(asset, throughFiscalYear: 2026, fiscalYearStartMonth: 1);

        Assert.Single(lines);
        Assert.Equal(2_000m, lines[0].DepreciationAmount);
    }

    // ------------------------------------------------------------------
    // Cas décalés — exercice juillet→juin (startMonth = 7)
    // ------------------------------------------------------------------

    [Fact]
    public void Offset_Linear_MidYearInService_ShouldProrateToFiscalYearEnd()
    {
        // Exercice juil.→juin. Mise en service 15/08/2026 (exercice 2026 = juil. 2026→juin 2027).
        // 10 000, 20 % → 2 000/an. Prorata du 15/08 au 30/06 suivant : 316 j/360 → 1 755,556.
        var asset = CreateAsset(10_000m, 20m, new DateTime(2026, 1, 10), new DateTime(2026, 8, 15));
        var lines = _engine.GenerateSchedule(asset, fiscalYearStartMonth: 7);

        Assert.Equal(6, lines.Count);
        Assert.Equal(2026, lines[0].FiscalYear);
        Assert.Equal(1_755.556m, lines[0].DepreciationAmount);
        Assert.Equal(2_000m, lines[1].DepreciationAmount);
        Assert.Equal(2031, lines[^1].FiscalYear);
        Assert.Equal(10_000m, lines[^1].AccumulatedDepreciation);
        Assert.Equal(0m, lines[^1].ClosingNbv);
        Assert.Equal(10_000m, lines.Sum(l => l.DepreciationAmount));
    }

    [Fact]
    public void Offset_Linear_FirstDayOfFiscalYear_ShouldBeFullAnnuity()
    {
        // Mise en service le 1er jour de l'exercice décalé (01/07) → annuité pleine (360 j/360).
        var asset = CreateAsset(10_000m, 20m, new DateTime(2026, 1, 10), new DateTime(2026, 7, 1));
        var lines = _engine.GenerateSchedule(asset, fiscalYearStartMonth: 7);

        Assert.Equal(5, lines.Count);
        Assert.Equal(2026, lines[0].FiscalYear);
        Assert.Equal(2_000m, lines[0].DepreciationAmount);
        Assert.Equal(10_000m, lines.Sum(l => l.DepreciationAmount));
        Assert.Equal(0m, lines[^1].ClosingNbv);
    }

    [Fact]
    public void Offset_Accelerated_MidYearInService_ShouldProrateEffectiveRate()
    {
        // 40 000, 15 % × 1,5 = 22,5 % → 9 000/an. Mise en service 01/10/2026 (exercice 2026).
        // Prorata du 01/10 au 30/06 : 270 j/360 → 6 750.
        var asset = CreateAsset(
            40_000m, 15m, new DateTime(2026, 1, 10), new DateTime(2026, 10, 1),
            method: DepreciationMethod.Accelerated, coefficient: 1.5m);
        var lines = _engine.GenerateSchedule(asset, fiscalYearStartMonth: 7);

        Assert.Equal(5, lines.Count);
        Assert.Equal(2026, lines[0].FiscalYear);
        Assert.Equal(6_750m, lines[0].DepreciationAmount);
        Assert.Equal(9_000m, lines[1].DepreciationAmount);
        Assert.Equal(40_000m, lines.Sum(l => l.DepreciationAmount));
        Assert.Equal(0m, lines[^1].ClosingNbv);
    }

    [Fact]
    public void Offset_Integral_LowValueAsset_ShouldDepreciateInInServiceFiscalYear()
    {
        // Bien de faible valeur : dotation unique dès l'exercice de mise en service (sans prorata).
        // Mise en service 15/09/2026 → exercice 2026 (juil. 2026→juin 2027).
        var asset = CreateAsset(
            150m, 100m, new DateTime(2026, 5, 10), new DateTime(2026, 9, 15),
            method: DepreciationMethod.Integral);
        var lines = _engine.GenerateSchedule(asset, fiscalYearStartMonth: 7);

        Assert.Single(lines);
        Assert.Equal(2026, lines[0].FiscalYear);
        Assert.Equal(150m, lines[0].DepreciationAmount);
        Assert.Equal(0m, lines[0].ClosingNbv);
    }

    [Fact]
    public void Offset_Integral_InServiceInSecondCalendarYear_ShouldUseCorrectFiscalKey()
    {
        // Mise en service 15/02/2027 (février) → exercice 2026 (juil. 2026→juin 2027), clé 2026.
        var asset = CreateAsset(
            150m, 100m, new DateTime(2026, 5, 10), new DateTime(2027, 2, 15),
            method: DepreciationMethod.Integral);
        var lines = _engine.GenerateSchedule(asset, fiscalYearStartMonth: 7);

        Assert.Single(lines);
        Assert.Equal(2026, lines[0].FiscalYear);
        Assert.Equal(150m, lines[0].DepreciationAmount);
    }

    [Fact]
    public void Offset_Disposal_MidFiscalYear_ShouldProrateFromFiscalYearStart()
    {
        // 40 000, 20 % → 8 000/an. Exercice juil.→juin. Mise en service 01/07/2026 (plein 2026).
        // Cession 31/12/2027 → exercice 2027 (juil. 2027→juin 2028) ; prorata depuis le 01/07/2027
        // jusqu'au 31/12/2027 : 180 j/360 → 4 000.
        var asset = CreateAsset(
            40_000m, 20m, new DateTime(2026, 1, 1), new DateTime(2026, 7, 1),
            disposal: new DateTime(2027, 12, 31));
        var lines = _engine.GenerateSchedule(asset, fiscalYearStartMonth: 7);

        Assert.Equal(2, lines.Count);
        Assert.Equal(2026, lines[0].FiscalYear);
        Assert.Equal(8_000m, lines[0].DepreciationAmount);
        Assert.Equal(2027, lines[1].FiscalYear);
        Assert.Equal(4_000m, lines[1].DepreciationAmount);
        Assert.Equal(12_000m, lines.Sum(l => l.DepreciationAmount));
    }

    [Fact]
    public void Offset_Disposal_SameFiscalYearAsInService_ShouldProrateBetweenDates()
    {
        // Mise en service 01/09/2026, cession 31/12/2026 — même exercice 2026 (juil. 2026→juin 2027).
        // 30 000, 15 % → 4 500/an ; du 01/09 au 31/12 : 120 j/360 → 1 500.
        var asset = CreateAsset(
            30_000m, 15m, new DateTime(2026, 1, 10), new DateTime(2026, 9, 1),
            disposal: new DateTime(2026, 12, 31));
        var lines = _engine.GenerateSchedule(asset, fiscalYearStartMonth: 7);

        Assert.Single(lines);
        Assert.Equal(2026, lines[0].FiscalYear);
        Assert.Equal(1_500m, lines[0].DepreciationAmount);
    }

    [Fact]
    public void Offset_CalculateDisposalYearDepreciation_MidFiscalYear_ShouldProrate30_360()
    {
        // 40 000, 20 % → 8 000/an. Exercice 2026 plein (8 000) ; cession 31/12/2027 → exercice 2027,
        // prorata 180 j/360 → 4 000.
        var asset = CreateAsset(
            40_000m, 20m, new DateTime(2026, 1, 1), new DateTime(2026, 7, 1),
            disposal: new DateTime(2027, 12, 31));

        var amount = _engine.CalculateDisposalYearDepreciation(
            asset, fiscalYear: 2027, priorAccumulatedDepreciation: 8_000m, fiscalYearStartMonth: 7);

        Assert.Equal(4_000m, amount);
    }

    // ------------------------------------------------------------------
    // Cohérence clé d'exercice (FiscalYearMath) — garde anti-incohérence
    // ------------------------------------------------------------------

    [Fact]
    public void Offset_Linear_FiscalYearKeys_ShouldBeContiguousIntegers()
    {
        var asset = CreateAsset(10_000m, 20m, new DateTime(2026, 1, 10), new DateTime(2026, 8, 15));
        var lines = _engine.GenerateSchedule(asset, fiscalYearStartMonth: 7);

        // Les clés d'exercice forment une suite d'entiers contigus à partir de l'exercice de mise
        // en service (année de début d'exercice).
        var keys = lines.Select(l => l.FiscalYear).ToList();
        Assert.Equal(2026, keys[0]);
        for (var i = 1; i < keys.Count; i++)
            Assert.Equal(keys[i - 1] + 1, keys[i]);
    }

    [Fact]
    public void Offset_Linear_InServiceBeforeFiscalStartMonth_BelongsToPreviousFiscalKey()
    {
        // Mise en service 15/02/2026 (février, avant juillet) → exercice 2025 (juil. 2025→juin 2026).
        var asset = CreateAsset(10_000m, 20m, new DateTime(2025, 6, 1), new DateTime(2026, 2, 15));
        var lines = _engine.GenerateSchedule(asset, throughFiscalYear: 2025, fiscalYearStartMonth: 7);

        Assert.Single(lines);
        Assert.Equal(2025, lines[0].FiscalYear);
        // Prorata du 15/02/2026 au 30/06/2026 : Days360FromFiscalYearStart(15/02, 7) = (2+12-7)=7 → 7*30+15=225
        // → restant = 360-225+1 = 136 ; 2 000 × 136/360 = 755,556.
        Assert.Equal(755.556m, lines[0].DepreciationAmount);
    }
}
