using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Events;

public sealed class PurchaseReceiptValidatedEvent : DomainEvent
{
    public Guid PurchaseReceiptId { get; }
    public string Number { get; }
    public Guid? PurchaseOrderId { get; }
    public int LinesReceived { get; }

    public PurchaseReceiptValidatedEvent(
        Guid purchaseReceiptId,
        string number,
        Guid? purchaseOrderId,
        int linesReceived)
    {
        PurchaseReceiptId = purchaseReceiptId;
        Number = number;
        PurchaseOrderId = purchaseOrderId;
        LinesReceived = linesReceived;
    }
}

public sealed class PurchaseReceiptCancelledEvent : DomainEvent
{
    public Guid PurchaseReceiptId { get; }
    public string Number { get; }
    public string Reason { get; }
    public bool WasValidated { get; }

    public PurchaseReceiptCancelledEvent(
        Guid purchaseReceiptId,
        string number,
        string reason,
        bool wasValidated)
    {
        PurchaseReceiptId = purchaseReceiptId;
        Number = number;
        Reason = reason;
        WasValidated = wasValidated;
    }
}
