using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.StockVouchers.Queries;

public sealed record GetStockVouchersQuery(
    StockVoucherKind? Kind = null,
    string? Search = null,
    StockVoucherStatus? Status = null,
    Guid? WarehouseId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<StockVoucherListDto>>;

public sealed class GetStockVouchersQueryHandler
    : IRequestHandler<GetStockVouchersQuery, PagedResult<StockVoucherListDto>>
{
    private readonly IStockVoucherRepository _repository;

    public GetStockVouchersQueryHandler(IStockVoucherRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<StockVoucherListDto>> Handle(
        GetStockVouchersQuery request,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _repository.SearchAsync(
            request.Kind,
            request.Search,
            request.Status,
            request.WarehouseId,
            request.FromDate,
            request.ToDate,
            request.Page,
            request.PageSize,
            cancellationToken);

        var dtos = items.Select(StockVoucherDtoMapper.ToListDto).ToList();
        return PagedResult<StockVoucherListDto>.Create(dtos, request.Page, request.PageSize, totalCount);
    }
}

public sealed record GetStockVouchersSummaryQuery(
    StockVoucherKind? Kind = null,
    string? Search = null,
    StockVoucherStatus? Status = null,
    Guid? WarehouseId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null) : IRequest<StockVoucherListSummaryDto>;

public sealed class GetStockVouchersSummaryQueryHandler
    : IRequestHandler<GetStockVouchersSummaryQuery, StockVoucherListSummaryDto>
{
    private readonly IStockVoucherRepository _repository;

    public GetStockVouchersSummaryQueryHandler(IStockVoucherRepository repository)
    {
        _repository = repository;
    }

    public Task<StockVoucherListSummaryDto> Handle(
        GetStockVouchersSummaryQuery request,
        CancellationToken cancellationToken) =>
        _repository.GetSummaryAsync(
            request.Kind,
            request.Search,
            request.Status,
            request.WarehouseId,
            request.FromDate,
            request.ToDate,
            cancellationToken);
}
