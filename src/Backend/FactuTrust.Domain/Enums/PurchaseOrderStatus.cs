namespace FactuTrust.Domain.Enums;

/// <summary>
/// Represents the lifecycle status of a purchase order (commande fournisseur).
/// </summary>
public enum PurchaseOrderStatus
{
    /// <summary>
    /// Draft - can be edited or deleted.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// Confirmed - sent to the supplier, cannot be edited.
    /// </summary>
    Confirmed = 1,

    /// <summary>
    /// Partially received - some lines have been received.
    /// </summary>
    PartiallyReceived = 2,

    /// <summary>
    /// Fully received - all lines have been received.
    /// </summary>
    Received = 3,

    /// <summary>
    /// Cancelled - voided by the user.
    /// </summary>
    Cancelled = 4,

    /// <summary>
    /// Invoiced - a supplier invoice has been created.
    /// </summary>
    Invoiced = 5
}

public static class PurchaseOrderStatusExtensions
{
    public static bool CanBeEdited(this PurchaseOrderStatus status) =>
        status == PurchaseOrderStatus.Draft;

    public static bool CanBeConfirmed(this PurchaseOrderStatus status) =>
        status == PurchaseOrderStatus.Draft;

    public static bool CanReceiveGoods(this PurchaseOrderStatus status) =>
        status is PurchaseOrderStatus.Confirmed or PurchaseOrderStatus.PartiallyReceived;

    public static bool CanBeCancelled(this PurchaseOrderStatus status) =>
        status is PurchaseOrderStatus.Draft or PurchaseOrderStatus.Confirmed;

    public static bool IsFinalized(this PurchaseOrderStatus status) =>
        status is PurchaseOrderStatus.Received or PurchaseOrderStatus.Cancelled or PurchaseOrderStatus.Invoiced;

    public static string ToDisplayString(this PurchaseOrderStatus status) => status switch
    {
        PurchaseOrderStatus.Draft => "Brouillon",
        PurchaseOrderStatus.Confirmed => "Confirmée",
        PurchaseOrderStatus.PartiallyReceived => "Partiellement reçue",
        PurchaseOrderStatus.Received => "Reçue",
        PurchaseOrderStatus.Cancelled => "Annulée",
        PurchaseOrderStatus.Invoiced => "Facturée",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    public static string ToCssClass(this PurchaseOrderStatus status) => status switch
    {
        PurchaseOrderStatus.Draft => "status-draft",
        PurchaseOrderStatus.Confirmed => "status-confirmed",
        PurchaseOrderStatus.PartiallyReceived => "status-partial",
        PurchaseOrderStatus.Received => "status-received",
        PurchaseOrderStatus.Cancelled => "status-cancelled",
        PurchaseOrderStatus.Invoiced => "status-invoiced",
        _ => "status-unknown"
    };
}
