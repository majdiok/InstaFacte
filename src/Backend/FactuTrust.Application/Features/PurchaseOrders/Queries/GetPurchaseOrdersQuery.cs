using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.PurchaseOrders.Queries;

/// <summary>
/// Query to get paginated purchase orders with optional search and filters.
/// </summary>
public sealed record GetPurchaseOrdersQuery(
    string? Search = null,
    PurchaseOrderStatus? Status = null,
    Guid? SupplierId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<PurchaseOrderListDto>>;

/// <summary>
/// Handler for GetPurchaseOrdersQuery.
/// </summary>
public sealed class GetPurchaseOrdersQueryHandler : IRequestHandler<GetPurchaseOrdersQuery, PagedResult<PurchaseOrderListDto>>
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;

    public GetPurchaseOrdersQueryHandler(IPurchaseOrderRepository purchaseOrderRepository)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
    }

    public async Task<PagedResult<PurchaseOrderListDto>> Handle(GetPurchaseOrdersQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _purchaseOrderRepository.SearchAsync(
            request.Search,
            request.Status,
            request.SupplierId,
            request.FromDate,
            request.ToDate,
            request.Page,
            request.PageSize,
            cancellationToken);

        var dtos = items.Select(po => new PurchaseOrderListDto
        {
            Id = po.Id,
            Number = po.Number.Value,
            SupplierName = po.Supplier?.Name ?? "—",
            SupplierId = po.SupplierId,
            OrderDate = po.OrderDate,
            ExpectedDeliveryDate = po.ExpectedDeliveryDate,
            Reference = po.Reference,
            Status = po.Status,
            StatusDisplay = po.Status.ToDisplayString(),
            StatusCss = po.Status.ToCssClass(),
            TotalHT = po.SubTotal.Amount,
            TotalTTC = po.TotalAmount.Amount,
            LineCount = po.Lines.Count,
            WarehouseId = po.WarehouseId,
            WarehouseName = po.Warehouse?.Name
        }).ToList();

        return PagedResult<PurchaseOrderListDto>.Create(dtos, request.Page, request.PageSize, totalCount);
    }
}
