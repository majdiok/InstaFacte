using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Events;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Represents the stock level of a product in a specific warehouse.
/// This is the main aggregate for stock management.
/// </summary>
public sealed class StockItem : AggregateRoot
{
    private readonly List<StockMovement> _movements = new();

    public Guid ProductId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public decimal QuantityOnHand { get; private set; }
    public decimal QuantityReserved { get; private set; }
    public decimal QuantityAvailable => QuantityOnHand - QuantityReserved;
    public decimal MinimumStock { get; private set; }
    public decimal? MaximumStock { get; private set; }
    public decimal AverageCost { get; private set; }
    public decimal StockValue => QuantityOnHand * AverageCost;
    public bool IsActive { get; private set; }

    public IReadOnlyCollection<StockMovement> Movements => _movements.AsReadOnly();

    private StockItem() { }

    public static Result<StockItem> Create(
        Guid productId,
        Guid warehouseId,
        decimal minimumStock = 0,
        decimal? maximumStock = null)
    {
        if (productId == Guid.Empty)
            return Result.Failure<StockItem>(Error.Validation("ProductId", "L'identifiant du produit est obligatoire"));

        if (warehouseId == Guid.Empty)
            return Result.Failure<StockItem>(Error.Validation("WarehouseId", "L'identifiant de l'entrepôt est obligatoire"));

        if (minimumStock < 0)
            return Result.Failure<StockItem>(Error.Validation("MinimumStock", "Le stock minimum ne peut pas être négatif"));

        if (maximumStock.HasValue && maximumStock.Value < minimumStock)
            return Result.Failure<StockItem>(Error.Validation("MaximumStock", "Le stock maximum doit être supérieur au stock minimum"));

        var stockItem = new StockItem
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            QuantityOnHand = 0,
            QuantityReserved = 0,
            MinimumStock = minimumStock,
            MaximumStock = maximumStock,
            AverageCost = 0,
            IsActive = true
        };

        return Result.Success(stockItem);
    }

    /// <summary>
    /// Records a stock entry (purchase, return, initial stock).
    /// Updates the weighted average cost (CMUP).
    /// </summary>
    public Result RecordEntry(
        decimal quantity,
        decimal unitCost,
        MovementReason reason,
        string? reference = null,
        string? notes = null)
    {
        if (quantity <= 0)
            return Result.Failure(Error.Validation("Quantity", "La quantité doit être positive"));

        if (unitCost < 0)
            return Result.Failure(Error.Validation("UnitCost", "Le coût unitaire ne peut pas être négatif"));

        // Calculate new weighted average cost (CMUP)
        var totalCurrentValue = QuantityOnHand * AverageCost;
        var entryValue = quantity * unitCost;
        var newTotalQuantity = QuantityOnHand + quantity;

        if (newTotalQuantity > 0)
        {
            AverageCost = (totalCurrentValue + entryValue) / newTotalQuantity;
        }

        QuantityOnHand = newTotalQuantity;

        var movement = StockMovement.Create(
            Id,
            MovementType.Entry,
            reason,
            quantity,
            unitCost,
            QuantityOnHand,
            reference,
            notes);

        _movements.Add(movement);

        AddDomainEvent(new StockMovementRecordedEvent(
            Id,
            ProductId,
            MovementType.Entry,
            reason,
            quantity,
            QuantityOnHand));

        return Result.Success();
    }

    /// <summary>
    /// Records a stock exit (sale, supplier return, damage).
    /// </summary>
    /// <param name="shortfallQuantity">
    /// Quantité demandée non honorée, lorsque l'appelant a délibérément réduit la sortie au
    /// stock disponible. Purement descriptif : n'influence aucun calcul, sert à rendre l'écart
    /// mesurable et réconciliable au lieu de le laisser dans un journal applicatif.
    /// </param>
    public Result RecordExit(
        decimal quantity,
        MovementReason reason,
        string? reference = null,
        string? notes = null,
        decimal? shortfallQuantity = null)
    {
        if (quantity <= 0)
            return Result.Failure(Error.Validation("Quantity", "La quantité doit être positive"));

        if (quantity > QuantityAvailable)
            return Result.Failure(Error.Validation("Quantity",
                $"Stock insuffisant. Disponible: {QuantityAvailable}, Demandé: {quantity}"));

        QuantityOnHand -= quantity;

        var movement = StockMovement.Create(
            Id,
            MovementType.Exit,
            reason,
            -quantity, // Negative for exits
            AverageCost,
            QuantityOnHand,
            reference,
            notes,
            shortfallQuantity);

        _movements.Add(movement);

        AddDomainEvent(new StockMovementRecordedEvent(
            Id,
            ProductId,
            MovementType.Exit,
            reason,
            quantity,
            QuantityOnHand));

        // Check for low stock alert
        if (QuantityOnHand <= MinimumStock && QuantityOnHand > 0)
        {
            AddDomainEvent(new StockLowAlertEvent(Id, ProductId, QuantityOnHand, MinimumStock));
        }
        else if (QuantityOnHand == 0)
        {
            AddDomainEvent(new StockOutOfStockEvent(Id, ProductId));
        }

        return Result.Success();
    }

    /// <summary>
    /// Records an inventory adjustment (can be positive or negative).
    /// </summary>
    public Result AdjustStock(
        decimal newQuantity,
        string? notes = null)
    {
        if (newQuantity < 0)
            return Result.Failure(Error.Validation("Quantity", "La quantité ne peut pas être négative"));

        var difference = newQuantity - QuantityOnHand;
        
        if (difference == 0)
            return Result.Success(); // No change needed

        QuantityOnHand = newQuantity;

        var movement = StockMovement.Create(
            Id,
            MovementType.Adjustment,
            MovementReason.InventoryAdjustment,
            difference,
            AverageCost,
            QuantityOnHand,
            null,
            notes);

        _movements.Add(movement);

        AddDomainEvent(new StockMovementRecordedEvent(
            Id,
            ProductId,
            MovementType.Adjustment,
            MovementReason.InventoryAdjustment,
            Math.Abs(difference),
            QuantityOnHand));

        return Result.Success();
    }

    /// <summary>
    /// Reserves stock for a pending order.
    /// </summary>
    public Result Reserve(decimal quantity)
    {
        if (quantity <= 0)
            return Result.Failure(Error.Validation("Quantity", "La quantité doit être positive"));

        if (quantity > QuantityAvailable)
            return Result.Failure(Error.Validation("Quantity",
                $"Stock disponible insuffisant pour réservation. Disponible: {QuantityAvailable}, Demandé: {quantity}"));

        QuantityReserved += quantity;
        return Result.Success();
    }

    /// <summary>
    /// Releases a previous reservation.
    /// </summary>
    public Result ReleaseReservation(decimal quantity)
    {
        if (quantity <= 0)
            return Result.Failure(Error.Validation("Quantity", "La quantité doit être positive"));

        if (quantity > QuantityReserved)
            return Result.Failure(Error.Validation("Quantity",
                $"Impossible de libérer plus que la quantité réservée. Réservé: {QuantityReserved}, Demandé: {quantity}"));

        QuantityReserved -= quantity;
        return Result.Success();
    }

    /// <summary>
    /// Updates the stock thresholds.
    /// </summary>
    public Result UpdateThresholds(decimal minimumStock, decimal? maximumStock)
    {
        if (minimumStock < 0)
            return Result.Failure(Error.Validation("MinimumStock", "Le stock minimum ne peut pas être négatif"));

        if (maximumStock.HasValue && maximumStock.Value < minimumStock)
            return Result.Failure(Error.Validation("MaximumStock", "Le stock maximum doit être supérieur au stock minimum"));

        MinimumStock = minimumStock;
        MaximumStock = maximumStock;

        return Result.Success();
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;

    /// <summary>
    /// Checks if stock is below minimum threshold.
    /// </summary>
    public bool IsLowStock => QuantityOnHand <= MinimumStock && QuantityOnHand > 0;

    /// <summary>
    /// Checks if stock is depleted.
    /// </summary>
    public bool IsOutOfStock => QuantityOnHand == 0;
}
