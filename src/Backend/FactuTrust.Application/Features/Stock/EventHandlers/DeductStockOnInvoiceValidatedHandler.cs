using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Application.Features.Stock.EventHandlers;

/// <summary>
/// Handles InvoiceValidatedEvent to automatically deduct stock when an invoice is validated.
/// This is the core integration point between Billing and Stock contexts.
/// </summary>
public sealed class DeductStockOnInvoiceValidatedHandler : INotificationHandler<InvoiceValidatedEvent>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IStockItemRepository _stockItemRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IProductRepository _productRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IAuditService _auditService;
    private readonly ILogger<DeductStockOnInvoiceValidatedHandler> _logger;

    public DeductStockOnInvoiceValidatedHandler(
        IInvoiceRepository invoiceRepository,
        IStockItemRepository stockItemRepository,
        IWarehouseRepository warehouseRepository,
        IProductRepository productRepository,
        IStockMovementRepository stockMovementRepository,
        IAuditService auditService,
        ILogger<DeductStockOnInvoiceValidatedHandler> logger)
    {
        _invoiceRepository = invoiceRepository;
        _stockItemRepository = stockItemRepository;
        _warehouseRepository = warehouseRepository;
        _productRepository = productRepository;
        _stockMovementRepository = stockMovementRepository;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task Handle(InvoiceValidatedEvent notification, CancellationToken cancellationToken)
    {
        // Credit notes (AVO) restore stock instead of deducting it — handled by
        // RestoreStockOnCreditNoteValidatedHandler. Short-circuit before any DB roundtrip.
        if (notification.IsCreditNote)
            return;

        _logger.LogInformation("Processing stock deduction for invoice {InvoiceId} ({InvoiceNumber})",
            notification.InvoiceId, notification.InvoiceNumber);

        var reference = $"Facture {notification.InvoiceNumber}";

        // Idempotency: skip if movements for this reference already exist
        var existingMovements = await _stockMovementRepository.GetByReferenceAsync(reference, cancellationToken);
        if (existingMovements.Count > 0)
        {
            _logger.LogInformation(
                "Stock deduction for invoice {InvoiceNumber} already processed ({Count} movements found) — skipping",
                notification.InvoiceNumber, existingMovements.Count);
            return;
        }

        // Get the invoice with lines
        var invoice = await _invoiceRepository.GetByIdWithLinesAsync(notification.InvoiceId, cancellationToken);
        if (invoice == null)
        {
            _logger.LogWarning("Invoice {InvoiceId} not found for stock deduction", notification.InvoiceId);
            return;
        }

        // Resolve warehouse: invoice-specific or default fallback
        Warehouse? targetWarehouse = null;
        if (invoice.WarehouseId.HasValue)
            targetWarehouse = await _warehouseRepository.GetByIdAsync(invoice.WarehouseId.Value, cancellationToken);

        targetWarehouse ??= await _warehouseRepository.GetDefaultAsync(cancellationToken);

        if (targetWarehouse == null)
        {
            _logger.LogWarning("No warehouse configured - skipping stock deduction for invoice {InvoiceId}", 
                notification.InvoiceId);
            return;
        }

        // Skip stock deduction if this invoice was generated from a delivery note
        // (stock was already decremented during delivery via DeductStockOnDeliveryNoteDeliveredHandler).
        // Le test porte sur la clé étrangère typée, jamais sur Reference : ce champ est une
        // chaîne libre saisissable par l'appelant, et s'y fier décrémentait le stock deux fois
        // dès qu'une référence personnalisée était fournie à la facturation du BL.
        if (invoice.SourceDeliveryNoteId.HasValue)
        {
            _logger.LogInformation(
                "Invoice {InvoiceNumber} was generated from delivery note {DeliveryNoteId} — skipping stock deduction (already done at delivery)",
                notification.InvoiceNumber, invoice.SourceDeliveryNoteId.Value);
            return;
        }

        foreach (var line in invoice.Lines)
        {
            // Get product to check if stock-managed
            var product = await _productRepository.GetByIdAsync(line.ProductId, cancellationToken);
            if (product == null || !product.IsStockManaged)
            {
                _logger.LogDebug("Skipping stock deduction for product {ProductId} (not stock-managed)", 
                    line.ProductId);
                continue;
            }

            // Get or create stock item
            var stockItem = await _stockItemRepository.GetByProductAndWarehouseAsync(
                line.ProductId, targetWarehouse.Id, cancellationToken);

            if (stockItem == null)
            {
                _logger.LogWarning("No stock item found for product {ProductId} in warehouse {WarehouseId}. Creating one.",
                    line.ProductId, targetWarehouse.Id);

                var createResult = StockItem.Create(line.ProductId, targetWarehouse.Id);
                if (createResult.IsFailure)
                {
                    _logger.LogError("Failed to create stock item for product {ProductId}: {Error}",
                        line.ProductId, createResult.Error.Description);
                    continue;
                }

                stockItem = createResult.Value;
                await _stockItemRepository.AddAsync(stockItem, cancellationToken);
            }

            var exitResult = stockItem.RecordExit(
                line.Quantity, 
                MovementReason.Sale, 
                reference, 
                $"Vente - Ligne de facture");

            if (exitResult.IsFailure)
            {
                var isInsufficientStock = exitResult.Error.Code == "Validation.Quantity"
                    && exitResult.Error.Description.Contains("Stock insuffisant", StringComparison.OrdinalIgnoreCase);
                var availableToDeduct = stockItem.QuantityAvailable;

                if (isInsufficientStock && availableToDeduct > 0)
                {
                    var shortfall = line.Quantity - availableToDeduct;

                    var partialExitResult = stockItem.RecordExit(
                        availableToDeduct,
                        MovementReason.Sale,
                        reference,
                        "Vente - Ligne de facture (déduction limitée au stock disponible)",
                        shortfallQuantity: shortfall);

                    if (partialExitResult.IsSuccess)
                    {
                        await _stockItemRepository.UpdateAsync(stockItem, cancellationToken);
                        _logger.LogInformation(
                            "Partial stock deduction for product {ProductId}, invoice {InvoiceNumber}: requested {Requested}, deducted {Deducted}. New balance: {NewBalance}",
                            line.ProductId, notification.InvoiceNumber, line.Quantity, availableToDeduct, stockItem.QuantityOnHand);

                        // Trace de premier ordre : l'écart vendu/sorti doit être réconciliable,
                        // pas seulement présent dans un journal applicatif.
                        await _auditService.LogAsync(
                            AuditActions.Stock.DeductionShortfall,
                            "Invoice",
                            notification.InvoiceId,
                            newValues: new
                            {
                                notification.InvoiceNumber,
                                line.ProductId,
                                Requested = line.Quantity,
                                Deducted = availableToDeduct,
                                Shortfall = shortfall,
                                WarehouseId = targetWarehouse.Id
                            },
                            cancellationToken: cancellationToken);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Stock deduction failed for product {ProductId}, invoice {InvoiceNumber}: {Error}. Current stock: {CurrentStock}, Requested: {Requested}",
                            line.ProductId, notification.InvoiceNumber, exitResult.Error.Description, stockItem.QuantityOnHand, line.Quantity);
                    }
                }
                else
                {
                    _logger.LogWarning(
                        "Stock deduction failed for product {ProductId}, invoice {InvoiceNumber}: {Error}. " +
                        "Current stock: {CurrentStock}, Requested: {Requested}",
                        line.ProductId,
                        notification.InvoiceNumber,
                        exitResult.Error.Description,
                        stockItem.QuantityOnHand,
                        line.Quantity);
                }
                continue;
            }

            await _stockItemRepository.UpdateAsync(stockItem, cancellationToken);

            _logger.LogInformation(
                "Stock deducted for product {ProductId}: {Quantity} units. New balance: {NewBalance}",
                line.ProductId, line.Quantity, stockItem.QuantityOnHand);
        }

        _logger.LogInformation("Completed stock deduction processing for invoice {InvoiceNumber}", 
            notification.InvoiceNumber);
    }
}
