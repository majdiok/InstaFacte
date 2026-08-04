using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.PurchaseReceipts.Queries;

/// <summary>
/// Query to prefill a purchase receipt from a purchase order (supplier, warehouse, pending lines).
/// </summary>
public sealed record GetPurchaseReceiptPrefillFromPOQuery(Guid PurchaseOrderId)
    : IRequest<Result<PurchaseReceiptPrefillDto>>;

/// <summary>
/// Handler for GetPurchaseReceiptPrefillFromPOQuery.
/// </summary>
public sealed class GetPurchaseReceiptPrefillFromPOQueryHandler
    : IRequestHandler<GetPurchaseReceiptPrefillFromPOQuery, Result<PurchaseReceiptPrefillDto>>
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;

    public GetPurchaseReceiptPrefillFromPOQueryHandler(IPurchaseOrderRepository purchaseOrderRepository)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
    }

    public async Task<Result<PurchaseReceiptPrefillDto>> Handle(
        GetPurchaseReceiptPrefillFromPOQuery request,
        CancellationToken cancellationToken)
    {
        var po = await _purchaseOrderRepository.GetByIdWithLinesAsync(request.PurchaseOrderId, cancellationToken);
        if (po is null)
            return Result.Failure<PurchaseReceiptPrefillDto>(Error.NotFound("PurchaseOrder", request.PurchaseOrderId));

        if (!po.Status.CanReceiveGoods())
            return Result.Failure<PurchaseReceiptPrefillDto>(Error.Validation("Status",
                "Cette commande ne peut pas servir de base à une réception dans son état actuel."));

        var pendingLines = po.Lines
            .Where(l => l.PendingQuantity > 0)
            .OrderBy(l => l.LineNumber)
            .Select(l => new PurchaseReceiptPrefillLineDto
            {
                PurchaseOrderLineId = l.Id,
                ProductId = l.ProductId,
                ProductCode = l.ProductCode,
                ProductName = l.ProductName,
                ProductDescription = l.ProductDescription,
                Unit = l.Unit,
                OrderedQuantity = l.Quantity,
                AlreadyReceivedQuantity = l.ReceivedQuantity,
                PendingQuantity = l.PendingQuantity,
                UnitPriceHT = l.UnitPrice.Amount
            })
            .ToList();

        if (pendingLines.Count == 0)
            return Result.Failure<PurchaseReceiptPrefillDto>(Error.Validation("Lines",
                "Toutes les lignes de cette commande sont déjà entièrement reçues."));

        var dto = new PurchaseReceiptPrefillDto
        {
            PurchaseOrderId = po.Id,
            PurchaseOrderNumber = po.Number.Value,
            SupplierId = po.SupplierId,
            SupplierName = po.Supplier?.Name ?? "—",
            WarehouseId = po.WarehouseId,
            WarehouseName = po.Warehouse?.Name,
            Lines = pendingLines
        };

        return Result.Success(dto);
    }
}
