namespace FactuTrust.Domain.Enums;

/// <summary>
/// Status of a supplier invoice (facture fournisseur).
/// </summary>
public enum SupplierInvoiceStatus
{
    /// <summary>
    /// Invoice pending payment.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Invoice has been paid.
    /// </summary>
    Paid = 1,

    /// <summary>
    /// Invoice has been cancelled.
    /// </summary>
    Cancelled = 2,

    /// <summary>
    /// Partial payment received.
    /// </summary>
    PartiallyPaid = 3
}

public static class SupplierInvoiceStatusExtensions
{
    public static string ToDisplayString(this SupplierInvoiceStatus status) => status switch
    {
        SupplierInvoiceStatus.Pending => "En attente",
        SupplierInvoiceStatus.Paid => "Payée",
        SupplierInvoiceStatus.Cancelled => "Annulée",
        SupplierInvoiceStatus.PartiallyPaid => "Partiellement payée",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    public static string ToCssClass(this SupplierInvoiceStatus status) => status switch
    {
        SupplierInvoiceStatus.Pending => "status-pending",
        SupplierInvoiceStatus.Paid => "status-paid",
        SupplierInvoiceStatus.Cancelled => "status-cancelled",
        SupplierInvoiceStatus.PartiallyPaid => "status-partially-paid",
        _ => "status-unknown"
    };
}
