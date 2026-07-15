using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using MediatR;

namespace FactuTrust.Application.Features.Stock.Queries;

/// <summary>
/// Query to get low stock and out-of-stock alerts.
/// </summary>
public sealed record GetStockAlertsQuery(Guid? WarehouseId = null) : IRequest<StockAlertsResult>;

/// <summary>
/// Result for stock alerts query.
/// </summary>
public sealed record StockAlertsResult(
    IReadOnlyList<TechnicalStockAlertDto> LowStockItems,
    IReadOnlyList<TechnicalStockAlertDto> OutOfStockItems,
    int TotalAlerts);

/// <summary>
/// DTO for technical stock alert (detailed view).
/// Different from StockAlertDto in DTOs namespace which is for simplified user-facing alerts.
/// </summary>
public sealed record TechnicalStockAlertDto(
    Guid StockItemId,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    Guid WarehouseId,
    string WarehouseName,
    decimal QuantityOnHand,
    decimal MinimumStock,
    decimal Deficit);

/// <summary>
/// Handler for GetStockAlertsQuery.
/// </summary>
public sealed class GetStockAlertsQueryHandler : IRequestHandler<GetStockAlertsQuery, StockAlertsResult>
{
    private readonly IStockItemRepository _stockItemRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IProductRepository _productRepository;
    private readonly ITenantContext _tenantContext;

    public GetStockAlertsQueryHandler(
        IStockItemRepository stockItemRepository,
        IWarehouseRepository warehouseRepository,
        IProductRepository productRepository,
        ITenantContext tenantContext)
    {
        _stockItemRepository = stockItemRepository;
        _warehouseRepository = warehouseRepository;
        _productRepository = productRepository;
        _tenantContext = tenantContext;
    }

    public async Task<StockAlertsResult> Handle(GetStockAlertsQuery request, CancellationToken cancellationToken)
    {
        var lowStockItems = await _stockItemRepository.GetLowStockItemsAsync(request.WarehouseId, cancellationToken);
        var outOfStockItems = await _stockItemRepository.GetOutOfStockItemsAsync(request.WarehouseId, cancellationToken);

        // Get warehouses for lookups
        var warehouses = await _warehouseRepository.GetActiveWarehousesAsync(cancellationToken);
        var warehouseDict = warehouses.ToDictionary(w => w.Id);

        // Collect all product IDs
        var allProductIds = lowStockItems.Select(i => i.ProductId)
            .Union(outOfStockItems.Select(i => i.ProductId))
            .Distinct()
            .ToList();

        // Build product lookup
        var productDict = new Dictionary<Guid, (string Code, string Name)>();
        foreach (var productId in allProductIds)
        {
            var product = await _productRepository.GetByIdAsync(productId, cancellationToken);
            if (product != null)
                productDict[productId] = (product.Code, product.Name);
        }

        var lowStockDtos = lowStockItems.Select(item =>
        {
            warehouseDict.TryGetValue(item.WarehouseId, out var warehouse);
            productDict.TryGetValue(item.ProductId, out var product);

            return new TechnicalStockAlertDto(
                item.Id,
                item.ProductId,
                product.Code ?? "N/A",
                product.Name ?? "N/A",
                item.WarehouseId,
                warehouse?.Name ?? "N/A",
                item.QuantityOnHand,
                item.MinimumStock,
                item.MinimumStock - item.QuantityOnHand);
        }).ToList();

        var outOfStockDtos = outOfStockItems.Select(item =>
        {
            warehouseDict.TryGetValue(item.WarehouseId, out var warehouse);
            productDict.TryGetValue(item.ProductId, out var product);

            return new TechnicalStockAlertDto(
                item.Id,
                item.ProductId,
                product.Code ?? "N/A",
                product.Name ?? "N/A",
                item.WarehouseId,
                warehouse?.Name ?? "N/A",
                0,
                item.MinimumStock,
                item.MinimumStock);
        }).ToList();

        return new StockAlertsResult(
            lowStockDtos,
            outOfStockDtos,
            lowStockDtos.Count + outOfStockDtos.Count);
    }
}
