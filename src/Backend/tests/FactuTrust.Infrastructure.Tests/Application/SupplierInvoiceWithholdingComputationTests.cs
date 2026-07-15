using FactuTrust.Application.Common;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class SupplierInvoiceWithholdingComputationTests
{
    [Fact]
    public void GetFiscalYearForRs7Threshold_WhenPaidAtSet_UsesPaidAtYear()
    {
        var invoiceDate = new DateTime(2025, 6, 1);
        var paidAt = new DateTime(2026, 4, 5);
        Assert.Equal(2026, SupplierInvoiceWithholdingComputation.GetFiscalYearForRs7Threshold(invoiceDate, paidAt));
    }

    [Fact]
    public void GetFiscalYearForRs7Threshold_WhenNoPaidAt_UsesInvoiceDateYear()
    {
        var invoiceDate = new DateTime(2026, 1, 15);
        Assert.Equal(2026, SupplierInvoiceWithholdingComputation.GetFiscalYearForRs7Threshold(invoiceDate, null));
    }
}
