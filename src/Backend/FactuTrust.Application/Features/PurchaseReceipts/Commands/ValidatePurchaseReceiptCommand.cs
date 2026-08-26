using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
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
/// Stock, PO imputation and receipt status are committed in one tenant transaction.
/// </summary>
public sealed class ValidatePurchaseReceiptCommandHandler : IRequestHandler<ValidatePurchaseReceiptCommand, Result>
{
    private static readonly PurchaseReceiptStatus[] AppliedReceiptStatuses =
    {
        PurchaseReceiptStatus.Validated,
        PurchaseReceiptStatus.PartiallyInvoiced,
        PurchaseReceiptStatus.Invoiced
    };

    private readonly IPurchaseReceiptRepository _purchaseReceiptRepository;
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IPurchaseGoodsReceptionService _receptionService;
    private readonly ITenantUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;

    public ValidatePurchaseReceiptCommandHandler(
        IPurchaseReceiptRepository purchaseReceiptRepository,
        IPurchaseOrderRepository purchaseOrderRepository,
        IWarehouseRepository warehouseRepository,
        IPurchaseGoodsReceptionService receptionService,
        ITenantUnitOfWork unitOfWork,
        IAuditService auditService)
    {
        _purchaseReceiptRepository = purchaseReceiptRepository;
        _purchaseOrderRepository = purchaseOrderRepository;
        _warehouseRepository = warehouseRepository;
        _receptionService = receptionService;
        _unitOfWork = unitOfWork;
        _auditService = auditService;
    }

    public async Task<Result> Handle(ValidatePurchaseReceiptCommand request, CancellationToken cancellationToken)
    {
        var receivableLineCount = 0;
        string? receiptNumber = null;

        var result = await _unitOfWork.ExecuteAsync(async ct =>
        {
            var receipt = await _purchaseReceiptRepository.GetByIdWithLinesAsync(request.Id, ct);
            if (receipt is null)
                return Result.Failure(Error.NotFound("PurchaseReceipt", request.Id));

            var validateResult = receipt.MarkValidated();
            if (validateResult.IsFailure)
                return validateResult;

            var receivableLines = receipt.Lines.Where(l => l.ReceivedQuantity > 0).ToList();
            receivableLineCount = receivableLines.Count;
            receiptNumber = receipt.Number.Value;

            var warehouse = await _warehouseRepository.GetByIdAsync(receipt.WarehouseId, ct);
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
                ct);

            if (stockResult.IsFailure)
                return stockResult;

            if (receipt.PurchaseOrderId is { } purchaseOrderId)
            {
                var po = await _purchaseOrderRepository.GetByIdWithLinesAsync(purchaseOrderId, ct);
                if (po is null)
                    return Result.Failure(Error.NotFound("PurchaseOrder", purchaseOrderId));

                var poReceptions = await BuildIdempotentPoReceptionsAsync(
                    receipt.Id, purchaseOrderId, receivableLines, po, ct);

                if (poReceptions.IsFailure)
                    return poReceptions;

                if (poReceptions.Value.Count > 0)
                {
                    var poResult = po.ReceiveGoods(poReceptions.Value);
                    if (poResult.IsFailure)
                        return poResult;

                    await _purchaseOrderRepository.UpdateAsync(po, ct);
                }
            }

            await _purchaseReceiptRepository.UpdateAsync(receipt, ct);
            return Result.Success();
        }, cancellationToken);

        if (result.IsFailure)
            return result;

        try
        {
            await _auditService.LogAsync(
                AuditActions.PurchaseReceipt.Validated,
                "PurchaseReceipt",
                request.Id,
                newValues: new
                {
                    Number = receiptNumber,
                    Status = PurchaseReceiptStatus.Validated.ToString(),
                    ReceivedLines = receivableLineCount
                },
                cancellationToken: cancellationToken);
        }
        catch
        {
            // Audit is best-effort and lives outside the tenant transaction.
        }

        return Result.Success();
    }

    /// <summary>
    /// Aggregates this receipt's quantities per PO line and subtracts unexplained received
    /// qty already on the PO (e.g. a previous validate that saved the PO then failed).
    /// </summary>
    private async Task<Result<List<(Guid LineId, decimal ReceivedQuantity)>>> BuildIdempotentPoReceptionsAsync(
        Guid currentReceiptId,
        Guid purchaseOrderId,
        IReadOnlyList<PurchaseReceiptLine> receivableLines,
        PurchaseOrder po,
        CancellationToken cancellationToken)
    {
        var thisByPoLine = receivableLines
            .Where(l => l.PurchaseOrderLineId.HasValue)
            .GroupBy(l => l.PurchaseOrderLineId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.ReceivedQuantity));

        if (thisByPoLine.Count == 0)
            return Result.Success(new List<(Guid LineId, decimal ReceivedQuantity)>());

        var siblings = await _purchaseReceiptRepository.GetByPurchaseOrderIdAsync(purchaseOrderId, cancellationToken);
        var appliedByOthers = new Dictionary<Guid, decimal>();
        foreach (var other in siblings)
        {
            if (other.Id == currentReceiptId)
                continue;
            if (!AppliedReceiptStatuses.Contains(other.Status))
                continue;

            foreach (var line in other.Lines)
            {
                if (line.PurchaseOrderLineId is not { } poLineId)
                    continue;
                appliedByOthers[poLineId] = appliedByOthers.GetValueOrDefault(poLineId) + line.ReceivedQuantity;
            }
        }

        var poReceptions = new List<(Guid LineId, decimal ReceivedQuantity)>();
        foreach (var (poLineId, thisQty) in thisByPoLine)
        {
            var poLine = po.Lines.FirstOrDefault(l => l.Id == poLineId);
            if (poLine is null)
                return Result.Failure<List<(Guid LineId, decimal ReceivedQuantity)>>(
                    Error.NotFound("PurchaseOrderLine", poLineId));

            var alreadyAppliedByOthers = appliedByOthers.GetValueOrDefault(poLineId);
            var unexplained = Math.Max(0m, poLine.ReceivedQuantity - alreadyAppliedByOthers);
            var stillNeeded = Math.Max(0m, thisQty - unexplained);
            if (stillNeeded > 0)
                poReceptions.Add((poLineId, stillNeeded));
        }

        return Result.Success(poReceptions);
    }
}
