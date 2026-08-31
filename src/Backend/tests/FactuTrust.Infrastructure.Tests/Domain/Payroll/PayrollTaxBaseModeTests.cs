using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Payroll;

/// <summary>
/// R-24 : assiette légale des taxes patronales (TFP, FOPROLOS, CSS patronale) = rémunération brute
/// totale, et non plus la seule assiette CNSSable. Les indemnités non soumises à la CNSS
/// (transport dans les limites légales, etc.) doivent grossir l'assiette fiscale, faute de quoi la
/// déclaration mensuelle sous-évalue les taxes.
/// </summary>
public sealed class PayrollTaxBaseModeTests
{
    private static PayrollYearParameters Params() => PayrollParameterDefaults.CreateDefaults(2026).Value;

    [Fact]
    public void TotalGrossMode_TfpFoprolos_BaseOnTotalGross_IncludingNonCnssableAllowance()
    {
        // Preset 2026 → PayrollTaxBaseMode.TotalGross (assiette légale).
        var parameters = Params();
        Assert.Equal(PayrollTaxBaseMode.TotalGross, parameters.PayrollTaxBaseMode);

        var input = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            NonTaxableAllowances = 300m, // non soumise à la CNSS mais versée
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m
        };

        var c = PayrollCalculator.Compute(input, parameters);

        // Brut total = 2000 (CNSSable) + 300 (non soumise) = 2300.
        Assert.Equal(2300m, c.GrossSalary);
        Assert.Equal(2300m, c.PayrollTaxBase);
        Assert.Equal(46.000m, c.Tfp);       // 2300 × 2 % (non-industriel)
        Assert.Equal(23.000m, c.Foprolos);  // 2300 × 1 %
        Assert.Equal(2000m, c.CnssableGross); // la base CNSS, elle, exclut l'indemnité
    }

    [Fact]
    public void LegacyMode_TfpFoprolos_BaseOnCnssableGross_NonCnssableExcluded()
    {
        var parameters = Params();
        parameters.SetPayrollTaxBaseMode(PayrollTaxBaseMode.Legacy);
        Assert.Equal(PayrollTaxBaseMode.Legacy, parameters.PayrollTaxBaseMode);

        var input = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            NonTaxableAllowances = 300m,
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m
        };

        var c = PayrollCalculator.Compute(input, parameters);

        // Legacy + plafond CNSS désactivé (preset) → assiette = brut CNSSable = 2000.
        Assert.Equal(2000m, c.PayrollTaxBase);
        Assert.Equal(40.000m, c.Tfp);      // 2000 × 2 %
        Assert.Equal(20.000m, c.Foprolos); // 2000 × 1 %
        // Le brut total affiché reste 2300 : seule l'assiette fiscale diffère.
        Assert.Equal(2300m, c.GrossSalary);
    }

    [Fact]
    public void TotalGrossMode_WithoutNonCnssableAllowance_MatchesLegacy_NoRegression()
    {
        // R-24 : sans indemnité non soumise, totalGross == cnssableGross → aucun changement de
        // comportement. Les cycles existants à salaire de base seul ne sont pas impactés.
        var input = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m
        };

        var totalGross = Params();
        var legacy = Params();
        legacy.SetPayrollTaxBaseMode(PayrollTaxBaseMode.Legacy);

        var cTotalGross = PayrollCalculator.Compute(input, totalGross);
        var cLegacy = PayrollCalculator.Compute(input, legacy);

        Assert.Equal(cLegacy.PayrollTaxBase, cTotalGross.PayrollTaxBase);
        Assert.Equal(cLegacy.Tfp, cTotalGross.Tfp);
        Assert.Equal(cLegacy.Foprolos, cTotalGross.Foprolos);
        Assert.Equal(cLegacy.CssEmployer, cTotalGross.CssEmployer);
    }
}
