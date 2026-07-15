using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using FactuTrust.Infrastructure.Services;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application.Payroll;

public sealed class PayrollAccountingEntryTests
{
    [Fact]
    public void PayrollJournalEntry_IsBalanced_ForTypicalRun()
    {
        var parameters = PayrollParameterDefaults.CreateDefaults(2026).Value;
        var input = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m
        };
        var computation = PayrollCalculator.Compute(input, parameters);

        var run = PayrollRun.Create(2026, 7, 2026).Value;
        var payslip = Payslip.FromComputation(
            run.Id, Guid.NewGuid(), "Test User", "EMP-001", null, 2026, 7, computation,
            parameters.CnssEmployeeRate, parameters.CnssEmployerRate);
        run.SetPayslips([payslip]);

        var employerCharges = run.TotalCnssEmployer + run.TotalTfp + run.TotalFoprolos + run.TotalWorkAccident;
        var stateWithholding = run.TotalIrpp + run.TotalCss + run.TotalTfp + run.TotalFoprolos;
        var socialOrg = run.TotalCnssEmployee + run.TotalCnssEmployer + run.TotalWorkAccident;

        var totalDebit = run.TotalGross + employerCharges;
        var totalCredit = run.TotalNet + stateWithholding + socialOrg;

        Assert.Equal(totalDebit, totalCredit);
    }
}
