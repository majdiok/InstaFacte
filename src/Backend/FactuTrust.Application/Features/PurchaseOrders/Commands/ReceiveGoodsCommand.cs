using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;

namespace FactuTrust.Application.Features.PurchaseOrders.Commands;

/// <summary>
/// Command to receive goods for a purchase order.
/// Updates received quantities on lines, transitions status, and creates stock entries.
/// </summary>
public sealed record ReceiveGoodsCommand(Guid PurchaseOrderId, ReceiveGoodsDto Dto) : IRequest<Result>;

/// <summary>
/// Handler for ReceiveGoodsCommand.
/// </summary>
public sealed class ReceiveGoodsCommandHandler : IRequestHandler<ReceiveGoodsCommand, Result>
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IStockItemRepository _stockItemRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IAuditService _auditService;

    public ReceiveGoodsCommandHandler(
        IPurchaseOrderRepository purchaseOrderRepository,
        IStockItemRepository stockItemRepository,
        IWarehouseRepository warehouseRepository,
        IAuditService auditService)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
        _stockItemRepository = stockItemRepository;
        _warehouseRepository = warehouseRepository;
        _auditService = auditService;
    }

    public async Task<Result> Handle(ReceiveGoodsCommand request, CancellationToken cancellationToken)
    {
        var po = await _purchaseOrderRepository.GetByIdWithLinesAsync(request.PurchaseOrderId, cancellationToken);
        if (po is null)
            return Result.Failure(Error.NotFound("PurchaseOrder", request.PurchaseOrderId));

        // Build the reception dictionary (lineId -> quantity)
        var receptions = new Dictionary<Guid, decimal>();
        foreach (var lineDto in request.Dto.Lines)
        {
            if (lineDto.ReceivedQuantity <= 0) continue;
            receptions[lineDto.LineId] = lineDto.ReceivedQuantity;
        }

        if (receptions.Count == 0)
            return Result.Failure(Error.Validation("Lines", "Aucune quantité à réceptionner."));

        var tuples = receptions.Select(kvp => (kvp.Key, kvp.Value));
        var result = po.ReceiveGoods(tuples);
        if (result.IsFailure)
            return result;

        // Resolve target warehouse: explicit DTO (validated) > PO warehouse > default
        Warehouse? targetWarehouse = null;

        if (request.Dto.WarehouseId is { } dtoWarehouseId)
        {
            var explicitWarehouse = await _warehouseRepository.GetByIdAsync(dtoWarehouseId, cancellationToken);
            if (explicitWarehouse is null)
                return Result.Failure(Error.NotFound("Warehouse", dtoWarehouseId));
            if (!explicitWarehouse.IsActive)
                return Result.Failure(Error.Validation("WarehouseId",
                    "L'entrepôt sélectionné n'est pas actif"));
            targetWarehouse = explicitWarehouse;
        }
        else
        {
            if (po.WarehouseId is { } poWarehouseId)
            {
                var poWarehouse = await _warehouseRepository.GetByIdAsync(poWarehouseId, cancellationToken);
                if (poWarehouse is not null && poWarehouse.IsActive)
                    targetWarehouse = poWarehouse;
            }

            targetWarehouse ??= await _warehouseRepository.GetDefaultAsync(cancellationToken);
        }

        if (targetWarehouse is null)
            return Result.Failure(Error.Validation("Warehouse",
                "Impossible de déterminer l'entrepôt de réception. Sélectionnez un entrepôt à la réception ou configurez un entrepôt par défaut actif (Paramètres > Entrepôts)."));

        foreach (var (lineId, receivedQty) in receptions)
        {
            var line = po.Lines.FirstOrDefault(l => l.Id == lineId);
            if (line is null)
                return Result.Failure(Error.Validation("Lines", "Ligne de commande introuvable."));

            var stockItem = await _stockItemRepository.GetByProductAndWarehouseAsync(
                line.ProductId, targetWarehouse.Id, cancellationToken);

            if (stockItem is null)
            {
                var createResult = StockItem.Create(line.ProductId, targetWarehouse.Id);
                if (createResult.IsFailure)
                    return Result.Failure(createResult.Error);

                stockItem = createResult.Value;
                var entryResult = stockItem.RecordEntry(
                    receivedQty,
                    line.UnitPrice.Amount,
                    MovementReason.Purchase,
                    reference: $"BC {po.Number.Value}",
                    notes: $"Réception fournisseur - {po.Supplier?.Name}");

                if (entryResult.IsFailure)
                    return entryResult;

                await _stockItemRepository.AddAsync(stockItem, cancellationToken);
            }
            else
            {
                var entryResult = stockItem.RecordEntry(
                    receivedQty,
                    line.UnitPrice.Amount,
                    MovementReason.Purchase,
                    reference: $"BC {po.Number.Value}",
                    notes: $"Réception fournisseur - {po.Supplier?.Name}");

                if (entryResult.IsFailure)
                    return entryResult;

                await _stockItemRepository.UpdateAsync(stockItem, cancellationToken);
            }
        }

        await _purchaseOrderRepository.UpdateAsync(po, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.PurchaseOrder.GoodsReceived,
            "PurchaseOrder",
            po.Id,
            newValues: new
            {
                Number = po.Number.Value,
                Status = po.Status.ToString(),
                ReceivedLines = receptions.Count
            },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}

