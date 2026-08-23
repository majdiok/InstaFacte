using FactuTrust.Application.Common.Interfaces.Repositories;
using MediatR;

namespace FactuTrust.Application.Features.Stock.Queries;

public sealed record ProductSerialDto(
    Guid Id,
    string SerialNumber,
    Guid? ProductLotId,
    string? LotNumber,
    DateTime? ExpiryDate);

public sealed record GetProductLotsByProductQuery(Guid ProductId, Guid WarehouseId)
    : IRequest<IReadOnlyList<StockLotBalanceDto>>;

public sealed class GetProductLotsByProductQueryHandler
    : IRequestHandler<GetProductLotsByProductQuery, IReadOnlyList<StockLotBalanceDto>>
{
    private readonly IStockItemRepository _stockItems;
    private readonly IStockTraceabilityQuery _traceability;

    public GetProductLotsByProductQueryHandler(
        IStockItemRepository stockItems,
        IStockTraceabilityQuery traceability)
    {
        _stockItems = stockItems;
        _traceability = traceability;
    }

    public async Task<IReadOnlyList<StockLotBalanceDto>> Handle(
        GetProductLotsByProductQuery request,
        CancellationToken cancellationToken)
    {
        var item = await _stockItems.GetByProductAndWarehouseAsync(
            request.ProductId, request.WarehouseId, cancellationToken);
        if (item is null)
            return Array.Empty<StockLotBalanceDto>();

        return await _traceability.ListLotsAsync(item.Id, cancellationToken);
    }
}

public sealed record GetProductSerialsQuery(Guid ProductId, Guid WarehouseId)
    : IRequest<IReadOnlyList<ProductSerialDto>>;

public sealed class GetProductSerialsQueryHandler
    : IRequestHandler<GetProductSerialsQuery, IReadOnlyList<ProductSerialDto>>
{
    private readonly IStockTraceabilityQuery _traceability;

    public GetProductSerialsQueryHandler(IStockTraceabilityQuery traceability)
    {
        _traceability = traceability;
    }

    public Task<IReadOnlyList<ProductSerialDto>> Handle(
        GetProductSerialsQuery request,
        CancellationToken cancellationToken) =>
        _traceability.ListInStockSerialsAsync(request.ProductId, request.WarehouseId, cancellationToken);
}

public sealed record ProductTraceabilityContextDto(
    Guid ProductId,
    Domain.Enums.TrackingMode TrackingMode,
    Domain.Enums.PickingPolicy PickingPolicy,
    bool HasExpiryTracking,
    int LotCount,
    int SerialCount);

public sealed record GetTraceabilityContextQuery(
    IReadOnlyList<Guid> ProductIds,
    Guid WarehouseId) : IRequest<IReadOnlyList<ProductTraceabilityContextDto>>;

public sealed class GetTraceabilityContextQueryHandler
    : IRequestHandler<GetTraceabilityContextQuery, IReadOnlyList<ProductTraceabilityContextDto>>
{
    private readonly IStockTraceabilityQuery _traceability;

    public GetTraceabilityContextQueryHandler(IStockTraceabilityQuery traceability)
    {
        _traceability = traceability;
    }

    public Task<IReadOnlyList<ProductTraceabilityContextDto>> Handle(
        GetTraceabilityContextQuery request,
        CancellationToken cancellationToken) =>
        _traceability.ListTraceabilityContextAsync(request.ProductIds, request.WarehouseId, cancellationToken);
}
