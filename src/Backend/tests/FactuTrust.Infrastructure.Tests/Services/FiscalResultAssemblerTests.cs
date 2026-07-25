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
}
