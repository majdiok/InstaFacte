using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Infrastructure.Services;

public sealed class PurchaseGoodsReceptionService : IPurchaseGoodsReceptionService
{
    private readonly IStockItemRepository _stockItemRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IProductRepository _productRepository;
    private readonly IStockMutationService _mutation;

    public PurchaseGoodsReceptionService(
        IStockItemRepository stockItemRepository,
        IStockMovementRepository stockMovementRepository,
        IProductRepository productRepository,
        IStockMutationService mutation)
    {
        _stockItemRepository = stockItemRepository;
        _stockMovementRepository = stockMovementRepository;
        _productRepository = productRepository;
        _mutation = mutation;
    }

    public async Task<Result> ApplyStockEntriesAsync(
        Warehouse warehouse,
        IReadOnlyList<PurchaseReceptionStockLine> lines,
        string stockReference,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        if (warehouse is null)
            return Result.Failure(Error.Validation("Warehouse", "L'entrepôt est obligatoire"));

        if (string.IsNullOrWhiteSpace(stockReference))
            return Result.Failure(Error.Validation("Reference", "La référence de mouvement est obligatoire"));

        var existing = await _stockMovementRepository.GetByReferenceAsync(stockReference, cancellationToken);
        if (existing.Any(m => m.Type == MovementType.Entry))
            return Result.Success();

        foreach (var line in lines)
        {
            if (line.Quantity <= 0)
                continue;

            var product = await _productRepository.GetByIdAsync(line.ProductId, cancellationToken);
            if (product is null || !product.IsStockManaged)
                continue;

            var kind = stockReference.StartsWith("BR ", StringComparison.Ordinal)
                ? StockDocumentKind.PurchaseReceipt
                : StockDocumentKind.PurchaseOrderReception;

            var result = await _mutation.ApplyAsync(new StockMutationRequest
            {
                ProductId = line.ProductId,
                WarehouseId = warehouse.Id,
                Kind = StockMutationKind.Entry,
                Quantity = line.Quantity,
                UnitCost = line.UnitPriceAmount,
                Reason = MovementReason.Purchase,
                Reference = stockReference,
                Notes = notes,
                DocumentLineId = line.DocumentLineId,
                DocumentKind = line.DocumentLineId.HasValue ? kind : null,
                Allocations = line.Allocations
            }, cancellationToken);

            if (result.IsFailure)
                return Result.Failure(result.Error);

            product.UpdateLastPurchasePrice(Money.Create(line.UnitPriceAmount, line.Currency));
            await _productRepository.UpdateAsync(product, cancellationToken);
        }

        return Result.Success();
    }

    public async Task<Result> ReverseStockEntriesAsync(
        Warehouse warehouse,
        IReadOnlyList<PurchaseReceptionStockLine> lines,
        string stockReference,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        if (warehouse is null)
            return Result.Failure(Error.Validation("Warehouse", "L'entrepôt est obligatoire"));

        var reverseReference = $"ANNUL {stockReference}";
        var existing = await _stockMovementRepository.GetByReferenceAsync(reverseReference, cancellationToken);
        if (existing.Any(m => m.Type == MovementType.Exit))
            return Result.Success();

        var original = await _stockMovementRepository.GetByReferenceAsync(stockReference, cancellationToken);
        var originalEntries = original.Where(m => m.Type == MovementType.Entry).ToList();

        if (originalEntries.Count > 0 && originalEntries.Any(m => m.ProductLotId.HasValue || m.SerialId.HasValue))
        {
            foreach (var movement in originalEntries)
            {
                var stockItem = await _stockItemRepository.GetByIdAsync(movement.StockItemId, cancellationToken);
                if (stockItem is null)
                    continue;

                var qty = Math.Abs(movement.Quantity);
                var result = await _mutation.ApplyAsync(new StockMutationRequest
                {
                    ProductId = stockItem.ProductId,
                    WarehouseId = warehouse.Id,
                    Kind = StockMutationKind.Exit,
                    Quantity = qty,
                    UnitCost = movement.UnitCost,
                    Reason = MovementReason.SupplierReturn,
                    Reference = reverseReference,
                    Notes = notes,
                    Allocations = new[]
                    {
                        new StockAllocationInput(qty, ProductLotId: movement.ProductLotId, SerialId: movement.SerialId)
                    }
                }, cancellationToken);
                if (result.IsFailure)
                    return Result.Failure(result.Error);
            }

            return Result.Success();
        }

        foreach (var line in lines)
        {
            if (line.Quantity <= 0)
                continue;

            var product = await _productRepository.GetByIdAsync(line.ProductId, cancellationToken);
            if (product is null || !product.IsStockManaged)
                continue;

            var result = await _mutation.ApplyAsync(new StockMutationRequest
            {
                ProductId = line.ProductId,
                WarehouseId = warehouse.Id,
                Kind = StockMutationKind.Exit,
                Quantity = line.Quantity,
                Reason = MovementReason.SupplierReturn,
                Reference = reverseReference,
                Notes = notes
            }, cancellationToken);

            if (result.IsFailure)
                return Result.Failure(result.Error);
        }

        return Result.Success();
    }
}
