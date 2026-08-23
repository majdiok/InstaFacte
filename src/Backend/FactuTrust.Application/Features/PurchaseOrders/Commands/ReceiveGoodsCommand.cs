using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;

namespace FactuTrust.Application.Features.PurchaseOrders.Commands;

/// <summary>
/// Command to receive goods for a purchase order.
/// Updates received quantities on lines, transitions status, and creates stock entries.
/// </summary>
public sealed record ReceiveGoodsCommand(Guid PurchaseOrderId, ReceiveGoodsDto Dto) : IRequest<Result>;

/// <summary>
/// Handler for ReceiveGoodsCommand. Delegates stock updates to <see cref="IPurchaseGoodsReceptionService"/>.
/// </summary>
public sealed class ReceiveGoodsCommandHandler : IRequestHandler<ReceiveGoodsCommand, Result>
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IPurchaseGoodsReceptionService _receptionService;
    private readonly IAuditService _auditService;

    public ReceiveGoodsCommandHandler(
        IPurchaseOrderRepository purchaseOrderRepository,
        IWarehouseRepository warehouseRepository,
        IPurchaseGoodsReceptionService receptionService,
        IAuditService auditService)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
        _warehouseRepository = warehouseRepository;
        _receptionService = receptionService;
        _auditService = auditService;
    }

    public async Task<Result> Handle(ReceiveGoodsCommand request, CancellationToken cancellationToken)
    {
        var po = await _purchaseOrderRepository.GetByIdWithLinesAsync(request.PurchaseOrderId, cancellationToken);
        if (po is null)
            return Result.Failure(Error.NotFound("PurchaseOrder", request.PurchaseOrderId));

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

        var stockLines = new List<PurchaseReceptionStockLine>();
        foreach (var (lineId, receivedQty) in receptions)
        {
            var line = po.Lines.FirstOrDefault(l => l.Id == lineId);
            if (line is null)
                return Result.Failure(Error.Validation("Lines", "Ligne de commande introuvable."));

            var lineAllocations = request.Dto.Lines
                .FirstOrDefault(l => l.LineId == lineId)?.Allocations;

            stockLines.Add(new PurchaseReceptionStockLine(
                line.ProductId,
                receivedQty,
                line.UnitPrice.Amount,
                line.UnitPrice.Currency,
                line.Id,
                lineAllocations));
        }

        // Legacy stock reference kept as "BC {number}" for compatibility with existing movements.
        var stockResult = await _receptionService.ApplyStockEntriesAsync(
            targetWarehouse,
            stockLines,
            stockReference: $"BC {po.Number.Value}",
            notes: $"Réception fournisseur - {po.Supplier?.Name}",
            cancellationToken);

        if (stockResult.IsFailure)
            return stockResult;

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
