using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using MediatR;

namespace FactuTrust.Application.Features.Stock.Queries;

/// <summary>
/// Query to get all warehouses.
/// </summary>
public sealed record GetWarehousesQuery(bool ActiveOnly = true) : IRequest<IReadOnlyList<WarehouseDto>>;

/// <summary>
/// DTO for warehouse.
/// </summary>
public sealed record WarehouseDto(
    Guid Id,
    string Code,
    string Name,
    string? Address,
    bool IsDefault,
    bool IsActive);

/// <summary>
/// Handler for GetWarehousesQuery.
/// </summary>
public sealed class GetWarehousesQueryHandler : IRequestHandler<GetWarehousesQuery, IReadOnlyList<WarehouseDto>>
{
    private readonly IWarehouseRepository _warehouseRepository;

    public GetWarehousesQueryHandler(IWarehouseRepository warehouseRepository)
    {
        _warehouseRepository = warehouseRepository;
    }

    public async Task<IReadOnlyList<WarehouseDto>> Handle(GetWarehousesQuery request, CancellationToken cancellationToken)
    {
        var warehouses = request.ActiveOnly
            ? await _warehouseRepository.GetActiveWarehousesAsync(cancellationToken)
            : await _warehouseRepository.GetAllAsync(cancellationToken);

        return warehouses.Select(w => new WarehouseDto(
            w.Id,
            w.Code,
            w.Name,
            w.Address,
            w.IsDefault,
            w.IsActive)).ToList();
    }
}
