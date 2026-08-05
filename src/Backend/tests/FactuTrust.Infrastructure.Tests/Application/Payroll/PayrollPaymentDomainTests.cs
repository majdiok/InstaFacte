using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application.Payroll;

public sealed class PayrollPaymentDomainTests
{
    [Fact]
    public void AuxiliaryAccountResolver_PadsEmployeeNumber()
    {
        var account = PayrollEmployeeAuxiliaryAccountResolver.Resolve("1");
        Assert.Equal("4210000001", account);
    }

    [Fact]
    public void Payslip_RegisterPayment_UpdatesStatus()
    {
        var payslip = CreatePayslip(net: 2000m);
        var partial = Math.Round(payslip.NetSalary / 2, 3);
        var result = payslip.RegisterPayment(partial, DateTime.UtcNow, "4210001");
        Assert.True(result.IsSuccess);
        Assert.Equal(PayslipPaymentStatus.PartiallyPaid, payslip.PaymentStatus);
        Assert.Equal(Math.Round(payslip.NetSalary - partial, 3), payslip.RemainingToPay);
    }

    [Fact]
    public void PayrollRun_Reopen_BlockedWhenPaymentsExist()
    {
        var run = CreateValidatedRun();
        var payslip = run.Payslips.First();
        payslip.RegisterPayment(payslip.NetSalary, DateTime.UtcNow);
        var reopen = run.Reopen();
        Assert.True(reopen.IsFailure);
        Assert.Contains("paiements", reopen.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PayrollPayment_Create_ValidatesAmounts()
    {
        var run = CreateValidatedRun();
        var payslip = run.Payslips.First();
        var result = PayrollPayment.Create(
            run,
            [(payslip, payslip.NetSalary, "4210001")],
            DateTime.UtcNow.Date,
            PaymentMethod.BankTransfer,
            Guid.NewGuid(),
            "VIR-001",
            null);
        Assert.True(result.IsSuccess);
        Assert.Equal(payslip.NetSalary, result.Value.Amount.Amount);
    }

    private static Payslip CreatePayslip(decimal net)
    {
        var runId = Guid.NewGuid();
        var computation = PayrollCalculator.Compute(
            new PayrollComputationInput { BaseSalary = net, Regime = SocialRegime.Rsna },
            PayrollParameterDefaults.CreateDefaults(2026).Value);
        return Payslip.FromComputation(
            runId, Guid.NewGuid(), "Alice", "001", null, 2026, 8, computation, 9.18m, 16.57m);
    }

    private static PayrollRun CreateValidatedRun()
    {
        var run = PayrollRun.Create(2026, 8, 2026).Value;
        var payslip = CreatePayslip(1500m);
        run.SetPayslips([payslip]);
        run.Validate("tester");
        return run;
    }
}
