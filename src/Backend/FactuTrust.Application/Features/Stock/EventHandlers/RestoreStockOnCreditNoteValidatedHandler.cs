using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Application.Features.Stock.EventHandlers;

/// <summary>
/// Handles InvoiceValidatedEvent for credit notes (AVO) only — restores stock for each line
/// (the dual of <see cref="DeductStockOnInvoiceValidatedHandler"/> which short-circuits on AVO).
/// Uses the line quantity as a positive entry; sign is carried by the invoice header,
/// not the line, so a 5-unit AVO line restores 5 units to stock.
/// </summary>
public sealed class RestoreStockOnCreditNoteValidatedHandler : INotificationHandler<InvoiceValidatedEvent>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IStockItemRepository _stockItemRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IProductRepository _productRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly ILogger<RestoreStockOnCreditNoteValidatedHandler> _logger;

    public RestoreStockOnCreditNoteValidatedHandler(
        IInvoiceRepository invoiceRepository,
        IStockItemRepository stockItemRepository,
        IWarehouseRepository warehouseRepository,
        IProductRepository productRepository,
        IStockMovementRepository stockMovementRepository,
        ILogger<RestoreStockOnCreditNoteValidatedHandler> logger)
    {
        _invoiceRepository = invoiceRepository;
        _stockItemRepository = stockItemRepository;
        _warehouseRepository = warehouseRepository;
        _productRepository = productRepository;
        _stockMovementRepository = stockMovementRepository;
        _logger = logger;
    }

    public async Task Handle(InvoiceValidatedEvent notification, CancellationToken cancellationToken)
    {
        if (!notification.IsCreditNote)
            return;

        _logger.LogInformation("Processing stock restoration for credit note {InvoiceId} ({InvoiceNumber})",
            notification.InvoiceId, notification.InvoiceNumber);

        var reference = $"Avoir {notification.InvoiceNumber}";

        var existingMovements = await _stockMovementRepository.GetByReferenceAsync(reference, cancellationToken);
        if (existingMovements.Count > 0)
        {
            _logger.LogInformation(
                "Stock restoration for credit note {InvoiceNumber} already processed ({Count} movements found) — skipping",
                notification.InvoiceNumber, existingMovements.Count);
            return;
        }

        var invoice = await _invoiceRepository.GetByIdWithLinesAsync(notification.InvoiceId, cancellationToken);
        if (invoice == null)
        {
            _logger.LogWarning("Credit note {InvoiceId} not found for stock restoration", notification.InvoiceId);
            return;
        }

        Warehouse? targetWarehouse = null;
        if (invoice.WarehouseId.HasValue)
            targetWarehouse = await _warehouseRepository.GetByIdAsync(invoice.WarehouseId.Value, cancellationToken);

        targetWarehouse ??= await _warehouseRepository.GetDefaultAsync(cancellationToken);

        if (targetWarehouse == null)
        {
            _logger.LogWarning("No warehouse configured - skipping stock restoration for credit note {InvoiceId}",
                notification.InvoiceId);
            return;
        }

        foreach (var line in invoice.Lines)
        {
            var product = await _productRepository.GetByIdAsync(line.ProductId, cancellationToken);
            if (product == null || !product.IsStockManaged)
            {
                _logger.LogDebug("Skipping stock restoration for product {ProductId} (not stock-managed)",
                    line.ProductId);
                continue;
            }

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

            // Use the existing average cost so CMUP is preserved (an avoir does not change valuation).
            var unitCost = stockItem.AverageCost;

            var entryResult = stockItem.RecordEntry(
                line.Quantity,
                unitCost,
                MovementReason.CustomerReturn,
                reference,
                "Réintégration suite à avoir client");

            if (entryResult.IsFailure)
            {
                _logger.LogWarning(
                    "Stock restoration failed for product {ProductId}, credit note {InvoiceNumber}: {Error}. Quantity: {Quantity}",
                    line.ProductId,
                    notification.InvoiceNumber,
                    entryResult.Error.Description,
                    line.Quantity);
                continue;
            }

            await _stockItemRepository.UpdateAsync(stockItem, cancellationToken);

            _logger.LogInformation(
                "Stock restored for product {ProductId}: +{Quantity} units. New balance: {NewBalance}",
                line.ProductId, line.Quantity, stockItem.QuantityOnHand);
        }

        _logger.LogInformation("Completed stock restoration processing for credit note {InvoiceNumber}",
            notification.InvoiceNumber);
    }
}
