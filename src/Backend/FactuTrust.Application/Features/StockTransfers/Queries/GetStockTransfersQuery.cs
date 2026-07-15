using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.StockTransfers.Queries;

public sealed record GetStockTransfersQuery(
    StockTransferStatus? Status = null,
    Guid? WarehouseId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null) : IRequest<Result<IReadOnlyList<StockTransferListDto>>>;

public sealed class GetStockTransfersQueryHandler
    : IRequestHandler<GetStockTransfersQuery, Result<IReadOnlyList<StockTransferListDto>>>
{
    private readonly IStockTransferRepository _repository;

    public GetStockTransfersQueryHandler(IStockTransferRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<StockTransferListDto>>> Handle(
        GetStockTransfersQuery request, CancellationToken cancellationToken)
    {
        var transfers = await _repository.GetFilteredAsync(
            request.Status, request.WarehouseId, request.FromDate, request.ToDate, cancellationToken);

        var dtos = transfers.Select(t => new StockTransferListDto
        {
            Id = t.Id,
            Number = t.Number.Value,
            TransferDate = t.TransferDate,
            Status = t.Status,
            StatusDisplay = t.Status.ToDisplayString(),
            StatusCss = t.Status.ToCssClass(),
            Reference = t.Reference,
            SourceWarehouseId = t.SourceWarehouseId,
            SourceWarehouseName = t.SourceWarehouse?.Name ?? "",
            DestinationWarehouseId = t.DestinationWarehouseId,
            DestinationWarehouseName = t.DestinationWarehouse?.Name ?? "",
            LineCount = t.Lines.Count,
            TotalRequestedQuantity = t.TotalRequestedQuantity,
            TotalTransferredQuantity = t.TotalTransferredQuantity,
            CreatedAt = t.CreatedAt
        }).ToList();

        return Result.Success<IReadOnlyList<StockTransferListDto>>(dtos);
    }
}
