using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Inventory.Queries;

/// <summary>
/// Query to get paginated physical inventories with optional filters.
/// </summary>
public sealed record GetPhysicalInventoriesQuery(
    string? Search = null,
    InventoryStatus? Status = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    Guid? WarehouseId = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<PhysicalInventoryListDto>>;

public sealed class GetPhysicalInventoriesQueryHandler : IRequestHandler<GetPhysicalInventoriesQuery, PagedResult<PhysicalInventoryListDto>>
{
    private readonly IPhysicalInventoryRepository _repository;

    public GetPhysicalInventoriesQueryHandler(IPhysicalInventoryRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<PhysicalInventoryListDto>> Handle(GetPhysicalInventoriesQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _repository.SearchAsync(
            request.Search,
            request.Status,
            request.FromDate,
            request.ToDate,
            request.WarehouseId,
            request.Page,
            request.PageSize,
            cancellationToken);

        var dtos = items.Select(i =>
        {
            var progressPercent = i.TotalProducts > 0
                ? (int)Math.Round((decimal)i.CountedProducts / i.TotalProducts * 100, 0)
                : 0;

            return new PhysicalInventoryListDto(
                i.Id,
                i.Reference,
                i.StartedAt,
                i.CompletedAt,
                i.WarehouseId,
                i.Warehouse?.Name ?? "Entrepôt",
                i.Type,
                i.Type.ToDisplayString(),
                i.Status,
                i.Status.ToDisplayString(),
                i.TotalProducts,
                i.CountedProducts,
                progressPercent);
        }).ToList();

        return PagedResult<PhysicalInventoryListDto>.Create(dtos, request.Page, request.PageSize, totalCount);
    }
}
