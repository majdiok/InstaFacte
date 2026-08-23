using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;

namespace FactuTrust.Application.Features.PurchaseReceipts.Commands;

/// <summary>
/// Command to validate a draft purchase receipt.
/// Updates PO received quantities (when linked) and creates stock entries.
/// </summary>
public sealed record PurchaseReceiptLineAllocationsDto(
    Guid LineId,
    IReadOnlyList<StockAllocationInput> Allocations);

public sealed record ValidatePurchaseReceiptCommand(
    Guid Id,
    IReadOnlyList<PurchaseReceiptLineAllocationsDto>? LineAllocations = null) : IRequest<Result>;

/// <summary>
/// Handler for ValidatePurchaseReceiptCommand.
/// </summary>
public sealed class ValidatePurchaseReceiptCommandHandler : IRequestHandler<ValidatePurchaseReceiptCommand, Result>
{
    private readonly IPurchaseReceiptRepository _purchaseReceiptRepository;
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IPurchaseGoodsReceptionService _receptionService;
    private readonly IAuditService _auditService;

    public ValidatePurchaseReceiptCommandHandler(
        IPurchaseReceiptRepository purchaseReceiptRepository,
        IPurchaseOrderRepository purchaseOrderRepository,
        IWarehouseRepository warehouseRepository,
        IPurchaseGoodsReceptionService receptionService,
        IAuditService auditService)
    {
        _purchaseReceiptRepository = purchaseReceiptRepository;
        _purchaseOrderRepository = purchaseOrderRepository;
        _warehouseRepository = warehouseRepository;
        _receptionService = receptionService;
        _auditService = auditService;
    }

    public async Task<Result> Handle(ValidatePurchaseReceiptCommand request, CancellationToken cancellationToken)
    {
        var receipt = await _purchaseReceiptRepository.GetByIdWithLinesAsync(request.Id, cancellationToken);
        if (receipt is null)
            return Result.Failure(Error.NotFound("PurchaseReceipt", request.Id));

        var validateResult = receipt.MarkValidated();
        if (validateResult.IsFailure)
            return validateResult;

        var receivableLines = receipt.Lines.Where(l => l.ReceivedQuantity > 0).ToList();

        if (receipt.PurchaseOrderId is { } purchaseOrderId)
        {
            var po = await _purchaseOrderRepository.GetByIdWithLinesAsync(purchaseOrderId, cancellationToken);
            if (po is null)
                return Result.Failure(Error.NotFound("PurchaseOrder", purchaseOrderId));

            var poReceptions = new List<(Guid LineId, decimal ReceivedQuantity)>();
            foreach (var line in receivableLines)
            {
                if (line.PurchaseOrderLineId is not { } poLineId)
                    continue;

                poReceptions.Add((poLineId, line.ReceivedQuantity));
            }

            if (poReceptions.Count > 0)
            {
                var poResult = po.ReceiveGoods(poReceptions);
                if (poResult.IsFailure)
                    return poResult;

                await _purchaseOrderRepository.UpdateAsync(po, cancellationToken);
            }
        }

        var warehouse = await _warehouseRepository.GetByIdAsync(receipt.WarehouseId, cancellationToken);
        if (warehouse is null)
            return Result.Failure(Error.NotFound("Warehouse", receipt.WarehouseId));

        if (!warehouse.IsActive)
            return Result.Failure(Error.Validation("Warehouse",
                "L'entrepôt sélectionné n'est pas actif"));

        var allocationsByLine = (request.LineAllocations ?? Array.Empty<PurchaseReceiptLineAllocationsDto>())
            .ToDictionary(a => a.LineId, a => a.Allocations);

        var stockLines = receivableLines
            .Select(l => new PurchaseReceptionStockLine(
                l.ProductId,
                l.ReceivedQuantity,
                l.UnitPrice.Amount,
                l.UnitPrice.Currency,
                l.Id,
                allocationsByLine.GetValueOrDefault(l.Id)))
            .ToList();

        var stockResult = await _receptionService.ApplyStockEntriesAsync(
            warehouse,
            stockLines,
            stockReference: $"BR {receipt.Number.Value}",
            notes: $"Réception fournisseur - {receipt.Supplier?.Name}",
            cancellationToken);

        if (stockResult.IsFailure)
            return stockResult;

        await _purchaseReceiptRepository.UpdateAsync(receipt, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.PurchaseReceipt.Validated,
            "PurchaseReceipt",
            receipt.Id,
            newValues: new
            {
                Number = receipt.Number.Value,
                Status = receipt.Status.ToString(),
                ReceivedLines = receivableLines.Count
            },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
