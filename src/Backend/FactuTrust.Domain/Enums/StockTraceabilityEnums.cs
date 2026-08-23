namespace FactuTrust.Domain.Enums;

/// <summary>Physical traceability mode for a catalog product. Default None preserves legacy stock.</summary>
public enum TrackingMode
{
    None = 0,
    Lot = 1,
    Serial = 2
}

/// <summary>Physical picking order when auto-allocating lots on exit.</summary>
public enum PickingPolicy
{
    None = 0,
    Fefo = 1,
    FifoPhysical = 2,
    Manual = 3
}

/// <summary>Inventory costing method. Average is CMUP (legacy default).</summary>
public enum CostingMethod
{
    Average = 0,
    Fifo = 1,
    Lifo = 2
}

public enum SerialStatus
{
    InStock = 0,
    Sold = 1,
    Scrapped = 2,
    InTransit = 3
}

public enum StockDocumentKind
{
    PurchaseReceipt = 1,
    DeliveryNote = 2,
    Invoice = 3,
    StockVoucherEntry = 4,
    StockVoucherIssue = 5,
    Transfer = 6,
    Inventory = 7,
    SalesReturnNote = 8,
    CreditNote = 9,
    PurchaseOrderReception = 10,
    ManualEntry = 11,
    ManualExit = 12
}

public enum StockMutationKind
{
    Entry = 1,
    Exit = 2,
    ReleaseAndExit = 3,
    Adjust = 4
}
