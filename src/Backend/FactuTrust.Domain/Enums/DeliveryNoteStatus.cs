namespace FactuTrust.Domain.Enums;

/// <summary>
/// Represents the lifecycle status of a delivery note (Bon de Livraison).
/// </summary>
public enum DeliveryNoteStatus
{
    /// <summary>
    /// Draft - can be edited or deleted.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// Confirmed - ready for delivery, cannot be edited.
    /// </summary>
    Confirmed = 1,

    /// <summary>
    /// In transit - delivery in progress (En cours).
    /// </summary>
    InTransit = 2,

    /// <summary>
    /// Delivered - delivery completed and validated.
    /// </summary>
    Delivered = 3,

    /// <summary>
    /// Partially delivered - some items delivered, others pending or rejected.
    /// </summary>
    PartiallyDelivered = 4,

    /// <summary>
    /// Delivery failed - could not complete delivery.
    /// </summary>
    Failed = 5,

    /// <summary>
    /// Invoiced - an invoice has been generated for this delivery.
    /// </summary>
    Invoiced = 6,

    /// <summary>
    /// Cancelled - delivery note voided.
    /// </summary>
    Cancelled = 7,

    /// <summary>
    /// Refused - all items rejected by the client during delivery.
    /// </summary>
    Refused = 8
}

public static class DeliveryNoteStatusExtensions
{
    public static bool CanBeEdited(this DeliveryNoteStatus status) => 
        status == DeliveryNoteStatus.Draft;

    public static bool CanBeConfirmed(this DeliveryNoteStatus status) => 
        status == DeliveryNoteStatus.Draft;

    public static bool CanStartDelivery(this DeliveryNoteStatus status) => 
        status == DeliveryNoteStatus.Confirmed;

    public static bool CanBeDelivered(this DeliveryNoteStatus status) => 
        status is DeliveryNoteStatus.InTransit or DeliveryNoteStatus.Confirmed;

    public static bool CanBeInvoiced(this DeliveryNoteStatus status) => 
        status is DeliveryNoteStatus.Delivered or DeliveryNoteStatus.PartiallyDelivered;

    public static bool CanBeCancelled(this DeliveryNoteStatus status) => 
        status is DeliveryNoteStatus.Draft or DeliveryNoteStatus.Confirmed;

    public static bool IsFinalized(this DeliveryNoteStatus status) => 
        status is DeliveryNoteStatus.Invoiced or DeliveryNoteStatus.Cancelled;

    /// <summary>
    /// Returns true if the delivery note is locked (immutable) — no quantity or price changes allowed.
    /// </summary>
    public static bool IsLocked(this DeliveryNoteStatus status) =>
        status is DeliveryNoteStatus.Delivered 
            or DeliveryNoteStatus.PartiallyDelivered
            or DeliveryNoteStatus.Invoiced 
            or DeliveryNoteStatus.Refused
            or DeliveryNoteStatus.Cancelled;

    public static string ToDisplayString(this DeliveryNoteStatus status) => status switch
    {
        DeliveryNoteStatus.Draft => "Brouillon",
        DeliveryNoteStatus.Confirmed => "Confirmé",
        DeliveryNoteStatus.InTransit => "En cours",
        DeliveryNoteStatus.Delivered => "Livré",
        DeliveryNoteStatus.PartiallyDelivered => "Partiellement livré",
        DeliveryNoteStatus.Failed => "Échec livraison",
        DeliveryNoteStatus.Invoiced => "Facturé",
        DeliveryNoteStatus.Cancelled => "Annulé",
        DeliveryNoteStatus.Refused => "Refusé",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    public static string ToCssClass(this DeliveryNoteStatus status) => status switch
    {
        DeliveryNoteStatus.Draft => "status-draft",
        DeliveryNoteStatus.Confirmed => "status-confirmed",
        DeliveryNoteStatus.InTransit => "status-transit",
        DeliveryNoteStatus.Delivered => "status-delivered",
        DeliveryNoteStatus.PartiallyDelivered => "status-partial",
        DeliveryNoteStatus.Failed => "status-failed",
        DeliveryNoteStatus.Invoiced => "status-invoiced",
        DeliveryNoteStatus.Cancelled => "status-cancelled",
        DeliveryNoteStatus.Refused => "status-refused",
        _ => "status-unknown"
    };
}
