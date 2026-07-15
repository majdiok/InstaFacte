using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Payroll;

public sealed class PayrollCalculatorTests
{
    private static PayrollYearParameters Params()
    {
        var result = PayrollParameterDefaults.CreateDefaults(2026);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    [Fact]
    public void Compute_SimpleSalary_Rsna_MatchesHandComputedValues()
    {
        var input = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m,
            IsHeadOfFamily = false,
            DependentChildren = 0
        };

        var c = PayrollCalculator.Compute(input, Params());

        Assert.Equal(2000m, c.GrossSalary);
        Assert.Equal(183.600m, c.CnssEmployee);          // 2000 * 9.18%
        Assert.Equal(1816.400m, c.TaxableBaseAfterCnss);
        Assert.Equal(166.667m, c.ProfessionalExpenses);  // capped at 2000/12
        Assert.Equal(1649.733m, c.MonthlyNetTaxable);
        Assert.Equal(266.600m, c.Irpp);
        Assert.Equal(8.249m, c.Css);
        Assert.Equal(1541.551m, c.NetSalary);
        Assert.Equal(331.400m, c.CnssEmployer);          // 2000 * 16.57%
        Assert.Equal(8.000m, c.WorkAccidentContribution);// 2000 * 0.4%
        Assert.Equal(40.000m, c.Tfp);                    // 2000 * 2% (non-industrial)
        Assert.Equal(20.000m, c.Foprolos);               // 2000 * 1%
    }

    [Fact]
    public void Compute_SivpExonere_HasNoCnss()
    {
        var input = new PayrollComputationInput
        {
            BaseSalary = 1500m,
            Regime = SocialRegime.SivpExonere,
            WorkAccidentRate = 1m
        };

        var c = PayrollCalculator.Compute(input, Params());

        Assert.Equal(0m, c.CnssEmployee);
        Assert.Equal(0m, c.CnssEmployer);
        Assert.Equal(0m, c.WorkAccidentContribution);
        // Base imposable == brut puisque pas de CNSS.
        Assert.Equal(1500m, c.TaxableBaseAfterCnss);
    }

    [Fact]
    public void Compute_LowSalary_FullyExempt_NoIrppNoCss()
    {
        // Un salaire faible dont le net imposable annuel reste sous 5000 DT.
        var input = new PayrollComputationInput
        {
            BaseSalary = 400m,
            Regime = SocialRegime.Rsna
        };

        var c = PayrollCalculator.Compute(input, Params());

        Assert.True(c.AnnualNetTaxable < 5000m);
        Assert.Equal(0m, c.Irpp);
        Assert.Equal(0m, c.Css);
    }

    [Fact]
    public void Compute_HighSalary_ProfessionalExpensesCapped()
    {
        var input = new PayrollComputationInput
        {
            BaseSalary = 8000m,
            Regime = SocialRegime.Rsna
        };

        var c = PayrollCalculator.Compute(input, Params());

        // Plafond mensuel = 2000/12 = 166.667.
        Assert.Equal(166.667m, c.ProfessionalExpenses);
    }

    [Fact]
    public void Compute_HeadOfFamilyWithChildren_ReducesIrpp()
    {
        var baseInput = new PayrollComputationInput { BaseSalary = 3000m, Regime = SocialRegime.Rsna };
        var withFamily = new PayrollComputationInput
        {
            BaseSalary = 3000m,
            Regime = SocialRegime.Rsna,
            IsHeadOfFamily = true,
            DependentChildren = 3
        };

        var pars = Params();
        var single = PayrollCalculator.Compute(baseInput, pars);
        var family = PayrollCalculator.Compute(withFamily, pars);

        Assert.True(family.Irpp < single.Irpp);
        Assert.True(family.NetSalary > single.NetSalary);
    }

    [Fact]
    public void Compute_UnpaidAbsence_ReducesGross()
    {
        var input = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            UnpaidAbsenceAmount = 200m,
            Regime = SocialRegime.Rsna
        };

        var c = PayrollCalculator.Compute(input, Params());

        Assert.Equal(1800m, c.CnssableGross);
    }

    [Fact]
    public void Compute_NonTaxableAllowance_AddedToNetButNotTaxed()
    {
        var without = new PayrollComputationInput { BaseSalary = 2000m, Regime = SocialRegime.Rsna };
        var with = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            NonTaxableAllowances = 100m,
            Regime = SocialRegime.Rsna
        };

        var pars = Params();
        var a = PayrollCalculator.Compute(without, pars);
        var b = PayrollCalculator.Compute(with, pars);

        // L'IRPP ne change pas (non imposable), mais le net augmente de 100.
        Assert.Equal(a.Irpp, b.Irpp);
        Assert.Equal(a.NetSalary + 100m, b.NetSalary);
    }

    [Theory]
    [InlineData(5000, 0)]        // Entièrement dans la tranche 0 %.
    [InlineData(10000, 750)]     // 5000 * 15%.
    [InlineData(20000, 3250)]    // 750 + 10000*25%.
    [InlineData(80000, 24750)]   // Toutes tranches + 10000*40%.
    public void ComputeProgressiveTax_MatchesBareme(decimal annualIncome, decimal expectedTax)
    {
        var tax = PayrollCalculator.ComputeProgressiveTax(annualIncome, Params());
        Assert.Equal(expectedTax, tax);
    }

    [Fact]
    public void Compute_ZeroSalary_ProducesZeros()
    {
        var c = PayrollCalculator.Compute(new PayrollComputationInput { BaseSalary = 0m }, Params());

        Assert.Equal(0m, c.GrossSalary);
        Assert.Equal(0m, c.CnssEmployee);
        Assert.Equal(0m, c.Irpp);
        Assert.Equal(0m, c.Css);
        Assert.Equal(0m, c.NetSalary);
    }

    [Fact]
    public void Compute_OtherDeductions_ReduceNet()
    {
        var input = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            OtherDeductions = 150m,
            Regime = SocialRegime.Rsna
        };

        var reference = PayrollCalculator.Compute(new PayrollComputationInput { BaseSalary = 2000m, Regime = SocialRegime.Rsna }, Params());
        var c = PayrollCalculator.Compute(input, Params());

        Assert.Equal(reference.NetSalary - 150m, c.NetSalary);
    }

    [Fact]
    public void Compute_OvertimeAmount_IncreasesCnssableGrossAndNet()
    {
        var without = new PayrollComputationInput { BaseSalary = 2000m, Regime = SocialRegime.Rsna };
        var withOvertime = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            OvertimeAmount = 100m,
            Regime = SocialRegime.Rsna
        };

        var pars = Params();
        var a = PayrollCalculator.Compute(without, pars);
        var b = PayrollCalculator.Compute(withOvertime, pars);

        Assert.Equal(2100m, b.CnssableGross);
        Assert.True(b.NetSalary > a.NetSalary);
        Assert.True(b.CnssEmployee > a.CnssEmployee);
    }

    [Fact]
    public void Compute_RsaRegime_UsesRsaCnssRates()
    {
        var pars = Params();
        var updateResult = pars.UpdateRates(
            pars.CnssEmployeeRate,
            pars.CnssEmployerRate,
            pars.CssRate,
            pars.CssAnnualExemptionThreshold,
            pars.ProfessionalExpensesRate,
            pars.ProfessionalExpensesAnnualCap,
            pars.HeadOfFamilyAnnualDeduction,
            pars.ChildAnnualDeduction,
            pars.MaxDeductibleChildren,
            pars.TfpRateIndustry,
            pars.TfpRateOther,
            pars.FoprolosRate,
            pars.MonthlySmig,
            cnssEmployeeRateRsa: 5m,
            cnssEmployerRateRsa: 10m,
            enforceSmigOnContracts: false,
            enableExtendedOvertimeRates: false,
            enableAllowanceQuadrantMatrix: false);
        Assert.True(updateResult.IsSuccess);

        var input = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            Regime = SocialRegime.Rsa
        };

        var c = PayrollCalculator.Compute(input, pars);

        Assert.Equal(100m, c.CnssEmployee);
        Assert.Equal(200m, c.CnssEmployer);
    }

    [Fact]
    public void Compute_QuadrantMatrix_TaxableOnly_IsTaxedButNotCnssSubject()
    {
        var pars = Params();
        var updateResult = pars.UpdateRates(
            pars.CnssEmployeeRate,
            pars.CnssEmployerRate,
            pars.CssRate,
            pars.CssAnnualExemptionThreshold,
            pars.ProfessionalExpensesRate,
            pars.ProfessionalExpensesAnnualCap,
            pars.HeadOfFamilyAnnualDeduction,
            pars.ChildAnnualDeduction,
            pars.MaxDeductibleChildren,
            pars.TfpRateIndustry,
            pars.TfpRateOther,
            pars.FoprolosRate,
            pars.MonthlySmig,
            pars.CnssEmployeeRateRsa,
            pars.CnssEmployerRateRsa,
            enforceSmigOnContracts: false,
            enableExtendedOvertimeRates: false,
            enableAllowanceQuadrantMatrix: true);
        Assert.True(updateResult.IsSuccess);

        var input = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            TaxableOnlyAllowances = 100m,
            Regime = SocialRegime.Rsna
        };

        var without = PayrollCalculator.Compute(new PayrollComputationInput { BaseSalary = 2000m, Regime = SocialRegime.Rsna }, pars);
        var with = PayrollCalculator.Compute(input, pars);

        Assert.Equal(2000m, with.CnssableGross);
        Assert.Equal(2100m, with.GrossSalary);
        Assert.Equal(without.CnssEmployee, with.CnssEmployee);
        Assert.True(with.Irpp > without.Irpp);
    }

    [Fact]
    public void Compute_OvertimeAmount_AddsEarningLine()
    {
        var input = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            OvertimeAmount = 50m,
            Regime = SocialRegime.Rsna
        };

        var c = PayrollCalculator.Compute(input, Params());

        Assert.Contains(c.Lines, l => l.Label == "Heures supplémentaires" && l.Amount == 50m);
    }
}
