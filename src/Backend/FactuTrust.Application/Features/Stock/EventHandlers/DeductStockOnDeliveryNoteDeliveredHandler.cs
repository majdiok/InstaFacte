using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.Stock.Services;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Application.Features.Stock.EventHandlers;

/// <summary>
/// Handles DeliveryNoteDeliveredEvent to automatically deduct stock when a delivery is completed.
/// For each line with DeliveredQuantity > 0, decrements stock from the default warehouse.
/// </summary>
public sealed class DeductStockOnDeliveryNoteDeliveredHandler : INotificationHandler<DeliveryNoteDeliveredEvent>
{
    private readonly IDeliveryNoteRepository _deliveryNoteRepository;
    private readonly IStockItemRepository _stockItemRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IProductRepository _productRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IStockMutationService _mutation;
    private readonly ITrackedDocumentStockService _trackedStock;
    private readonly ILogger<DeductStockOnDeliveryNoteDeliveredHandler> _logger;

    public DeductStockOnDeliveryNoteDeliveredHandler(
        IDeliveryNoteRepository deliveryNoteRepository,
        IStockItemRepository stockItemRepository,
        IWarehouseRepository warehouseRepository,
        IProductRepository productRepository,
        IStockMovementRepository stockMovementRepository,
        IStockMutationService mutation,
        ITrackedDocumentStockService trackedStock,
        ILogger<DeductStockOnDeliveryNoteDeliveredHandler> logger)
    {
        _deliveryNoteRepository = deliveryNoteRepository;
        _stockItemRepository = stockItemRepository;
        _warehouseRepository = warehouseRepository;
        _productRepository = productRepository;
        _stockMovementRepository = stockMovementRepository;
        _mutation = mutation;
        _trackedStock = trackedStock;
        _logger = logger;
    }

    public async Task Handle(DeliveryNoteDeliveredEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing stock deduction for delivery note {DeliveryNoteId} ({DeliveryNoteNumber})",
            notification.DeliveryNoteId, notification.DeliveryNoteNumber);

        var reference = $"BL {notification.DeliveryNoteNumber}";

        // Idempotency: skip if movements for this reference already exist
        var existingMovements = await _stockMovementRepository.GetByReferenceAsync(reference, cancellationToken);
        if (existingMovements.Count > 0)
        {
            _logger.LogInformation(
                "Stock deduction for delivery note {DeliveryNoteNumber} already processed ({Count} movements found) — skipping",
                notification.DeliveryNoteNumber, existingMovements.Count);
            return;
        }

        // 1. Load delivery note with lines
        var deliveryNote = await _deliveryNoteRepository.GetByIdWithLinesAsync(
            notification.DeliveryNoteId, cancellationToken);

        if (deliveryNote is null)
        {
            _logger.LogWarning("Delivery note {DeliveryNoteId} not found for stock deduction",
                notification.DeliveryNoteId);
            return;
        }

        // 2. Resolve warehouse: delivery-note-specific or default fallback
        Warehouse? targetWarehouse = null;
        if (deliveryNote.WarehouseId.HasValue)
            targetWarehouse = await _warehouseRepository.GetByIdAsync(deliveryNote.WarehouseId.Value, cancellationToken);

        targetWarehouse ??= await _warehouseRepository.GetDefaultAsync(cancellationToken);

        if (targetWarehouse is null)
        {
            _logger.LogWarning(
                "No warehouse configured — skipping stock deduction for delivery note {DeliveryNoteId}",
                notification.DeliveryNoteId);
            return;
        }

        // 3. For each line with delivered quantity, decrement stock
        foreach (var line in deliveryNote.Lines)
        {
            if (line.DeliveredQuantity <= 0)
                continue;

            // Check if product is stock-managed
            var product = await _productRepository.GetByIdAsync(line.ProductId, cancellationToken);
            if (product is null || !product.IsStockManaged)
            {
                _logger.LogDebug(
                    "Skipping stock deduction for product {ProductId} (not stock-managed)",
                    line.ProductId);
                continue;
            }

            if (_trackedStock.IsLiveTracked(product))
            {
                _logger.LogInformation(
                    "Skipping event-handler deduction for tracked product {ProductId} on BL {DeliveryNoteNumber}",
                    line.ProductId, notification.DeliveryNoteNumber);
                continue;
            }

            var exitResult = await _mutation.ApplyAsync(new StockMutationRequest
            {
                ProductId = line.ProductId,
                WarehouseId = targetWarehouse.Id,
                Kind = StockMutationKind.Exit,
                Quantity = line.DeliveredQuantity,
                Reason = MovementReason.Delivery,
                Reference = reference,
                Notes = $"Livraison - Ligne {line.LineNumber}"
            }, cancellationToken);

            if (exitResult.IsFailure)
            {
                var stockItem = await _stockItemRepository.GetByProductAndWarehouseAsync(
                    line.ProductId, targetWarehouse.Id, cancellationToken);
                _logger.LogWarning(
                    "Stock deduction failed for product {ProductId}, BL {DeliveryNoteNumber}: {Error}. " +
                    "Current stock: {CurrentStock}, Requested: {Requested}",
                    line.ProductId,
                    notification.DeliveryNoteNumber,
                    exitResult.Error.Description,
                    stockItem?.QuantityOnHand ?? 0,
                    line.DeliveredQuantity);
                continue;
            }

            _logger.LogInformation(
                "Stock deducted for product {ProductId}: {Quantity} units (BL {DeliveryNoteNumber}). New balance: {NewBalance}",
                line.ProductId, line.DeliveredQuantity, notification.DeliveryNoteNumber, exitResult.Value.QuantityOnHand);
        }

        _logger.LogInformation(
            "Completed stock deduction processing for delivery note {DeliveryNoteNumber}",
            notification.DeliveryNoteNumber);
    }
}
