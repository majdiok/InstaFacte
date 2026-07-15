using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Events;

/// <summary>
/// Raised when a new delivery note is created.
/// </summary>
public sealed class DeliveryNoteCreatedEvent : DomainEvent
{
    public Guid DeliveryNoteId { get; }
    public string DeliveryNoteNumber { get; }
    public Guid ClientId { get; }

    public DeliveryNoteCreatedEvent(Guid deliveryNoteId, string deliveryNoteNumber, Guid clientId)
    {
        DeliveryNoteId = deliveryNoteId;
        DeliveryNoteNumber = deliveryNoteNumber;
        ClientId = clientId;
    }
}

/// <summary>
/// Raised when a delivery note is confirmed and ready for delivery.
/// </summary>
public sealed class DeliveryNoteConfirmedEvent : DomainEvent
{
    public Guid DeliveryNoteId { get; }
    public string DeliveryNoteNumber { get; }

    public DeliveryNoteConfirmedEvent(Guid deliveryNoteId, string deliveryNoteNumber)
    {
        DeliveryNoteId = deliveryNoteId;
        DeliveryNoteNumber = deliveryNoteNumber;
    }
}

/// <summary>
/// Raised when a delivery note starts transit.
/// </summary>
public sealed class DeliveryNoteInTransitEvent : DomainEvent
{
    public Guid DeliveryNoteId { get; }
    public string DeliveryNoteNumber { get; }

    public DeliveryNoteInTransitEvent(Guid deliveryNoteId, string deliveryNoteNumber)
    {
        DeliveryNoteId = deliveryNoteId;
        DeliveryNoteNumber = deliveryNoteNumber;
    }
}

/// <summary>
/// Raised when a delivery is completed (fully or partially).
/// </summary>
public sealed class DeliveryNoteDeliveredEvent : DomainEvent
{
    public Guid DeliveryNoteId { get; }
    public string DeliveryNoteNumber { get; }
    public string RecipientName { get; }
    public bool IsPartial { get; }

    public DeliveryNoteDeliveredEvent(Guid deliveryNoteId, string deliveryNoteNumber, string recipientName, bool isPartial)
    {
        DeliveryNoteId = deliveryNoteId;
        DeliveryNoteNumber = deliveryNoteNumber;
        RecipientName = recipientName;
        IsPartial = isPartial;
    }
}

/// <summary>
/// Raised when a delivery fails.
/// </summary>
public sealed class DeliveryNoteFailedEvent : DomainEvent
{
    public Guid DeliveryNoteId { get; }
    public string DeliveryNoteNumber { get; }
    public string FailureReason { get; }

    public DeliveryNoteFailedEvent(Guid deliveryNoteId, string deliveryNoteNumber, string failureReason)
    {
        DeliveryNoteId = deliveryNoteId;
        DeliveryNoteNumber = deliveryNoteNumber;
        FailureReason = failureReason;
    }
}

/// <summary>
/// Raised when a delivery note is cancelled.
/// </summary>
public sealed class DeliveryNoteCancelledEvent : DomainEvent
{
    public Guid DeliveryNoteId { get; }
    public string DeliveryNoteNumber { get; }
    public string CancellationReason { get; }

    public DeliveryNoteCancelledEvent(Guid deliveryNoteId, string deliveryNoteNumber, string cancellationReason)
    {
        DeliveryNoteId = deliveryNoteId;
        DeliveryNoteNumber = deliveryNoteNumber;
        CancellationReason = cancellationReason;
    }
}

/// <summary>
/// Raised when a delivery note is converted to an invoice.
/// </summary>
public sealed class DeliveryNoteInvoicedEvent : DomainEvent
{
    public Guid DeliveryNoteId { get; }
    public string DeliveryNoteNumber { get; }
    public Guid InvoiceId { get; }
    public string InvoiceNumber { get; }

    public DeliveryNoteInvoicedEvent(Guid deliveryNoteId, string deliveryNoteNumber, Guid invoiceId, string invoiceNumber)
    {
        DeliveryNoteId = deliveryNoteId;
        DeliveryNoteNumber = deliveryNoteNumber;
        InvoiceId = invoiceId;
        InvoiceNumber = invoiceNumber;
    }
}
