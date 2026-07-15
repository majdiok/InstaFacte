using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Events;

public sealed class StockTransferCreatedEvent : DomainEvent
{
    public Guid StockTransferId { get; }
    public string Number { get; }
    public Guid SourceWarehouseId { get; }
    public Guid DestinationWarehouseId { get; }

    public StockTransferCreatedEvent(Guid stockTransferId, string number, Guid sourceWarehouseId, Guid destinationWarehouseId)
    {
        StockTransferId = stockTransferId;
        Number = number;
        SourceWarehouseId = sourceWarehouseId;
        DestinationWarehouseId = destinationWarehouseId;
    }
}

public sealed class StockTransferConfirmedEvent : DomainEvent
{
    public Guid StockTransferId { get; }
    public string Number { get; }

    public StockTransferConfirmedEvent(Guid stockTransferId, string number)
    {
        StockTransferId = stockTransferId;
        Number = number;
    }
}

public sealed class StockTransferCompletedEvent : DomainEvent
{
    public Guid StockTransferId { get; }
    public string Number { get; }
    public Guid SourceWarehouseId { get; }
    public Guid DestinationWarehouseId { get; }
    public int LineCount { get; }

    public StockTransferCompletedEvent(Guid stockTransferId, string number, Guid sourceWarehouseId, Guid destinationWarehouseId, int lineCount)
    {
        StockTransferId = stockTransferId;
        Number = number;
        SourceWarehouseId = sourceWarehouseId;
        DestinationWarehouseId = destinationWarehouseId;
        LineCount = lineCount;
    }
}

public sealed class StockTransferCancelledEvent : DomainEvent
{
    public Guid StockTransferId { get; }
    public string Number { get; }
    public string Reason { get; }

    public StockTransferCancelledEvent(Guid stockTransferId, string number, string reason)
    {
        StockTransferId = stockTransferId;
        Number = number;
        Reason = reason;
    }
}
