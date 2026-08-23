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
    Delivery = 9,

    /// <summary>
    /// Internal consumption (samples used in-house, production consumption, etc.).
    /// Append-only: values are persisted as integers.
    /// </summary>
    InternalUse = 10,

    /// <summary>
    /// Gift or commercial sample leaving stock.
    /// </summary>
    GiftOrSample = 11,

    /// <summary>
    /// Found goods / miscellaneous entry not covered by purchase, return, or initial stock.
    /// </summary>
    FoundOrOther = 12
}

public static class MovementReasonExtensions
{
    public static string ToDisplayString(this MovementReason reason) => reason switch
    {
        MovementReason.Purchase => "Achat",
        MovementReason.Sale => "Vente",
        MovementReason.CustomerReturn => "Retour Client",
        MovementReason.SupplierReturn => "Retour Fournisseur",
        MovementReason.InventoryAdjustment => "Ajustement",
        MovementReason.Damage => "Dommage/Perte",
        MovementReason.Transfer => "Transfert",
        MovementReason.InitialStock => "Stock Initial",
        MovementReason.Delivery => "Livraison",
        MovementReason.InternalUse => "Consommation interne",
        MovementReason.GiftOrSample => "Don / échantillon",
        MovementReason.FoundOrOther => "Trouvé / autre",
        _ => reason.ToString()
    };
}
