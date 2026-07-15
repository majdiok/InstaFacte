using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Inventory.Queries;

/// <summary>
/// Query to get aggregated totals for the inventory list over the ENTIRE filtered set.
/// Mirrors <see cref="GetPhysicalInventoriesQuery"/> filters (without pagination) so the UI totals
/// zone reflects exactly the active filters, not just the current page.
/// </summary>
public sealed record GetInventoriesSummaryQuery(
    string? Search = null,
    InventoryStatus? Status = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    Guid? WarehouseId = null) : IRequest<InventoryListSummaryDto>;

/// <summary>
/// Handler for GetInventoriesSummaryQuery.
/// </summary>
public sealed class GetInventoriesSummaryQueryHandler
    : IRequestHandler<GetInventoriesSummaryQuery, InventoryListSummaryDto>
{
    private readonly IPhysicalInventoryRepository _repository;

    public GetInventoriesSummaryQueryHandler(IPhysicalInventoryRepository repository)
    {
        _repository = repository;
    }

    public Task<InventoryListSummaryDto> Handle(GetInventoriesSummaryQuery request, CancellationToken cancellationToken)
        => _repository.GetSummaryAsync(
            request.Search,
            request.Status,
            request.FromDate,
            request.ToDate,
            request.WarehouseId,
            cancellationToken);
}
