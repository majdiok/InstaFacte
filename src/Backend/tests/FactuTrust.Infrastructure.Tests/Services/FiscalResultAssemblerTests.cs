using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Fiscal;
using FactuTrust.Domain.Entities.Fiscal;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Contrôles fiscaux appliqués en amont du moteur : imputation des reports (prescription des déficits,
/// plafonnement au stock disponible) et estimation du chiffre d'affaires local TTC.
/// </summary>
public sealed class FiscalResultAssemblerTests
{
    private const int Year = 2026;

    private static IncomeTaxYearParameter Params() => IncomeTaxYearParameterDefaults.Create(Year);

    private static FiscalResultDeclaration Declaration(
        decimal accountingNetResult, params FiscalCarryForwardItem[] carryForwards)
    {
        var d = FiscalResultDeclaration.Create(Year, TaxpayerKind.CorporateIS);
        d.UpdateInputs(TaxpayerKind.CorporateIS, accountingNetResult, 0.15m, 0m, 0m, 0m, 0m);
        d.ReplaceCarryForwards(carryForwards);
        return d;
    }

    // ── Estimation du CA local TTC (assiette du minimum d'impôt) ─────────────────────────────

    [Fact]
    public void EstimateLocalTurnoverTtc_SumsRevenueAndCollectedVat()
    {
        var rows = new List<BalanceRowDto>
        {
            new() { AccountNumber = "707000", Label = "Ventes", MovementCredit = 1_000_000m },
            new() { AccountNumber = "436710", Label = "TVA collectée", MovementCredit = 190_000m },
            new() { AccountNumber = "607000", Label = "Achats", MovementDebit = 400_000m }, // ignoré
            new() { AccountNumber = "411000", Label = "Clients", MovementDebit = 900_000m }  // ignoré
        };

        Assert.Equal(1_190_000m, FiscalResultAssembler.EstimateLocalTurnoverTtc(rows));
    }

    [Fact]
    public void EstimateLocalTurnoverTtc_NeverNegative()
    {
        var rows = new List<BalanceRowDto>
        {
            new() { AccountNumber = "707000", Label = "Ventes", MovementDebit = 500m }
        };

        Assert.Equal(0m, FiscalResultAssembler.EstimateLocalTurnoverTtc(rows));
    }

    [Fact]
    public void AssembleNew_UsesSuggestedTurnover_SoMinimumTaxIsNotStuckAtFloor()
    {
        // Correctif : l'aperçu initial forçait le CA à 0 → minimum d'impôt bloqué au plancher (500).
        var dto = FiscalResultAssembler.AssembleNew(
            Year, accountingNetResult: -1_000m, suggestions: Array.Empty<FiscalAdjustmentLineDto>(),
            p: Params(), enabled: true, suggestedLocalTurnoverTtc: 2_000_000m);

        Assert.Equal(2_000_000m, dto.LocalTurnoverTtc);
        Assert.Equal(2_000_000m, dto.SuggestedLocalTurnoverTtc);
        Assert.Equal(4_000m, dto.Computation.MinimumTax); // 0,2 % × 2 000 000
    }

    // ── Prescription et plafonnement des reports ─────────────────────────────────────────────

    [Fact]
    public void ExpiredDeficit_IsNotImputed_AndRaisesWarning()
    {
        // Déficit 2018, imputable jusqu'à 2023 : prescrit sur l'exercice 2026.
        var expired = FiscalCarryForwardItem.Create(FiscalCarryForwardKind.Deficit, 2018, 10_000m, 10_000m, 2023);
        var dto = FiscalResultAssembler.Assemble(Declaration(50_000m, expired), Params(), enabled: true);

        Assert.Equal(0m, dto.Computation.DeficitsImputed);
        Assert.Equal(50_000m, dto.Computation.TaxableResult);
        Assert.Contains(dto.Warnings, w => w.Contains("prescrit"));
    }

    [Fact]
    public void NonExpiredDeficit_IsImputed()
    {
        var valid = FiscalCarryForwardItem.Create(FiscalCarryForwardKind.Deficit, 2023, 10_000m, 10_000m, 2028);
        var dto = FiscalResultAssembler.Assemble(Declaration(50_000m, valid), Params(), enabled: true);

        Assert.Equal(10_000m, dto.Computation.DeficitsImputed);
        Assert.Equal(40_000m, dto.Computation.TaxableResult);
        Assert.Empty(dto.Warnings);
    }

    [Fact]
    public void DeficitExpiry_IsDerivedFromOriginYear_WhenNotProvided()
    {
        // Report de 5 exercices : un déficit 2019 est prescrit au-delà de 2024.
        var item = FiscalCarryForwardItem.Create(FiscalCarryForwardKind.Deficit, 2019, 8_000m, 8_000m, expiryYear: null);

        Assert.Equal(2024, item.ExpiryYear);

        var dto = FiscalResultAssembler.Assemble(Declaration(50_000m, item), Params(), enabled: true);
        Assert.Equal(0m, dto.Computation.DeficitsImputed);
        Assert.Contains(dto.Warnings, w => w.Contains("prescrit"));
    }

    [Fact]
    public void DeferredDepreciation_IsNeverTimeBarred()
    {
        // Les amortissements réputés différés sont reportables sans limite de durée.
        var old = FiscalCarryForwardItem.Create(FiscalCarryForwardKind.DeferredDepreciation, 2010, 6_000m, 6_000m, expiryYear: null);

        Assert.Null(old.ExpiryYear);

        var dto = FiscalResultAssembler.Assemble(Declaration(50_000m, old), Params(), enabled: true);
        Assert.Equal(6_000m, dto.Computation.DeferredDepreciationImputed);
        Assert.Equal(44_000m, dto.Computation.TaxableResult);
    }

    [Fact]
    public void Imputation_IsCappedAtAvailableStock()
    {
        // L'entité borne déjà l'imputation au stock reportable disponible.
        var item = FiscalCarryForwardItem.Create(FiscalCarryForwardKind.Deficit, 2024, 5_000m, 9_999m, 2029);

        Assert.Equal(5_000m, item.ImputedThisYear);

        var dto = FiscalResultAssembler.Assemble(Declaration(50_000m, item), Params(), enabled: true);
        Assert.Equal(5_000m, dto.Computation.DeficitsImputed);
        Assert.Equal(45_000m, dto.Computation.TaxableResult);
    }

    // ── Paramètres de référence appliqués via les défauts d'exercice ─────────────────────────

    [Fact]
    public void DefaultParameters_ApplyCssAtOnePercent()
    {
        // Le défaut historique (CSS 0 %) sous-évaluait l'impôt total.
        var dto = FiscalResultAssembler.Assemble(Declaration(31_330m), Params(), enabled: true);

        Assert.Equal(31_330m, dto.Computation.TaxableResult);
        Assert.Equal(313.300m, dto.Computation.Css);
    }

    // ── T14 — batterie de calcul de l'impôt (planchers, CSS, arrondi, barème IRPP, crédits) ──

    /// <summary>Feuille brouillon avec toutes les entrées éditables (taux IS 15 % par défaut).</summary>
    private static FiscalResultDeclaration Sheet(
        decimal accountingNetResult,
        decimal localTurnoverTtc = 0m,
        decimal acomptesPaid = 0m,
        decimal withholdingSuffered = 0m,
        decimal priorTaxCredit = 0m,
        TaxpayerKind kind = TaxpayerKind.CorporateIS,
        MinimumTaxRegime minimumTaxRegime = MinimumTaxRegime.Standard,
        params FiscalCarryForwardItem[] carryForwards)
    {
        var d = FiscalResultDeclaration.Create(Year, kind);
        d.UpdateInputs(kind, accountingNetResult, 0.15m, localTurnoverTtc,
            acomptesPaid, withholdingSuffered, priorTaxCredit, minimumTaxRegime);
        d.ReplaceCarryForwards(carryForwards);
        return d;
    }

    private static FiscalCarryForwardItem Deficit(int originYear, decimal initial, decimal imputed, int? expiry = null) =>
        FiscalCarryForwardItem.Create(FiscalCarryForwardKind.Deficit, originYear, initial, imputed, expiry);

    private static FiscalCarryForwardItem Deferred(int originYear, decimal initial, decimal imputed) =>
        FiscalCarryForwardItem.Create(FiscalCarryForwardKind.DeferredDepreciation, originYear, initial, imputed, null);

    // — Planchers du minimum d'impôt (500 droit commun / 300 réduit) —

    [Fact]
    public void MinimumTax_StandardFloor500_BindsWhenTaxOnResultBelowFloor()
    {
        // Résultat 1 000 → IS 150 ; CA 100 000 → 0,2 % = 200 < plancher 500 → minimum = 500.
        var dto = FiscalResultAssembler.Assemble(Sheet(1_000m, localTurnoverTtc: 100_000m), Params(), enabled: true);

        Assert.Equal(150m, dto.Computation.TaxOnResult);
        Assert.Equal(500m, dto.Computation.MinimumTax);
        Assert.Equal(500m, dto.Computation.TaxDue);            // taxDue = max(150, 500) = 500
        Assert.Equal((int)MinimumTaxRegime.Standard, dto.Computation.MinimumTaxRegime);
    }

    [Fact]
    public void MinimumTax_ReducedFloor300_BindsWhenTaxOnResultBelowFloor()
    {
        // Régime réduit : 0,1 % × 100 000 = 100 < plancher 300 → minimum = 300 (> IS 150).
        var dto = FiscalResultAssembler.Assemble(
            Sheet(1_000m, localTurnoverTtc: 100_000m, minimumTaxRegime: MinimumTaxRegime.Reduced), Params(), enabled: true);

        Assert.Equal(300m, dto.Computation.MinimumTax);
        Assert.Equal(300m, dto.Computation.TaxDue);            // taxDue = max(150, 300) = 300
        Assert.Equal((int)MinimumTaxRegime.Reduced, dto.Computation.MinimumTaxRegime);
    }

    [Fact]
    public void MinimumTax_ExemptRegime_NoMinimum_TaxOnResultPrevails()
    {
        var dto = FiscalResultAssembler.Assemble(
            Sheet(1_000m, localTurnoverTtc: 100_000m, minimumTaxRegime: MinimumTaxRegime.Exempt), Params(), enabled: true);

        Assert.Equal(0m, dto.Computation.MinimumTax);
        Assert.Equal(150m, dto.Computation.TaxDue);            // taxDue = max(150, 0) = 150
    }

    [Fact]
    public void TaxDue_EqualsTaxOnResult_WhenResultDominatesMinimum()
    {
        // Résultat 100 000 → IS 15 000 >> minimum 500 : l'impôt calculé prévaut sur le plancher.
        var dto = FiscalResultAssembler.Assemble(Sheet(100_000m, localTurnoverTtc: 100_000m), Params(), enabled: true);

        Assert.Equal(15_000m, dto.Computation.TaxOnResult);
        Assert.Equal(500m, dto.Computation.MinimumTax);
        Assert.Equal(15_000m, dto.Computation.TaxDue);         // taxDue = max(15 000, 500) = 15 000
    }

    [Fact]
    public void MinimumTax_AppliesEvenInDeficit_WhenNoTaxableResult()
    {
        // Résultat négatif : pas d'IS, mais le minimum d'impôt (plancher 500) reste dû ; déficit reportable.
        var dto = FiscalResultAssembler.Assemble(Sheet(-5_000m, localTurnoverTtc: 100_000m), Params(), enabled: true);

        Assert.Equal(5_000m, dto.Computation.DeficitGeneratedThisYear);
        Assert.Equal(0m, dto.Computation.TaxableResult);
        Assert.Equal(0m, dto.Computation.TaxOnResult);
        Assert.Equal(500m, dto.Computation.TaxDue);            // minimum dû malgré le déficit
        Assert.Equal(0m, dto.Computation.Css);                 // CSS = 1 % × 0 = 0
    }

    // — CSS 1 % et arrondi de l'assiette au dinar inférieur —

    [Fact]
    public void Css_OnePercentOfFlooredTaxableResult()
    {
        // Assiette 100 000 → CSS 1 % = 1 000 (assiette arrondie, sans millimes).
        var dto = FiscalResultAssembler.Assemble(Sheet(100_000m), Params(), enabled: true);

        Assert.Equal(100_000m, dto.Computation.TaxableResult);
        Assert.Equal(1_000m, dto.Computation.Css);
    }

    [Fact]
    public void Assiette_RoundedDownToDinar_BeforeRateAndCss()
    {
        // 12 345,678 → arrondi au dinar inférieur = 12 345 ; IS = 1 851,75 ; CSS = 123,45.
        var dto = FiscalResultAssembler.Assemble(Sheet(12_345.678m, localTurnoverTtc: 100_000m), Params(), enabled: true);

        Assert.Equal(12_345m, dto.Computation.TaxableResult);  // les millimes sont abandonnées (floor)
        Assert.Equal(1_851.750m, dto.Computation.TaxOnResult); // 15 % × 12 345
        Assert.Equal(123.450m, dto.Computation.Css);           // 1 % × 12 345
    }

    // — Barème IRPP progressif multi-tranches (LF 2025) —

    [Fact]
    public void Irpp_ProgressiveScale_MultiBracketTaxOnResult()
    {
        // Personne physique IRPP-BIC, résultat imposable 80 000. Barème LF 2025 :
        // 5 000×0 + 5 000×15 % + 10 000×25 % + 10 000×30 % + 10 000×33 % + 10 000×36 % +
        // 20 000×38 % + 10 000×40 % = 24 750.
        var dto = FiscalResultAssembler.Assemble(
            Sheet(80_000m, kind: TaxpayerKind.IndividualIrppBic), Params(), enabled: true);

        Assert.Equal((int)TaxpayerKind.IndividualIrppBic, dto.Computation.TaxpayerKind);
        Assert.Equal(24_750m, dto.Computation.TaxOnResult);
        Assert.Equal(24_750m, dto.Computation.TaxDue);         // domine le minimum 500
        Assert.Equal(800m, dto.Computation.Css);               // 1 % × 80 000
    }

    // — Crédits (acomptes + RAS + crédit antérieur) et net à payer —

    [Fact]
    public void Credits_AcomptesPlusWithholdingPlusPriorCredit_NetToPay()
    {
        // Impôt dû 15 000 + CSS 1 000 = 16 000 ; crédits 4 000 + 2 000 + 1 000 = 7 000 → net 9 000.
        var dto = FiscalResultAssembler.Assemble(
            Sheet(100_000m, localTurnoverTtc: 100_000m,
                acomptesPaid: 4_000m, withholdingSuffered: 2_000m, priorTaxCredit: 1_000m),
            Params(), enabled: true);

        Assert.Equal(16_000m, dto.Computation.TotalTaxDue);
        Assert.Equal(4_000m, dto.Computation.AcomptesPaid);
        Assert.Equal(2_000m, dto.Computation.WithholdingSuffered);
        Assert.Equal(1_000m, dto.Computation.PriorTaxCredit);
        Assert.Equal(9_000m, dto.Computation.NetToPay);
        Assert.Equal(0m, dto.Computation.CreditToCarry);
    }

    [Fact]
    public void Credits_ExceedTotalTaxDue_CreditToCarry_NoNetToPay()
    {
        // Acomptes 20 000 > impôt total 16 000 → excédent 4 000 reporté, rien à payer.
        var dto = FiscalResultAssembler.Assemble(
            Sheet(100_000m, localTurnoverTtc: 100_000m, acomptesPaid: 20_000m), Params(), enabled: true);

        Assert.Equal(0m, dto.Computation.NetToPay);
        Assert.Equal(4_000m, dto.Computation.CreditToCarry);
    }

    // — Avertissements FIFO / amortissements différés (non bloquants au brouillon) —

    [Fact]
    public void Draft_NonFifoOrder_RaisesWarning_ButComputationProceeds()
    {
        // Déficit 2020 (stock 10 000, non imputé) + déficit 2023 (imputé 5 000) → FIFO non respecté.
        var declaration = Sheet(50_000m);
        declaration.ReplaceCarryForwards(new[]
        {
            Deficit(2020, 10_000m, 0m, expiry: 2030),   // plus ancien, non imputé, encore imputable
            Deficit(2023, 5_000m, 5_000m, 2028)          // plus récent, imputé → FIFO violé
        });
        var dto = FiscalResultAssembler.Assemble(declaration, Params(), enabled: true);

        Assert.Contains(dto.Warnings, w => w.Contains("FIFO"));
        Assert.Equal(5_000m, dto.Computation.DeficitsImputed); // seul le 2023 est imputé
        Assert.Equal(45_000m, dto.Computation.TaxableResult); // 50 000 − 5 000
    }

    [Fact]
    public void Draft_DeferredBeforeOrdinaryDeficit_RaisesWarning_ButComputationProceeds()
    {
        // Déficit ordinaire 2024 (stock 8 000, non imputé) + amortissement différé imputé 6 000 → ordre d'imputation.
        var declaration = Sheet(50_000m);
        declaration.ReplaceCarryForwards(new[]
        {
            Deficit(2024, 8_000m, 0m, 2029),
            Deferred(2022, 6_000m, 6_000m)
        });
        var dto = FiscalResultAssembler.Assemble(declaration, Params(), enabled: true);

        Assert.Contains(dto.Warnings, w => w.Contains("Amortissement différé"));
        Assert.Equal(6_000m, dto.Computation.DeferredDepreciationImputed);
        Assert.Equal(44_000m, dto.Computation.TaxableResult); // 50 000 − 6 000 (le déficit 2024 n'est pas imputé)
    }
}
