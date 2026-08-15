using FactuTrust.API.Middleware;
using Xunit;

namespace FactuTrust.API.Tests;

public sealed class AccountingFirmMasterOnlyRoutesTests
{
    [Theory]
    [InlineData("/api/firm/dashboard", "accountingFirm", true)]
    [InlineData("/api/firm/dashboard/decision-tables", "accountingFirm", true)]
    [InlineData("/api/auth/me", "accountingFirm", true)]
    [InlineData("/api/honoraires/invoices", "accountingFirm", false)]
    [InlineData("/api/firm/dashboard", "company", false)]
    public void IsMatch_returns_expected(string path, string tenantKind, bool expected)
    {
        Assert.Equal(expected, AccountingFirmMasterOnlyRoutes.IsMatch(path, tenantKind));
    }
}
