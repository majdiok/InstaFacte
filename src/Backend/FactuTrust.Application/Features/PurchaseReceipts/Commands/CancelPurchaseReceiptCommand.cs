using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;

namespace FactuTrust.Application.Features.PurchaseReceipts.Commands;

/// <summary>
/// Command to cancel a purchase receipt.
/// If the receipt was validated, reverses PO quantities and stock entries first.
/// </summary>
public sealed record CancelPurchaseReceiptCommand(Guid Id, string Reason) : IRequest<Result>;

/// <summary>
/// Handler for CancelPurchaseReceiptCommand.
/// </summary>
public sealed class CancelPurchaseReceiptCommandHandler : IRequestHandler<CancelPurchaseReceiptCommand, Result>
{
    private readonly IPurchaseReceiptRepository _purchaseReceiptRepository;
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IPurchaseGoodsReceptionService _receptionService;
    private readonly IAuditService _auditService;

    public CancelPurchaseReceiptCommandHandler(
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

    public async Task<Result> Handle(CancelPurchaseReceiptCommand request, CancellationToken cancellationToken)
    {
        var receipt = await _purchaseReceiptRepository.GetByIdWithLinesAsync(request.Id, cancellationToken);
        if (receipt is null)
            return Result.Failure(Error.NotFound("PurchaseReceipt", request.Id));

        var wasValidated = receipt.Status == PurchaseReceiptStatus.Validated;

        if (wasValidated)
        {
            var receivableLines = receipt.Lines.Where(l => l.ReceivedQuantity > 0).ToList();

            if (receipt.PurchaseOrderId is { } purchaseOrderId)
            {
                var po = await _purchaseOrderRepository.GetByIdWithLinesAsync(purchaseOrderId, cancellationToken);
                if (po is null)
                    return Result.Failure(Error.NotFound("PurchaseOrder", purchaseOrderId));

                var reversals = receivableLines
                    .Where(l => l.PurchaseOrderLineId.HasValue)
                    .Select(l => (l.PurchaseOrderLineId!.Value, l.ReceivedQuantity))
                    .ToList();

                if (reversals.Count > 0)
                {
                    var reversePoResult = po.ReverseGoodsReception(reversals);
                    if (reversePoResult.IsFailure)
                        return reversePoResult;

                    await _purchaseOrderRepository.UpdateAsync(po, cancellationToken);
                }
            }

            var warehouse = await _warehouseRepository.GetByIdAsync(receipt.WarehouseId, cancellationToken);
            if (warehouse is null)
                return Result.Failure(Error.NotFound("Warehouse", receipt.WarehouseId));

            var stockLines = receivableLines
                .Select(l => new PurchaseReceptionStockLine(
                    l.ProductId,
                    l.ReceivedQuantity,
                    l.UnitPrice.Amount,
                    l.UnitPrice.Currency))
                .ToList();

            var reverseStockResult = await _receptionService.ReverseStockEntriesAsync(
                warehouse,
                stockLines,
                stockReference: $"BR {receipt.Number.Value}",
                notes: $"Annulation réception - {receipt.Number.Value}",
                cancellationToken);

            if (reverseStockResult.IsFailure)
                return reverseStockResult;
        }

        var cancelResult = receipt.Cancel(request.Reason);
        if (cancelResult.IsFailure)
            return cancelResult;

        await _purchaseReceiptRepository.UpdateAsync(receipt, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.PurchaseReceipt.Cancelled,
            "PurchaseReceipt",
            receipt.Id,
            newValues: new
            {
                Number = receipt.Number.Value,
                Reason = request.Reason,
                WasValidated = wasValidated
            },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
