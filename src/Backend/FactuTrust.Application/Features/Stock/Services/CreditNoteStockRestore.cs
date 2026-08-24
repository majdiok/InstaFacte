using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.Stock.Services;

/// <summary>
/// Restores stock for a credit note in the same unit of work as validation.
/// When any line is live-tracked (lot/serial/FIFO), all stock-managed lines are restored
/// here so the event handler's "Avoir {n}" skip cannot drop CMUP quantity.
/// </summary>
public static class CreditNoteStockRestore
{
    public static async Task<Result> RestoreAsync(
        Invoice creditNote,
        IInvoiceRepository invoices,
        IProductRepository products,
        IWarehouseRepository warehouses,
        IStockMovementRepository movements,
        IStockItemRepository stockItems,
        ITrackedDocumentStockService trackedStock,
        CancellationToken cancellationToken)
    {
        var candidates = new List<InvoiceLine>();
        var anyTracked = false;
        foreach (var line in creditNote.Lines)
        {
            if (!line.ProductId.HasValue)
                continue;

            var product = await products.GetByIdAsync(line.ProductId.Value, cancellationToken);
            if (product is null || !product.IsStockManaged)
                continue;

            if (trackedStock.IsLiveTracked(product))
                anyTracked = true;
            candidates.Add(line);
        }

        if (!anyTracked)
            return Result.Success();

        Warehouse? warehouse = null;
        if (creditNote.WarehouseId.HasValue)
            warehouse = await warehouses.GetByIdAsync(creditNote.WarehouseId.Value, cancellationToken);
        warehouse ??= await warehouses.GetDefaultAsync(cancellationToken);
        if (warehouse is null)
            return Result.Failure(Error.Validation("Warehouse",
                "Un entrepôt est obligatoire pour valider un avoir contenant des articles suivis."));

        Invoice? original = null;
        if (creditNote.LinkedInvoiceId.HasValue)
            original = await invoices.GetByIdWithLinesAsync(creditNote.LinkedInvoiceId.Value, cancellationToken);

        var originalExits = original is not null
            ? await movements.GetByReferenceAsync($"Facture {original.Number.Value}", cancellationToken)
            : Array.Empty<StockMovement>();

        var restoreLines = new List<TrackedDocumentLine>();
        foreach (var line in candidates)
        {
            IReadOnlyList<StockAllocationInput>? allocations = null;
            decimal? unitCost = null;
            var stockItem = await stockItems.GetByProductAndWarehouseAsync(
                line.ProductId!.Value, warehouse.Id, cancellationToken);
            if (stockItem is not null)
            {
                var sliced = StockValuationRestore.FromExitMovements(
                    originalExits.Where(m => m.StockItemId == stockItem.Id),
                    line.Quantity,
                    restoreSameWarehouse: true);
                if (sliced.Count > 0)
                {
                    allocations = sliced;
                    unitCost = sliced[0].UnitCost;
                }
            }

            restoreLines.Add(new TrackedDocumentLine(
                line.ProductId.Value,
                line.Quantity,
                line.Id,
                StockDocumentKind.CreditNote,
                allocations,
                unitCost));
        }

        return await trackedStock.ApplyEntriesAsync(
            warehouse.Id,
            $"Avoir {creditNote.Number.Value}",
            MovementReason.CustomerReturn,
            restoreLines,
            cancellationToken,
            includeUntracked: true);
    }
}
