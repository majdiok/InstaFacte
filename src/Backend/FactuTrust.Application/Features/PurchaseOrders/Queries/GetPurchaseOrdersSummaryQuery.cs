using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.PurchaseOrders.Queries;

/// <summary>
/// Query to get aggregated totals for the purchase order list over the ENTIRE filtered set.
/// Mirrors <see cref="GetPurchaseOrdersQuery"/> filters (without pagination) so the UI totals
/// zone reflects exactly the active filters, not just the current page.
/// </summary>
public sealed record GetPurchaseOrdersSummaryQuery(
    string? Search = null,
    PurchaseOrderStatus? Status = null,
    Guid? SupplierId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null) : IRequest<PurchaseOrderListSummaryDto>;

/// <summary>
/// Handler for GetPurchaseOrdersSummaryQuery.
/// </summary>
public sealed class GetPurchaseOrdersSummaryQueryHandler
    : IRequestHandler<GetPurchaseOrdersSummaryQuery, PurchaseOrderListSummaryDto>
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;

    public GetPurchaseOrdersSummaryQueryHandler(IPurchaseOrderRepository purchaseOrderRepository)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
    }

    public Task<PurchaseOrderListSummaryDto> Handle(GetPurchaseOrdersSummaryQuery request, CancellationToken cancellationToken)
        => _purchaseOrderRepository.GetSummaryAsync(
            request.Search,
            request.Status,
            request.SupplierId,
            request.FromDate,
            request.ToDate,
            cancellationToken);
}
