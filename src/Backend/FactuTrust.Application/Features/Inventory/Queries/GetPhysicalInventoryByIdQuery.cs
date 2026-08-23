using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Inventory.Queries;

/// <summary>
/// Query to get a single physical inventory by id for consultation.
/// </summary>
public sealed record GetPhysicalInventoryByIdQuery(Guid Id) : IRequest<Result<PhysicalInventoryDetailDto>>;

public sealed class GetPhysicalInventoryByIdQueryHandler : IRequestHandler<GetPhysicalInventoryByIdQuery, Result<PhysicalInventoryDetailDto>>
{
    private readonly IPhysicalInventoryRepository _repository;

    public GetPhysicalInventoryByIdQueryHandler(IPhysicalInventoryRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<PhysicalInventoryDetailDto>> Handle(GetPhysicalInventoryByIdQuery request, CancellationToken cancellationToken)
    {
        var inventory = await _repository.GetByIdWithDetailsAsync(request.Id, cancellationToken);
        if (inventory == null)
            return Result.Failure<PhysicalInventoryDetailDto>(Error.NotFound("Inventaire", request.Id));

        var progressPercent = inventory.TotalProducts > 0
            ? (int)Math.Round((decimal)inventory.CountedProducts / inventory.TotalProducts * 100, 0)
            : 0;

        var lines = inventory.CountLines.Select(l => new PhysicalInventoryDetailLineDto(
            l.ProductId,
            l.ProductCode,
            l.ProductName,
            l.TheoreticalQuantity,
            l.CountedQuantity,
            l.Difference,
            l.IsCounted,
            l.ProductLotId,
            l.LotNumber)).ToList();

        var dto = new PhysicalInventoryDetailDto(
            inventory.Id,
            inventory.Reference,
            inventory.StartedAt,
            inventory.CompletedAt,
            inventory.WarehouseId,
            inventory.Warehouse?.Name ?? "Entrepôt",
            inventory.Type,
            inventory.Type.ToDisplayString(),
            inventory.Status,
            inventory.Status.ToDisplayString(),
            inventory.Notes,
            inventory.TotalProducts,
            inventory.CountedProducts,
            lines);

        return Result.Success(dto);
    }
}
