using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Represents a stock movement (entry, exit, or adjustment).
/// This is an immutable record for audit trail purposes.
/// </summary>
public sealed class StockMovement : Entity
{
    public Guid StockItemId { get; private set; }
    public MovementType Type { get; private set; }
    public MovementReason Reason { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitCost { get; private set; }
    public decimal BalanceAfter { get; private set; }
    public string? Reference { get; private set; }
    public string? Notes { get; private set; }
    public DateTime OccurredAt { get; private set; }

    private StockMovement() { }

    internal static StockMovement Create(
        Guid stockItemId,
        MovementType type,
        MovementReason reason,
        decimal quantity,
        decimal unitCost,
        decimal balanceAfter,
        string? reference = null,
        string? notes = null)
    {
        // Use explicit constructor call to ensure Entity base class generates the Id
        var movement = new StockMovement();
        movement.StockItemId = stockItemId;
        movement.Type = type;
        movement.Reason = reason;
        movement.Quantity = quantity;
        movement.UnitCost = unitCost;
        movement.BalanceAfter = balanceAfter;
        movement.Reference = reference?.Trim();
        movement.Notes = notes?.Trim();
        movement.OccurredAt = DateTime.UtcNow;
        return movement;
    }
}
