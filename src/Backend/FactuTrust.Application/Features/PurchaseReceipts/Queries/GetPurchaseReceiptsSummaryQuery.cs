using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.PurchaseReceipts.Queries;

/// <summary>
/// Query to get aggregated totals for the purchase receipt list over the ENTIRE filtered set.
/// </summary>
public sealed record GetPurchaseReceiptsSummaryQuery(
    string? Search = null,
    PurchaseReceiptStatus? Status = null,
    Guid? SupplierId = null,
    Guid? PurchaseOrderId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null) : IRequest<PurchaseReceiptListSummaryDto>;

/// <summary>
/// Handler for GetPurchaseReceiptsSummaryQuery.
/// </summary>
public sealed class GetPurchaseReceiptsSummaryQueryHandler
    : IRequestHandler<GetPurchaseReceiptsSummaryQuery, PurchaseReceiptListSummaryDto>
{
    private readonly IPurchaseReceiptRepository _purchaseReceiptRepository;

    public GetPurchaseReceiptsSummaryQueryHandler(IPurchaseReceiptRepository purchaseReceiptRepository)
    {
        _purchaseReceiptRepository = purchaseReceiptRepository;
    }

    public Task<PurchaseReceiptListSummaryDto> Handle(
        GetPurchaseReceiptsSummaryQuery request,
        CancellationToken cancellationToken)
        => _purchaseReceiptRepository.GetSummaryAsync(
            request.Search,
            request.Status,
            request.SupplierId,
            request.PurchaseOrderId,
            request.FromDate,
            request.ToDate,
            cancellationToken);
}
