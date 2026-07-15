using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Events;

/// <summary>
/// Raised when a physical inventory is started.
/// </summary>
public sealed class InventoryStartedEvent : DomainEvent
{
    public Guid InventoryId { get; }
    public Guid WarehouseId { get; }
    public int ProductCount { get; }

    public InventoryStartedEvent(Guid inventoryId, Guid warehouseId, int productCount)
    {
        InventoryId = inventoryId;
        WarehouseId = warehouseId;
        ProductCount = productCount;
    }
}

/// <summary>
/// Raised when a physical inventory is validated.
/// This triggers stock adjustments for all products with discrepancies.
/// </summary>
public sealed class InventoryValidatedEvent : DomainEvent
{
    public Guid InventoryId { get; }
    public Guid WarehouseId { get; }
    public int TotalProducts { get; }
    public int ProductsWithDifference { get; }
    public IReadOnlyList<InventoryAdjustmentItem> Adjustments { get; }

    public InventoryValidatedEvent(
        Guid inventoryId,
        Guid warehouseId,
        int totalProducts,
        int productsWithDifference,
        IReadOnlyList<InventoryAdjustmentItem> adjustments)
    {
        InventoryId = inventoryId;
        WarehouseId = warehouseId;
        TotalProducts = totalProducts;
        ProductsWithDifference = productsWithDifference;
        Adjustments = adjustments;
    }
}

/// <summary>
/// Represents a stock adjustment to be made from inventory validation.
/// </summary>
public sealed record InventoryAdjustmentItem(
    Guid ProductId,
    string ProductName,
    decimal PreviousQuantity,
    decimal NewQuantity,
    decimal Difference);

/// <summary>
/// Raised when a physical inventory is cancelled.
/// </summary>
public sealed class InventoryCancelledEvent : DomainEvent
{
    public Guid InventoryId { get; }

    public InventoryCancelledEvent(Guid inventoryId)
    {
        InventoryId = inventoryId;
    }
}
