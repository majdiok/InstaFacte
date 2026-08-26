using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces.Services;

public sealed record StockAllocationInput(
    decimal Quantity,
    Guid? ProductLotId = null,
    string? LotNumber = null,
    DateTime? ExpiryDate = null,
    DateTime? ManufacturedOn = null,
    Guid? SerialId = null,
    string? SerialNumber = null,
    decimal? UnitCost = null,
    DateTime? ReceivedAt = null,
    Guid? RestoreValuationLayerId = null)
{
    public bool HasTraceabilityIdentity() =>
        ProductLotId is not null
        || !string.IsNullOrWhiteSpace(LotNumber)
        || SerialId is not null
        || !string.IsNullOrWhiteSpace(SerialNumber);
}

public sealed record StockMutationRequest
{
    public required Guid ProductId { get; init; }
    public required Guid WarehouseId { get; init; }
    public required StockMutationKind Kind { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitCost { get; init; }
    public MovementReason Reason { get; init; }
    public string? Reference { get; init; }
    public string? Notes { get; init; }
    public decimal? ShortfallQuantity { get; init; }
    public decimal? ReservedQuantity { get; init; }
    public IReadOnlyList<StockAllocationInput>? Allocations { get; init; }
    public Guid? DocumentLineId { get; init; }
    public StockDocumentKind? DocumentKind { get; init; }
}

public sealed record StockMutationResult(Guid StockItemId, decimal QuantityOnHand, decimal AverageCost);

public interface IStockMutationService
{
    /// <summary>
    /// Mutates an already-tracked <see cref="StockItem"/>. Does not persist.
    /// When <paramref name="product"/> is untracked (or flags are off), delegates 1:1 to
    /// <see cref="StockItem.RecordEntry"/> / RecordExit / ReleaseAndExit / AdjustStock.
    /// </summary>
    Result Apply(
        StockItem stockItem,
        StockMutationRequest request,
        Product? product = null,
        IStockTraceabilityStore? store = null);

    /// <summary>Load/create stock item, apply, persist via the ambient tenant context.</summary>
    Task<Result<StockMutationResult>> ApplyAsync(StockMutationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates one opening FIFO/LIFO layer per warehouse with on-hand quantity, then sets the costing method.
    /// Refused when remaining layers already exist.
    /// </summary>
    Task<Result> CreateOpeningValuationLayersAsync(
        Guid productId,
        CostingMethod costingMethod,
        CancellationToken cancellationToken = default);
}

public interface IStockTraceabilityStore
{
    ProductLot? FindLotByNumber(Guid productId, string lotNumber);
    ProductLot? GetLot(Guid lotId);
    void AddLot(ProductLot lot);
    StockLotBalance? FindBalance(Guid stockItemId, Guid lotId);
    IReadOnlyList<StockLotBalance> ListBalances(Guid stockItemId);
    void AddBalance(StockLotBalance balance);
    IReadOnlyList<StockValuationLayer> ListOpenLayers(Guid stockItemId);
    StockValuationLayer? GetLayer(Guid layerId);
    void AddLayer(StockValuationLayer layer);
    ProductSerial? FindSerial(Guid productId, string serialNumber);
    ProductSerial? GetSerial(Guid serialId);
    IReadOnlyList<ProductSerial> ListInStockSerials(Guid productId, Guid warehouseId);
    void AddSerial(ProductSerial serial);
    void AddAllocation(StockDocumentAllocation allocation);
    IReadOnlyList<StockDocumentAllocation> ListAllocations(StockDocumentKind kind, Guid documentLineId);
    DateTime? GetLotFirstReceivedAt(Guid lotId);
}
