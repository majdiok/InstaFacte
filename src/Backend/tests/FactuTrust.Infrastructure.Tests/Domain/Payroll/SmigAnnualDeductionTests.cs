using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Payroll;

/// <summary>
/// R-12 : déduction annuelle 500 TND (SMIG/SMAG) — forfait mensuel R(500/12) = 41,667 appliqué sur
/// le net imposable des salariés éligibles (rémunération ≤ SMIG du mois), puis forfait annuel EXACT
/// proratisé aux mois éligibles à la régularisation de fin d'exercice (neutralise la dérive d'arrondi
/// mensuelle : 12 × 41,667 = 500,004 ≠ 500).
/// </summary>
public sealed class SmigAnnualDeductionTests
{
    private static PayrollYearParameters Params() => PayrollParameterDefaults.CreateDefaults(2026).Value;

    private static PayrollYearParameters ParamsWithSmigMode(SmigIrppExemptionMode mode)
    {
        var p = Params();
        var result = p.UpdateRates(
            p.CnssEmployeeRate, p.CnssEmployerRate, p.CssRate, p.CssAnnualExemptionThreshold,
            p.ProfessionalExpensesRate, p.ProfessionalExpensesAnnualCap,
            p.HeadOfFamilyAnnualDeduction, p.ChildAnnualDeduction, p.MaxDeductibleChildren,
            p.TfpRateIndustry, p.TfpRateOther, p.FoprolosRate, p.MonthlySmig,
            p.CnssEmployeeRateRsa, p.CnssEmployerRateRsa,
            p.EnforceSmigOnContracts, p.EnableExtendedOvertimeRates, p.EnableAllowanceQuadrantMatrix,
            p.StudentChildAnnualDeduction, p.DisabledChildAnnualDeduction,
            p.ParentDeductionRatePercent, p.ParentAnnualDeductionCap, p.IsIndustrialSector,
            p.MealVoucherDailyExemptionCap, p.EnableIrppRegularization,
            smigIrppExemptionMode: mode);
        Assert.True(result.IsSuccess);
        return p;
    }

    [Fact]
    public void Monthly_SmigSalary_Eligible_AppliesForfait_AndReducesNetTaxable()
    {
        var input = new PayrollComputationInput
        {
            BaseSalary = 554.736m, // SMIG mensuel 2026
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m
        };

        var none = ParamsWithSmigMode(SmigIrppExemptionMode.None);
        var deduction = ParamsWithSmigMode(SmigIrppExemptionMode.SmigAnnualDeduction);

        var cNone = PayrollCalculator.Compute(input, none);
        var cDeduction = PayrollCalculator.Compute(input, deduction);

        Assert.Equal(0m, cNone.SmigAnnualDeductionAmount);
        Assert.Equal(41.667m, cDeduction.SmigAnnualDeductionAmount); // R(500 / 12)
        // Le forfait mensuel ampute le net imposable ; le reste du calcul est inchangé hormis cette base.
        Assert.Equal(Round(cNone.MonthlyNetTaxable - 41.667m), cDeduction.MonthlyNetTaxable);
    }

    [Fact]
    public void Monthly_AboveSmig_NotEligible_NoDeduction()
    {
        // Net imposable supérieur au SMIG → non éligible, aucun forfait appliqué.
        var input = new PayrollComputationInput
        {
            BaseSalary = 700m,
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m
        };

        var deduction = ParamsWithSmigMode(SmigIrppExemptionMode.SmigAnnualDeduction);
        var c = PayrollCalculator.Compute(input, deduction);

        Assert.Equal(0m, c.SmigAnnualDeductionAmount);
        Assert.True(c.MonthlyNetTaxable > 554.736m); // confirme la non-éligibilité
    }

    [Fact]
    public void AnnualRegularization_AppliesExactAnnualForfait_NeutralizesMonthlyRoundingDrift()
    {
        // 12 mois éligibles à SMIG, net imposable mensuel post-déduction = 400 porté sur chaque bulletin.
        // Somme naïve = 4800,000. La régularisation reconstitue le cumul avant déduction
        // (4800 + 12 × 41,667 = 5300,004) puis applique le forfait annuel EXACT (500,000),
        // soit un cumul corrigé de 4800,004 — neutralisant la dérive (12 × 41,667 = 500,004 ≠ 500).
        var months = Enumerable.Range(1, 12)
            .Select(m => new IrppRegularizationMonth(m, MonthlyNetTaxable: 400m, Irpp: 0m, Css: 0m, BaseSalary: 554.736m))
            .ToList();

        var deduction = ParamsWithSmigMode(SmigIrppExemptionMode.SmigAnnualDeduction);
        var result = IrppRegularizationCalculator.Compute(months, deduction);

        Assert.Equal(4800.004m, result.CumulNetTaxable);
    }

    [Fact]
    public void AnnualRegularization_NoneMode_LeavesCumulUnchanged()
    {
        // Hors mode SmigAnnualDeduction, la régularisation ne reconstitue rien : cumul = somme brute.
        var months = Enumerable.Range(1, 12)
            .Select(m => new IrppRegularizationMonth(m, 400m, 0m, 0m, 554.736m))
            .ToList();

        var none = ParamsWithSmigMode(SmigIrppExemptionMode.None);
        var result = IrppRegularizationCalculator.Compute(months, none);

        Assert.Equal(4800.000m, result.CumulNetTaxable);
    }

    private static decimal Round(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
