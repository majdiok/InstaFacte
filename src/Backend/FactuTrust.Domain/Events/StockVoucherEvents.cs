using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Events;

public sealed class StockVoucherCreatedEvent : DomainEvent
{
    public Guid StockVoucherId { get; }
    public string Number { get; }
    public StockVoucherKind Kind { get; }

    public StockVoucherCreatedEvent(Guid stockVoucherId, string number, StockVoucherKind kind)
    {
        StockVoucherId = stockVoucherId;
        Number = number;
        Kind = kind;
    }
}

public sealed class StockVoucherValidatedEvent : DomainEvent
{
    public Guid StockVoucherId { get; }
    public string Number { get; }
    public StockVoucherKind Kind { get; }
    public Guid WarehouseId { get; }
    public int LineCount { get; }

    public StockVoucherValidatedEvent(
        Guid stockVoucherId,
        string number,
        StockVoucherKind kind,
        Guid warehouseId,
        int lineCount)
    {
        StockVoucherId = stockVoucherId;
        Number = number;
        Kind = kind;
        WarehouseId = warehouseId;
        LineCount = lineCount;
    }
}

public sealed class StockVoucherCancelledEvent : DomainEvent
{
    public Guid StockVoucherId { get; }
    public string Number { get; }
    public string Reason { get; }
    public bool WasValidated { get; }

    public StockVoucherCancelledEvent(Guid stockVoucherId, string number, string reason, bool wasValidated)
    {
        StockVoucherId = stockVoucherId;
        Number = number;
        Reason = reason;
        WasValidated = wasValidated;
    }
}
