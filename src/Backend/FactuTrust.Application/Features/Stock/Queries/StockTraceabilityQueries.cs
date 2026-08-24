using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Stock.Queries;

public sealed record StockFeaturesDto(
    bool LotTrackingEnabled,
    bool SerialTrackingEnabled,
    bool ExpiryTrackingEnabled,
    bool ProductVariantsEnabled,
    bool FifoLifoValuationEnabled,
    bool BlockExpiredLotsOnExit,
    bool StrictTrackedAllocation);

public sealed record GetStockFeaturesQuery : IRequest<StockFeaturesDto>;

public sealed class GetStockFeaturesQueryHandler : IRequestHandler<GetStockFeaturesQuery, StockFeaturesDto>
{
    private readonly StockTraceabilityOptions _options;

    public GetStockFeaturesQueryHandler(IOptions<StockTraceabilityOptions> options)
    {
        _options = options.Value;
    }

    public Task<StockFeaturesDto> Handle(GetStockFeaturesQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(new StockFeaturesDto(
            _options.LotTrackingEnabled,
            _options.SerialTrackingEnabled,
            _options.ExpiryTrackingEnabled,
            _options.ProductVariantsEnabled,
            _options.FifoLifoValuationEnabled,
            _options.BlockExpiredLotsOnExit,
            _options.StrictTrackedAllocation));
}

public sealed record StockLotBalanceDto(
    Guid ProductLotId,
    string LotNumber,
    DateTime? ExpiryDate,
    decimal QuantityOnHand,
    decimal QuantityReserved,
    decimal QuantityAvailable);

public sealed record GetStockItemLotsQuery(Guid StockItemId) : IRequest<IReadOnlyList<StockLotBalanceDto>>;

public sealed class GetStockItemLotsQueryHandler
    : IRequestHandler<GetStockItemLotsQuery, IReadOnlyList<StockLotBalanceDto>>
{
    private readonly IStockItemRepository _stockItems;
    private readonly IStockTraceabilityQuery _traceability;

    public GetStockItemLotsQueryHandler(IStockItemRepository stockItems, IStockTraceabilityQuery traceability)
    {
        _stockItems = stockItems;
        _traceability = traceability;
    }

    public async Task<IReadOnlyList<StockLotBalanceDto>> Handle(
        GetStockItemLotsQuery request,
        CancellationToken cancellationToken)
    {
        var item = await _stockItems.GetByIdAsync(request.StockItemId, cancellationToken);
        if (item is null)
            return Array.Empty<StockLotBalanceDto>();

        return await _traceability.ListLotsAsync(item.Id, cancellationToken);
    }
}

public sealed record StockValuationLayerDto(
    DateTime ReceivedAt,
    decimal RemainingQuantity,
    decimal OriginalQuantity,
    decimal UnitCost,
    decimal RemainingValue,
    string? LotNumber,
    string? SourceReference);

public sealed record GetStockItemValuationLayersQuery(Guid StockItemId)
    : IRequest<IReadOnlyList<StockValuationLayerDto>>;

public sealed class GetStockItemValuationLayersQueryHandler
    : IRequestHandler<GetStockItemValuationLayersQuery, IReadOnlyList<StockValuationLayerDto>>
{
    private readonly IStockItemRepository _stockItems;
    private readonly IStockTraceabilityQuery _traceability;

    public GetStockItemValuationLayersQueryHandler(
        IStockItemRepository stockItems,
        IStockTraceabilityQuery traceability)
    {
        _stockItems = stockItems;
        _traceability = traceability;
    }

    public async Task<IReadOnlyList<StockValuationLayerDto>> Handle(
        GetStockItemValuationLayersQuery request,
        CancellationToken cancellationToken)
    {
        var item = await _stockItems.GetByIdAsync(request.StockItemId, cancellationToken);
        if (item is null)
            return Array.Empty<StockValuationLayerDto>();

        return await _traceability.ListValuationLayersAsync(item.Id, cancellationToken);
    }
}

public sealed record ExpiryAlertDto(
    Guid ProductId,
    string ProductCode,
    string ProductName,
    Guid WarehouseId,
    string WarehouseName,
    string LotNumber,
    DateTime ExpiryDate,
    decimal QuantityOnHand,
    int DaysRemaining);

public sealed record GetExpiryAlertsQuery(Guid? WarehouseId = null) : IRequest<IReadOnlyList<ExpiryAlertDto>>;

public sealed class GetExpiryAlertsQueryHandler : IRequestHandler<GetExpiryAlertsQuery, IReadOnlyList<ExpiryAlertDto>>
{
    private readonly IStockTraceabilityQuery _traceability;

    public GetExpiryAlertsQueryHandler(IStockTraceabilityQuery traceability)
    {
        _traceability = traceability;
    }

    public Task<IReadOnlyList<ExpiryAlertDto>> Handle(GetExpiryAlertsQuery request, CancellationToken cancellationToken) =>
        _traceability.ListExpiryAlertsAsync(request.WarehouseId, cancellationToken);
}

public interface IStockTraceabilityQuery
{
    Task<IReadOnlyList<StockLotBalanceDto>> ListLotsAsync(Guid stockItemId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StockValuationLayerDto>> ListValuationLayersAsync(Guid stockItemId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpiryAlertDto>> ListExpiryAlertsAsync(Guid? warehouseId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductSerialDto>> ListInStockSerialsAsync(
        Guid productId,
        Guid warehouseId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductTraceabilityContextDto>> ListTraceabilityContextAsync(
        IReadOnlyList<Guid> productIds,
        Guid warehouseId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, string>> GetLotLabelsAsync(
        Domain.Enums.StockDocumentKind kind,
        IReadOnlyCollection<Guid> documentLineIds,
        CancellationToken cancellationToken = default);
}

public sealed record ListProductAttributesQuery : IRequest<IReadOnlyList<ProductAttributeDto>>;

public sealed record ProductAttributeDto(
    Guid Id,
    string Code,
    string Name,
    IReadOnlyList<ProductAttributeValueDto> Values);

public sealed record ProductAttributeValueDto(Guid Id, string Code, string Name);

public sealed class ListProductAttributesQueryHandler
    : IRequestHandler<ListProductAttributesQuery, IReadOnlyList<ProductAttributeDto>>
{
    private readonly IProductAttributeRepository _attributes;

    public ListProductAttributesQueryHandler(IProductAttributeRepository attributes)
    {
        _attributes = attributes;
    }

    public async Task<IReadOnlyList<ProductAttributeDto>> Handle(
        ListProductAttributesQuery request,
        CancellationToken cancellationToken)
    {
        var items = await _attributes.ListAsync(cancellationToken);
        return items.Select(d => new ProductAttributeDto(
            d.Id,
            d.Code,
            d.Name,
            d.Values.OrderBy(v => v.SortOrder).Select(v => new ProductAttributeValueDto(v.Id, v.Code, v.Name)).ToList()))
            .ToList();
    }
}
