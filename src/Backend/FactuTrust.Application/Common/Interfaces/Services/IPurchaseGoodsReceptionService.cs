using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Services;

public sealed record PurchaseReceptionStockLine(
    Guid ProductId,
    decimal Quantity,
    decimal UnitPriceAmount,
    string Currency,
    Guid? DocumentLineId = null,
    IReadOnlyList<StockAllocationInput>? Allocations = null);

/// <summary>
/// Shared stock entry / reversal for purchase goods reception (legacy ReceiveGoods + BR validation).
/// </summary>
public interface IPurchaseGoodsReceptionService
{
    /// <summary>
    /// Creates stock entries for stock-managed products. Idempotent when movements with
    /// <paramref name="stockReference"/> already exist.
    /// </summary>
    Task<Result> ApplyStockEntriesAsync(
        Warehouse warehouse,
        IReadOnlyList<PurchaseReceptionStockLine> lines,
        string stockReference,
        string? notes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reverses stock entries previously created for <paramref name="stockReference"/>
    /// (exit movements for cancellation of a validated BR).
    /// </summary>
    Task<Result> ReverseStockEntriesAsync(
        Warehouse warehouse,
        IReadOnlyList<PurchaseReceptionStockLine> lines,
        string stockReference,
        string? notes,
        CancellationToken cancellationToken = default);
}
