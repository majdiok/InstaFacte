namespace FactuTrust.Domain.Enums;

/// <summary>
/// Lifecycle status of a purchase receipt (bon de réception d'achat).
/// </summary>
public enum PurchaseReceiptStatus
{
    /// <summary>Draft — editable, no stock/PO impact.</summary>
    Draft = 0,

    /// <summary>Validated — stock entries created, PO quantities updated, document locked.</summary>
    Validated = 1,

    /// <summary>Cancelled — voided (validated cancellation reverses stock and PO quantities).</summary>
    Cancelled = 2,

    /// <summary>Partially invoiced — some received quantities have been invoiced.</summary>
    PartiallyInvoiced = 3,

    /// <summary>Invoiced — all received quantities on this receipt have been invoiced.</summary>
    Invoiced = 4
}

public static class PurchaseReceiptStatusExtensions
{
    public static bool CanBeEdited(this PurchaseReceiptStatus status) =>
        status == PurchaseReceiptStatus.Draft;

    public static bool CanBeValidated(this PurchaseReceiptStatus status) =>
        status == PurchaseReceiptStatus.Draft;

    public static bool CanBeCancelled(this PurchaseReceiptStatus status) =>
        status is PurchaseReceiptStatus.Draft or PurchaseReceiptStatus.Validated
            or PurchaseReceiptStatus.PartiallyInvoiced;

    public static bool CanBeInvoiced(this PurchaseReceiptStatus status) =>
        status is PurchaseReceiptStatus.Validated or PurchaseReceiptStatus.PartiallyInvoiced;

    public static bool CanBeDeleted(this PurchaseReceiptStatus status) =>
        status == PurchaseReceiptStatus.Draft;

    public static bool IsFinalized(this PurchaseReceiptStatus status) =>
        status is PurchaseReceiptStatus.Validated
            or PurchaseReceiptStatus.PartiallyInvoiced
            or PurchaseReceiptStatus.Invoiced
            or PurchaseReceiptStatus.Cancelled;

    public static string ToDisplayString(this PurchaseReceiptStatus status) => status switch
    {
        PurchaseReceiptStatus.Draft => "Brouillon",
        PurchaseReceiptStatus.Validated => "Validé",
        PurchaseReceiptStatus.Cancelled => "Annulé",
        PurchaseReceiptStatus.PartiallyInvoiced => "Partiellement facturé",
        PurchaseReceiptStatus.Invoiced => "Facturé",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    public static string ToCssClass(this PurchaseReceiptStatus status) => status switch
    {
        PurchaseReceiptStatus.Draft => "status-draft",
        PurchaseReceiptStatus.Validated => "status-validated",
        PurchaseReceiptStatus.Cancelled => "status-cancelled",
        PurchaseReceiptStatus.PartiallyInvoiced => "status-partial-invoiced",
        PurchaseReceiptStatus.Invoiced => "status-invoiced",
        _ => "status-unknown"
    };
}
