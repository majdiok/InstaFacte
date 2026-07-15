using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Events;

/// <summary>
/// Raised when a stock movement is recorded.
/// </summary>
public sealed class StockMovementRecordedEvent : DomainEvent
{
    public Guid StockItemId { get; }
    public Guid ProductId { get; }
    public MovementType Type { get; }
    public MovementReason Reason { get; }
    public decimal Quantity { get; }
    public decimal NewBalance { get; }

    public StockMovementRecordedEvent(
        Guid stockItemId,
        Guid productId,
        MovementType type,
        MovementReason reason,
        decimal quantity,
        decimal newBalance)
    {
        StockItemId = stockItemId;
        ProductId = productId;
        Type = type;
        Reason = reason;
        Quantity = quantity;
        NewBalance = newBalance;
    }
}

/// <summary>
/// Raised when stock level falls below the minimum threshold.
/// </summary>
public sealed class StockLowAlertEvent : DomainEvent
{
    public Guid StockItemId { get; }
    public Guid ProductId { get; }
    public decimal CurrentQuantity { get; }
    public decimal MinimumThreshold { get; }

    public StockLowAlertEvent(
        Guid stockItemId,
        Guid productId,
        decimal currentQuantity,
        decimal minimumThreshold)
    {
        StockItemId = stockItemId;
        ProductId = productId;
        CurrentQuantity = currentQuantity;
        MinimumThreshold = minimumThreshold;
    }
}

/// <summary>
/// Raised when stock reaches zero.
/// </summary>
public sealed class StockOutOfStockEvent : DomainEvent
{
    public Guid StockItemId { get; }
    public Guid ProductId { get; }

    public StockOutOfStockEvent(Guid stockItemId, Guid productId)
    {
        StockItemId = stockItemId;
        ProductId = productId;
    }
}
