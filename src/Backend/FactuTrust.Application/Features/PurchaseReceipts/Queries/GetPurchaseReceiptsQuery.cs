using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.PurchaseReceipts.Queries;

/// <summary>
/// Query to get paginated purchase receipts with optional search and filters.
/// </summary>
public sealed record GetPurchaseReceiptsQuery(
    string? Search = null,
    PurchaseReceiptStatus? Status = null,
    Guid? SupplierId = null,
    Guid? PurchaseOrderId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<PurchaseReceiptListDto>>;

/// <summary>
/// Handler for GetPurchaseReceiptsQuery.
/// </summary>
public sealed class GetPurchaseReceiptsQueryHandler
    : IRequestHandler<GetPurchaseReceiptsQuery, PagedResult<PurchaseReceiptListDto>>
{
    private readonly IPurchaseReceiptRepository _purchaseReceiptRepository;

    public GetPurchaseReceiptsQueryHandler(IPurchaseReceiptRepository purchaseReceiptRepository)
    {
        _purchaseReceiptRepository = purchaseReceiptRepository;
    }

    public async Task<PagedResult<PurchaseReceiptListDto>> Handle(
        GetPurchaseReceiptsQuery request,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _purchaseReceiptRepository.SearchAsync(
            request.Search,
            request.Status,
            request.SupplierId,
            request.PurchaseOrderId,
            request.FromDate,
            request.ToDate,
            request.Page,
            request.PageSize,
            cancellationToken);

        var dtos = items.Select(r => new PurchaseReceiptListDto
        {
            Id = r.Id,
            Number = r.Number.Value,
            SupplierName = r.Supplier?.Name ?? "—",
            SupplierId = r.SupplierId,
            ReceiptDate = r.ReceiptDate,
            SupplierReference = r.SupplierReference,
            Status = r.Status,
            StatusDisplay = r.Status.ToDisplayString(),
            StatusCss = r.Status.ToCssClass(),
            TotalHT = r.SubTotal.Amount,
            TotalTTC = r.TotalAmount.Amount,
            LineCount = r.Lines.Count,
            WarehouseId = r.WarehouseId,
            WarehouseName = r.Warehouse?.Name,
            PurchaseOrderId = r.PurchaseOrderId,
            PurchaseOrderNumber = r.PurchaseOrder?.Number.Value,
            IsPartialRelativeToOrdered = r.IsPartialRelativeToOrdered
        }).ToList();

        return PagedResult<PurchaseReceiptListDto>.Create(dtos, request.Page, request.PageSize, totalCount);
    }
}
