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

    public PurchaseGoodsReceptionService(
        IStockItemRepository stockItemRepository,
        IStockMovementRepository stockMovementRepository,
        IProductRepository productRepository)
    {
        _stockItemRepository = stockItemRepository;
        _stockMovementRepository = stockMovementRepository;
        _productRepository = productRepository;
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
            return Result.Success(); // idempotent

        foreach (var line in lines)
        {
            if (line.Quantity <= 0)
                continue;

            var product = await _productRepository.GetByIdAsync(line.ProductId, cancellationToken);
            if (product is null || !product.IsStockManaged)
                continue;

            var stockItem = await _stockItemRepository.GetByProductAndWarehouseAsync(
                line.ProductId, warehouse.Id, cancellationToken);

            if (stockItem is null)
            {
                var createResult = StockItem.Create(line.ProductId, warehouse.Id);
                if (createResult.IsFailure)
                    return Result.Failure(createResult.Error);

                stockItem = createResult.Value;
                var entryResult = stockItem.RecordEntry(
                    line.Quantity,
                    line.UnitPriceAmount,
                    MovementReason.Purchase,
                    reference: stockReference,
                    notes: notes);

                if (entryResult.IsFailure)
                    return entryResult;

                await _stockItemRepository.AddAsync(stockItem, cancellationToken);
            }
            else
            {
                var entryResult = stockItem.RecordEntry(
                    line.Quantity,
                    line.UnitPriceAmount,
                    MovementReason.Purchase,
                    reference: stockReference,
                    notes: notes);

                if (entryResult.IsFailure)
                    return entryResult;

                await _stockItemRepository.UpdateAsync(stockItem, cancellationToken);
            }

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
            return Result.Success(); // already reversed

        foreach (var line in lines)
        {
            if (line.Quantity <= 0)
                continue;

            var product = await _productRepository.GetByIdAsync(line.ProductId, cancellationToken);
            if (product is null || !product.IsStockManaged)
                continue;

            var stockItem = await _stockItemRepository.GetByProductAndWarehouseAsync(
                line.ProductId, warehouse.Id, cancellationToken);

            if (stockItem is null)
                continue;

            var exitResult = stockItem.RecordExit(
                line.Quantity,
                MovementReason.SupplierReturn,
                reference: reverseReference,
                notes: notes);

            if (exitResult.IsFailure)
                return exitResult;

            await _stockItemRepository.UpdateAsync(stockItem, cancellationToken);
        }

        return Result.Success();
    }
}
