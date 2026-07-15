namespace FactuTrust.Domain.Enums;

/// <summary>
/// Type of stock movement (direction).
/// </summary>
public enum MovementType
{
    /// <summary>
    /// Stock entry (increases quantity).
    /// </summary>
    Entry = 1,

    /// <summary>
    /// Stock exit (decreases quantity).
    /// </summary>
    Exit = 2,

    /// <summary>
    /// Inventory adjustment (can be positive or negative).
    /// </summary>
    Adjustment = 3
}

/// <summary>
/// Reason for the stock movement.
/// </summary>
public enum MovementReason
{
    /// <summary>
    /// Purchase from supplier.
    /// </summary>
    Purchase = 1,

    /// <summary>
    /// Sale to customer (via invoice).
    /// </summary>
    Sale = 2,

    /// <summary>
    /// Customer return (credit note).
    /// </summary>
    CustomerReturn = 3,

    /// <summary>
    /// Return to supplier.
    /// </summary>
    SupplierReturn = 4,

    /// <summary>
    /// Inventory count adjustment.
    /// </summary>
    InventoryAdjustment = 5,

    /// <summary>
    /// Damaged or broken items.
    /// </summary>
    Damage = 6,

    /// <summary>
    /// Transfer between warehouses.
    /// </summary>
    Transfer = 7,

    /// <summary>
    /// Initial stock setup.
    /// </summary>
    InitialStock = 8,

    /// <summary>
    /// Delivery to customer (via delivery note).
    /// </summary>
    Delivery = 9
}
