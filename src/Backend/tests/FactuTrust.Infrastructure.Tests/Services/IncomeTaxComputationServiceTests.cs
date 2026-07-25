using FactuTrust.Application.Common.Fiscal;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class IncomeTaxComputationServiceTests
{
    private static IReadOnlyList<IrppBracket> DefaultIrpp() => new List<IrppBracket>
    {
        new(0, 0), new(5000, 26), new(20000, 28), new(30000, 32), new(50000, 35)
    };

    private static IncomeTaxComputationInput Input(
        TaxpayerKind kind = TaxpayerKind.CorporateIS,
        decimal accountingResult = 0,
        decimal reint = 0,
        decimal deduc = 0,
        decimal deficitImputed = 0,
        decimal deferredImputed = 0,
        decimal isRate = 0.15m,
        decimal caTtc = 0,
        decimal minTaxRate = 0.002m,
        decimal minTaxFloor = 500m,
        bool cssApplies = true,
        decimal cssRate = 0m,
        decimal cssFloor = 0m,
        decimal acomptes = 0,
        decimal rs = 0,
        decimal priorCredit = 0,
        MinimumTaxRegime regime = MinimumTaxRegime.Standard,
        decimal minTaxReducedRate = 0.001m,
        decimal minTaxFloorReduced = 300m,
        bool roundToDinar = false) =>
        new(kind, accountingResult, reint, deduc, deficitImputed, deferredImputed, isRate, caTtc,
            minTaxRate, minTaxFloor, cssApplies, cssRate, cssFloor, DefaultIrpp(), acomptes, rs, priorCredit,
            regime, minTaxReducedRate, minTaxFloorReduced, roundToDinar);

    [Fact]
    public void Is_Profit_ComputesTaxAtRate()
    {
        var c = IncomeTaxComputationService.Compute(Input(accountingResult: 100000, reint: 15000, caTtc: 500000));

        Assert.Equal(115000m, c.ResultBeforeCarryForward);
        Assert.Equal(115000m, c.TaxableResult);
        Assert.Equal(17250m, c.TaxOnResult);          // 115000 * 15%
        Assert.Equal(1000m, c.MinimumTax);            // max(0.2% * 500000, 500) = 1000
        Assert.Equal(17250m, c.TaxDue);               // max(IS, minimum)
        Assert.Equal(17250m, c.NetToPay);
        Assert.Equal(0m, c.CreditToCarry);
    }

    [Fact]
    public void Is_Deficit_MinimumTaxApplies_AndDeficitCarried()
    {
        var c = IncomeTaxComputationService.Compute(Input(accountingResult: -5000, caTtc: 200000));

        Assert.Equal(0m, c.TaxableResult);
        Assert.Equal(5000m, c.DeficitGeneratedThisYear);
        Assert.Equal(0m, c.TaxOnResult);
        Assert.Equal(500m, c.MinimumTax);             // max(0.2% * 200000 = 400, 500) = 500
        Assert.Equal(500m, c.TaxDue);
        Assert.Equal(500m, c.NetToPay);
    }

    [Fact]
    public void Is_DeficitImputation_ReducesTaxableResult()
    {
        var c = IncomeTaxComputationService.Compute(Input(accountingResult: 50000, deficitImputed: 20000));

        Assert.Equal(20000m, c.DeficitsImputed);
        Assert.Equal(30000m, c.TaxableResult);
        Assert.Equal(4500m, c.TaxOnResult);           // 30000 * 15%
    }

    [Fact]
    public void Irpp_Bic_ComputesProgressiveScale()
    {
        // taxable 25000 : 0(0-5000) + 3900(15000*26%) + 1400(5000*28%) = 5300
        var c = IncomeTaxComputationService.Compute(Input(kind: TaxpayerKind.IndividualIrppBic, accountingResult: 25000));
        Assert.Equal(25000m, c.TaxableResult);
        Assert.Equal(5300m, c.TaxOnResult);
    }

    [Fact]
    public void Credits_ExceedingTax_ProduceCreditToCarry()
    {
        var c = IncomeTaxComputationService.Compute(Input(accountingResult: 4000, isRate: 0.15m, acomptes: 1500));
        // taxable 4000 → IS 600 ; minimum 500 ; taxDue 600 ; credits 1500 → crédit 900
        Assert.Equal(600m, c.TaxDue);
        Assert.Equal(0m, c.NetToPay);
        Assert.Equal(900m, c.CreditToCarry);
    }

    [Fact]
    public void Css_WhenConfigured_IsAddedToTotal()
    {
        var c = IncomeTaxComputationService.Compute(Input(accountingResult: 100000, cssApplies: true, cssRate: 0.01m));
        Assert.Equal(1000m, c.Css);                   // 1% * 100000
        Assert.Equal(c.TaxDue + 1000m, c.TotalTaxDue);
    }

    // ── Correctifs fiscaux : régimes de minimum d'impôt, arrondi de l'assiette ────────────────

    [Fact]
    public void MinimumTax_ReducedRegime_UsesReducedRateAndFloor()
    {
        // Régime réduit : max(0,1 % × 200 000 = 200 ; plancher réduit 300) = 300 (au lieu de 500).
        var c = IncomeTaxComputationService.Compute(Input(
            accountingResult: -1000, caTtc: 200000, regime: MinimumTaxRegime.Reduced));

        Assert.Equal(300m, c.MinimumTax);
        Assert.Equal(300m, c.TaxDue);
        Assert.Equal((int)MinimumTaxRegime.Reduced, c.MinimumTaxRegime);
    }

    [Fact]
    public void MinimumTax_ReducedRegime_AppliesRateWhenAboveFloor()
    {
        // 0,1 % × 1 000 000 = 1 000 > plancher réduit 300.
        var c = IncomeTaxComputationService.Compute(Input(
            accountingResult: -1000, caTtc: 1000000, regime: MinimumTaxRegime.Reduced));

        Assert.Equal(1000m, c.MinimumTax);
    }

    [Fact]
    public void MinimumTax_ExemptRegime_IsZero_EvenWithTurnoverAndDeficit()
    {
        // Société nouvellement créée / ZDR / totalement exportatrice : aucun minimum d'impôt.
        var c = IncomeTaxComputationService.Compute(Input(
            accountingResult: -5000, caTtc: 2000000, regime: MinimumTaxRegime.Exempt));

        Assert.Equal(0m, c.MinimumTax);
        Assert.Equal(0m, c.TaxDue);
        Assert.Equal(0m, c.NetToPay);
        Assert.Equal(5000m, c.DeficitGeneratedThisYear);
    }

    [Fact]
    public void MinimumTax_StandardRegime_UsesTurnoverRate_NotOnlyFloor()
    {
        // Correctif : sans CA renseigné le minimum restait bloqué au plancher (500).
        var c = IncomeTaxComputationService.Compute(Input(accountingResult: -1000, caTtc: 2000000));

        Assert.Equal(4000m, c.MinimumTax); // 0,2 % × 2 000 000
        Assert.Equal(4000m, c.TaxDue);
    }

    [Fact]
    public void TaxableResult_RoundedDownToDinar_WhenEnabled()
    {
        // Assiette 10 000,750 → 10 000 (arrondi au dinar inférieur) → IS = 1 500.
        var c = IncomeTaxComputationService.Compute(Input(
            accountingResult: 10000.750m, isRate: 0.15m, roundToDinar: true));

        Assert.Equal(10000m, c.TaxableResult);
        Assert.Equal(1500m, c.TaxOnResult);
    }

    [Fact]
    public void TaxableResult_NotRounded_WhenDisabled_PreservesLegacyBehaviour()
    {
        var c = IncomeTaxComputationService.Compute(Input(
            accountingResult: 10000.750m, isRate: 0.15m, roundToDinar: false));

        Assert.Equal(10000.750m, c.TaxableResult);
        Assert.Equal(1500.113m, c.TaxOnResult); // 10 000,750 × 15 % arrondi au millime
    }

    [Fact]
    public void Css_AtOnePercent_IsComputedOnTaxableResult()
    {
        // Défaut de référence : CSS 1 % de l'assiette (le défaut nul sous-évaluait l'impôt).
        var c = IncomeTaxComputationService.Compute(Input(
            accountingResult: 31330, reint: 400, cssApplies: true, cssRate: 0.01m));

        Assert.Equal(31730m, c.TaxableResult);
        Assert.Equal(317.300m, c.Css);
        Assert.Equal(c.TaxDue + 317.300m, c.TotalTaxDue);
    }

    [Fact]
    public void Deductions_ReduceResult()
    {
        var c = IncomeTaxComputationService.Compute(Input(accountingResult: 100000, reint: 10000, deduc: 40000));
        Assert.Equal(70000m, c.ResultBeforeCarryForward);
        Assert.Equal(70000m, c.TaxableResult);
    }
}
