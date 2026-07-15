using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Events;

/// <summary>
/// Raised when a purchase order is confirmed.
/// </summary>
public sealed class PurchaseOrderConfirmedEvent : DomainEvent
{
    public Guid PurchaseOrderId { get; }
    public string Number { get; }
    public Guid SupplierId { get; }

    public PurchaseOrderConfirmedEvent(Guid purchaseOrderId, string number, Guid supplierId)
    {
        PurchaseOrderId = purchaseOrderId;
        Number = number;
        SupplierId = supplierId;
    }
}

/// <summary>
/// Raised when goods are received for a purchase order.
/// </summary>
public sealed class GoodsReceivedEvent : DomainEvent
{
    public Guid PurchaseOrderId { get; }
    public string Number { get; }
    public int LinesReceived { get; }
    public bool IsFullyReceived { get; }

    public GoodsReceivedEvent(Guid purchaseOrderId, string number, int linesReceived, bool isFullyReceived)
    {
        PurchaseOrderId = purchaseOrderId;
        Number = number;
        LinesReceived = linesReceived;
        IsFullyReceived = isFullyReceived;
    }
}

/// <summary>
/// Raised when a purchase order is cancelled.
/// </summary>
public sealed class PurchaseOrderCancelledEvent : DomainEvent
{
    public Guid PurchaseOrderId { get; }
    public string Number { get; }
    public string Reason { get; }

    public PurchaseOrderCancelledEvent(Guid purchaseOrderId, string number, string reason)
    {
        PurchaseOrderId = purchaseOrderId;
        Number = number;
        Reason = reason;
    }
}
