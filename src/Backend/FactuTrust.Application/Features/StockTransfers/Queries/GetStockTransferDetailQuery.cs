using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.StockTransfers.Queries;

public sealed record GetStockTransferDetailQuery(Guid Id) : IRequest<Result<StockTransferDetailDto>>;

public sealed class GetStockTransferDetailQueryHandler
    : IRequestHandler<GetStockTransferDetailQuery, Result<StockTransferDetailDto>>
{
    private readonly IStockTransferRepository _repository;

    public GetStockTransferDetailQueryHandler(IStockTransferRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<StockTransferDetailDto>> Handle(
        GetStockTransferDetailQuery request, CancellationToken cancellationToken)
    {
        var transfer = await _repository.GetByIdWithLinesAsync(request.Id, cancellationToken);
        if (transfer is null)
            return Result.Failure<StockTransferDetailDto>(Error.NotFound("StockTransfer", request.Id));

        var dto = new StockTransferDetailDto
        {
            Id = transfer.Id,
            Number = transfer.Number.Value,
            TransferDate = transfer.TransferDate,
            Status = transfer.Status,
            StatusDisplay = transfer.Status.ToDisplayString(),
            StatusCss = transfer.Status.ToCssClass(),
            Reference = transfer.Reference,
            Notes = transfer.Notes,
            SourceWarehouseId = transfer.SourceWarehouseId,
            SourceWarehouseName = transfer.SourceWarehouse?.Name ?? "",
            SourceWarehouseAddress = transfer.SourceWarehouse?.Address,
            DestinationWarehouseId = transfer.DestinationWarehouseId,
            DestinationWarehouseName = transfer.DestinationWarehouse?.Name ?? "",
            DestinationWarehouseAddress = transfer.DestinationWarehouse?.Address,
            Lines = transfer.Lines.Select(l => new StockTransferLineDto
            {
                Id = l.Id,
                LineNumber = l.LineNumber,
                ProductId = l.ProductId,
                ProductCode = l.ProductCode,
                ProductName = l.ProductName,
                RequestedQuantity = l.RequestedQuantity,
                TransferredQuantity = l.TransferredQuantity,
                IsFullyTransferred = l.IsFullyTransferred,
                Notes = l.Notes
            }).ToList(),
            TotalRequestedQuantity = transfer.TotalRequestedQuantity,
            TotalTransferredQuantity = transfer.TotalTransferredQuantity,
            ConfirmedAt = transfer.ConfirmedAt,
            CompletedAt = transfer.CompletedAt,
            CancelledAt = transfer.CancelledAt,
            CancellationReason = transfer.CancellationReason,
            CreatedAt = transfer.CreatedAt,
            UpdatedAt = transfer.UpdatedAt
        };

        return Result.Success(dto);
    }
}
