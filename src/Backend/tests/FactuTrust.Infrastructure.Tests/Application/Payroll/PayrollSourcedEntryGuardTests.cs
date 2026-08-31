using FactuTrust.Application.Features.Accounting;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application.Payroll;

/// <summary>
/// R-07 / R-08 : le garde-fou partagé reconnaît toutes les sources paie (extourne/suppression
/// manuelles interdites) et laisse passer les autres écritures.
/// </summary>
public sealed class PayrollSourcedEntryGuardTests
{
    [Theory]
    [InlineData("PayrollRun")]
    [InlineData("PayrollPayment")]
    [InlineData("CnssContributionPayment")]
    [InlineData("EmployeeAdvance")]
    [InlineData("EmployeeLoan")]
    public void IsSystemSource_RecognizesPayrollSources(string source) =>
        Assert.True(PayrollSourcedEntryGuard.IsSystemSource(source));

    [Theory]
    [InlineData("Invoice")]
    [InlineData("SupplierInvoice")]
    [InlineData("CashOperation")]
    [InlineData("BankDeposit")]
    [InlineData(null)]
    [InlineData("")]
    public void IsSystemSource_LeavesNonPayrollSourcesAlone(string? source) =>
        Assert.False(PayrollSourcedEntryGuard.IsSystemSource(source));
}
