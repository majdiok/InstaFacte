using FactuTrust.Domain.ClientPortal;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.ClientPortal;

public sealed class ClientPortalAccessTests
{
    [Theory]
    [InlineData("/api/invoices", true)]
    [InlineData("/api/invoices/pdf", true)]
    [InlineData("/api/clients", true)]
    [InlineData("/api/portal/invoices", false)]
    [InlineData("/api/auth/login", false)]
    [InlineData("/api/auth/refresh", false)]
    [InlineData("/api/public/street", false)]
    [InlineData("/api/health", false)]
    [InlineData("/health", false)]
    [InlineData("/dashboard", false)]
    public void IsDeniedStaffApiPath_isolates_portal_from_staff_api(string path, bool denied)
    {
        Assert.Equal(denied, ClientPortalAccess.IsDeniedStaffApiPath(path));
    }

    [Fact]
    public void IsInvoiceVisibleToPortal_excludes_draft()
    {
        Assert.False(ClientPortalAccess.IsInvoiceVisibleToPortal(InvoiceStatus.Draft));
        Assert.True(ClientPortalAccess.IsInvoiceVisibleToPortal(InvoiceStatus.Validated));
        Assert.True(ClientPortalAccess.IsInvoiceVisibleToPortal(InvoiceStatus.PartiallyPaid));
        Assert.True(ClientPortalAccess.IsInvoiceVisibleToPortal(InvoiceStatus.Paid));
        Assert.True(ClientPortalAccess.IsInvoiceVisibleToPortal(InvoiceStatus.Cancelled));
        Assert.True(ClientPortalAccess.IsInvoiceVisibleToPortal(InvoiceStatus.Archived));
        Assert.True(ClientPortalAccess.IsInvoiceVisibleToPortal(InvoiceStatus.Signed));
        Assert.True(ClientPortalAccess.IsInvoiceVisibleToPortal(InvoiceStatus.Overdue));
    }
}
