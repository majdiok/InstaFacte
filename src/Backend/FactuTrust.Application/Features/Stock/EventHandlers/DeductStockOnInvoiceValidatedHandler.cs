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
    private readonly IStockMutationService _mutation;
    private readonly ITrackedDocumentStockService _trackedStock;
    private readonly ILogger<DeductStockOnInvoiceValidatedHandler> _logger;

    public DeductStockOnInvoiceValidatedHandler(
        IInvoiceRepository invoiceRepository,
        IStockItemRepository stockItemRepository,
        IWarehouseRepository warehouseRepository,
        IProductRepository productRepository,
        IStockMovementRepository stockMovementRepository,
        IAuditService auditService,
        IStockMutationService mutation,
        ITrackedDocumentStockService trackedStock,
        ILogger<DeductStockOnInvoiceValidatedHandler> logger)
    {
        _invoiceRepository = invoiceRepository;
        _stockItemRepository = stockItemRepository;
        _warehouseRepository = warehouseRepository;
        _productRepository = productRepository;
        _stockMovementRepository = stockMovementRepository;
        _auditService = auditService;
        _mutation = mutation;
        _trackedStock = trackedStock;
        _logger = logger;
    }

    public async Task Handle(InvoiceValidatedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.IsCreditNote)
            return;

        _logger.LogInformation("Processing stock deduction for invoice {InvoiceId} ({InvoiceNumber})",
            notification.InvoiceId, notification.InvoiceNumber);

        var reference = $"Facture {notification.InvoiceNumber}";

        var existingMovements = await _stockMovementRepository.GetByReferenceAsync(reference, cancellationToken);
        if (existingMovements.Count > 0)
        {
            _logger.LogInformation(
                "Stock deduction for invoice {InvoiceNumber} already processed ({Count} movements found) — skipping",
                notification.InvoiceNumber, existingMovements.Count);
            return;
        }

        var invoice = await _invoiceRepository.GetByIdWithLinesAsync(notification.InvoiceId, cancellationToken);
        if (invoice == null)
        {
            _logger.LogWarning("Invoice {InvoiceId} not found for stock deduction", notification.InvoiceId);
            return;
        }

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

        if (invoice.SourceDeliveryNoteId.HasValue)
        {
            _logger.LogInformation(
                "Invoice {InvoiceNumber} was generated from delivery note {DeliveryNoteId} — skipping stock deduction (already done at delivery)",
                notification.InvoiceNumber, invoice.SourceDeliveryNoteId.Value);
            return;
        }

        foreach (var line in invoice.Lines)
        {
            if (!line.ProductId.HasValue)
            {
                _logger.LogDebug("Skipping stock deduction for custom line {LineNumber} (no product reference)",
                    line.LineNumber);
                continue;
            }

            var product = await _productRepository.GetByIdAsync(line.ProductId.Value, cancellationToken);
            if (product == null || !product.IsStockManaged)
            {
                _logger.LogDebug("Skipping stock deduction for product {ProductId} (not stock-managed)",
                    line.ProductId);
                continue;
            }

            if (_trackedStock.IsLiveTracked(product))
            {
                _logger.LogInformation(
                    "Skipping event-handler deduction for tracked product {ProductId} on invoice {InvoiceNumber} (applied in ValidateInvoice)",
                    line.ProductId, notification.InvoiceNumber);
                continue;
            }

            var exitResult = await _mutation.ApplyAsync(new StockMutationRequest
            {
                ProductId = line.ProductId.Value,
                WarehouseId = targetWarehouse.Id,
                Kind = StockMutationKind.Exit,
                Quantity = line.Quantity,
                Reason = MovementReason.Sale,
                Reference = reference,
                Notes = "Vente - Ligne de facture"
            }, cancellationToken);

            if (exitResult.IsFailure)
            {
                var isInsufficientStock = exitResult.Error.Code == "Validation.Quantity"
                    && exitResult.Error.Description.Contains("Stock insuffisant", StringComparison.OrdinalIgnoreCase);
                var stockItem = await _stockItemRepository.GetByProductAndWarehouseAsync(
                    line.ProductId.Value, targetWarehouse.Id, cancellationToken);
                var availableToDeduct = stockItem?.QuantityAvailable ?? 0;

                if (isInsufficientStock && availableToDeduct > 0)
                {
                    var shortfall = line.Quantity - availableToDeduct;

                    var partialExitResult = await _mutation.ApplyAsync(new StockMutationRequest
                    {
                        ProductId = line.ProductId.Value,
                        WarehouseId = targetWarehouse.Id,
                        Kind = StockMutationKind.Exit,
                        Quantity = availableToDeduct,
                        Reason = MovementReason.Sale,
                        Reference = reference,
                        Notes = "Vente - Ligne de facture (déduction limitée au stock disponible)",
                        ShortfallQuantity = shortfall
                    }, cancellationToken);

                    if (partialExitResult.IsSuccess)
                    {
                        _logger.LogInformation(
                            "Partial stock deduction for product {ProductId}, invoice {InvoiceNumber}: requested {Requested}, deducted {Deducted}. New balance: {NewBalance}",
                            line.ProductId, notification.InvoiceNumber, line.Quantity, availableToDeduct, partialExitResult.Value.QuantityOnHand);

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
                            line.ProductId, notification.InvoiceNumber, exitResult.Error.Description, stockItem?.QuantityOnHand ?? 0, line.Quantity);
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
                        stockItem?.QuantityOnHand ?? 0,
                        line.Quantity);
                }
                continue;
            }

            _logger.LogInformation(
                "Stock deducted for product {ProductId}: {Quantity} units. New balance: {NewBalance}",
                line.ProductId, line.Quantity, exitResult.Value.QuantityOnHand);
        }

        _logger.LogInformation("Completed stock deduction processing for invoice {InvoiceNumber}",
            notification.InvoiceNumber);
    }
}
