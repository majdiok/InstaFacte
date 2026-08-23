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

    /// <summary>
    /// Quantité demandée qui n'a PAS pu être honorée, faute de stock disponible.
    /// <c>null</c> en fonctionnement normal.
    ///
    /// Une vente n'est jamais bloquée par une rupture : la sortie est limitée au stock
    /// disponible. Sans cette colonne, l'écart entre le vendu et le sorti restait invisible et
    /// donc irréconciliable — il ne vivait que dans un avertissement de journal applicatif.
    /// </summary>
    public decimal? ShortfallQuantity { get; private set; }

    /// <summary>Vrai si ce mouvement traduit une rupture partiellement honorée.</summary>
    public bool HasShortfall => ShortfallQuantity is > 0;

    public Guid? ProductLotId { get; private set; }
    public Guid? SerialId { get; private set; }
    public Guid? ValuationLayerId { get; private set; }

    private StockMovement() { }

    internal static StockMovement Create(
        Guid stockItemId,
        MovementType type,
        MovementReason reason,
        decimal quantity,
        decimal unitCost,
        decimal balanceAfter,
        string? reference = null,
        string? notes = null,
        decimal? shortfallQuantity = null,
        Guid? productLotId = null,
        Guid? serialId = null,
        Guid? valuationLayerId = null)
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
        movement.ShortfallQuantity = shortfallQuantity is > 0 ? shortfallQuantity : null;
        movement.ProductLotId = productLotId;
        movement.SerialId = serialId;
        movement.ValuationLayerId = valuationLayerId;
        return movement;
    }
}
