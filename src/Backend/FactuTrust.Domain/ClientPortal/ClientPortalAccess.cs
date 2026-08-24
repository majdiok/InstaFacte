using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.ClientPortal;

/// <summary>
/// Pure rules for the client portal: API isolation and document visibility.
/// Kept free of ASP.NET types so unit tests do not need the API project.
/// </summary>
public static class ClientPortalAccess
{
    public static readonly InvoiceStatus[] VisibleInvoiceStatuses =
    {
        InvoiceStatus.Validated,
        InvoiceStatus.Signed,
        InvoiceStatus.PartiallyPaid,
        InvoiceStatus.Paid,
        InvoiceStatus.Overdue,
        InvoiceStatus.Cancelled,
        InvoiceStatus.Archived
    };

    public static bool IsInvoiceVisibleToPortal(InvoiceStatus status) =>
        status != InvoiceStatus.Draft && VisibleInvoiceStatuses.Contains(status);

    /// <summary>
    /// Staff API paths a portal JWT must never reach. Auth + portal + anonymous public stay allowed.
    /// </summary>
    public static bool IsDeniedStaffApiPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var p = path.Trim();
        if (!p.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
            return false;

        if (p.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase))
            return false;

        if (p.StartsWith("/api/portal", StringComparison.OrdinalIgnoreCase))
            return false;

        if (p.StartsWith("/api/public", StringComparison.OrdinalIgnoreCase))
            return false;

        if (p.StartsWith("/api/health", StringComparison.OrdinalIgnoreCase))
            return false;

        if (p.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }
}
