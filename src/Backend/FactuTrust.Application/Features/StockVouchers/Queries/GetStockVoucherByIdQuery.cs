using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.StockVouchers.Queries;

public sealed record GetStockVoucherByIdQuery(Guid Id) : IRequest<Result<StockVoucherDetailDto>>;

public sealed class GetStockVoucherByIdQueryHandler
    : IRequestHandler<GetStockVoucherByIdQuery, Result<StockVoucherDetailDto>>
{
    private readonly IStockVoucherRepository _repository;

    public GetStockVoucherByIdQueryHandler(IStockVoucherRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<StockVoucherDetailDto>> Handle(
        GetStockVoucherByIdQuery request,
        CancellationToken cancellationToken)
    {
        var voucher = await _repository.GetByIdWithLinesAsync(request.Id, cancellationToken);
        if (voucher is null)
            return Result.Failure<StockVoucherDetailDto>(Error.NotFound("StockVoucher", request.Id));

        return Result.Success(StockVoucherDtoMapper.ToDetailDto(voucher));
    }
}
