using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Payroll;

public sealed class PayrollCalculatorDeductionLinesTests
{
    private static PayrollYearParameters Params() => PayrollParameterDefaults.CreateDefaults(2026).Value;

    [Fact]
    public void DeductionLines_Empty_KeepsLegacyOtherDeductionsBehavior()
    {
        var input = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            OtherDeductions = 150m
        };

        var result = PayrollCalculator.Compute(input, Params());
        Assert.Equal(150m, result.OtherDeductions);
        Assert.Contains(result.Lines, l => l.Label.Contains("Autres retenues"));
    }

    [Fact]
    public void DeductionLines_MultipleKinds_ProducesTypedPayslipLines()
    {
        var input = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            DeductionLines =
            [
                new DeductionLineInput("Avance", 100m, DeductionKind.Advance),
                new DeductionLineInput("Prêt", 50m, DeductionKind.Loan)
            ],
            OtherDeductions = 150m
        };

        var result = PayrollCalculator.Compute(input, Params());
        Assert.Equal(150m, result.OtherDeductions);
        Assert.Contains(result.Lines, l => l.Label == "Avance" && l.DeductionKind == DeductionKind.Advance);
        Assert.Contains(result.Lines, l => l.Label == "Prêt" && l.DeductionKind == DeductionKind.Loan);
    }

    [Fact]
    public void InKindBenefit_AddsToGrossAndCnssable_AndAppliesOffset()
    {
        var without = PayrollCalculator.Compute(new PayrollComputationInput { BaseSalary = 2000m }, Params());
        var with = PayrollCalculator.Compute(new PayrollComputationInput
        {
            BaseSalary = 2000m,
            InKindTaxableCnssableBenefits = 300m,
            DeductionLines = [new DeductionLineInput("Compensation AEN", 300m, DeductionKind.InKindBenefitOffset)]
        }, Params());

        Assert.True(with.CnssableGross > without.CnssableGross);
        Assert.True(with.Irpp > without.Irpp);
        Assert.Contains(with.Lines, l => l.DeductionKind == DeductionKind.InKindBenefitOffset);
        // Le net baisse uniquement du supplément d'IRPP/CSS (l'avantage n'est pas versé en cash).
        Assert.True(with.NetSalary < without.NetSalary);
        Assert.True(with.NetSalary > without.NetSalary - 300m);
    }

    [Fact]
    public void PostTaxDeductions_ReduceNet_AfterIrpp()
    {
        var input = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            PostTaxDeductionLines = [new DeductionLineInput("Saisie", 200m, DeductionKind.Garnishment)]
        };

        var without = PayrollCalculator.Compute(new PayrollComputationInput { BaseSalary = 2000m }, Params());
        var result = PayrollCalculator.Compute(input, Params());

        Assert.Equal(without.NetSalary - 200m, result.NetSalary);
        Assert.Contains(result.Lines, l => l.Label == "Saisie");
    }
}
