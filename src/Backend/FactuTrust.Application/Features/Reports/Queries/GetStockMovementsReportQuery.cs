using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using MediatR;

namespace FactuTrust.Application.Features.Reports.Queries;

/// <summary>
/// Query to get paginated stock movements report (mouvement détaillé de stock).
/// </summary>
public sealed record GetStockMovementsReportQuery(
    Guid? WarehouseId,
    DateTime? FromDate,
    DateTime? ToDate,
    int Page = 1,
    int PageSize = 20) : IRequest<(IReadOnlyList<StockMovementReportRowDto> Items, int TotalCount)>;

/// <summary>
/// Handler for GetStockMovementsReportQuery.
/// </summary>
public sealed class GetStockMovementsReportQueryHandler
    : IRequestHandler<GetStockMovementsReportQuery, (IReadOnlyList<StockMovementReportRowDto> Items, int TotalCount)>
{
    private readonly IStockMovementRepository _movementRepository;

    public GetStockMovementsReportQueryHandler(IStockMovementRepository movementRepository)
    {
        _movementRepository = movementRepository;
    }

    public async Task<(IReadOnlyList<StockMovementReportRowDto> Items, int TotalCount)> Handle(
        GetStockMovementsReportQuery request,
        CancellationToken cancellationToken)
    {
        return await _movementRepository.GetReportPageAsync(
            request.WarehouseId,
            request.FromDate,
            request.ToDate,
            request.Page,
            request.PageSize,
            cancellationToken);
    }
}
