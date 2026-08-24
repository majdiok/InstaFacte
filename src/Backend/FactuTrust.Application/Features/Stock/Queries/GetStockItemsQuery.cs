using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Stock.Queries;

/// <summary>
/// Query to get stock items with filtering and pagination.
/// </summary>
public sealed record GetStockItemsQuery(
    Guid? WarehouseId,
    bool? LowStockOnly,
    bool? OutOfStockOnly,
    int Page = 1,
    int PageSize = 20) : IRequest<StockItemsResult>;

/// <summary>
/// Result for stock items query.
/// </summary>
public sealed record StockItemsResult(
    IReadOnlyList<StockItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>
/// DTO for stock item.
/// </summary>
public sealed record StockItemDto(
    Guid Id,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    decimal QuantityOnHand,
    decimal QuantityAvailable,
    decimal QuantityReserved,
    decimal MinimumStock,
    decimal? MaximumStock,
    decimal AverageCost,
    decimal StockValue,
    bool IsLowStock,
    bool IsOutOfStock,
    CostingMethod CostingMethod);

/// <summary>
/// Handler for GetStockItemsQuery.
/// </summary>
public sealed class GetStockItemsQueryHandler : IRequestHandler<GetStockItemsQuery, StockItemsResult>
{
    private readonly IStockItemRepository _stockItemRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IProductRepository _productRepository;
    private readonly ITenantContext _tenantContext;

    public GetStockItemsQueryHandler(
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

    public async Task<StockItemsResult> Handle(GetStockItemsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _stockItemRepository.SearchAsync(
            searchTerm: null,
            warehouseId: request.WarehouseId,
            lowStockOnly: request.LowStockOnly,
            outOfStockOnly: request.OutOfStockOnly,
            page: request.Page,
            pageSize: request.PageSize,
            cancellationToken: cancellationToken);

        // Get warehouses and products for the items to fill in names
        var warehouseIds = items.Select(i => i.WarehouseId).Distinct().ToList();
        var productIds = items.Select(i => i.ProductId).Distinct().ToList();

        var warehouses = await _warehouseRepository.GetActiveWarehousesAsync(cancellationToken);
        var warehouseDict = warehouses.ToDictionary(w => w.Id);

        // Build product lookup (simplified - in real app would batch load)
        var productDict = new Dictionary<Guid, Product>();
        foreach (var productId in productIds)
        {
            var product = await _productRepository.GetByIdAsync(productId, cancellationToken);
            if (product != null)
                productDict[productId] = product;
        }

        var dtos = items.Select(item =>
        {
            warehouseDict.TryGetValue(item.WarehouseId, out var warehouse);
            productDict.TryGetValue(item.ProductId, out var product);

            return new StockItemDto(
                item.Id,
                item.ProductId,
                product?.Code ?? "N/A",
                product?.Name ?? "N/A",
                item.WarehouseId,
                warehouse?.Code ?? "N/A",
                warehouse?.Name ?? "N/A",
                item.QuantityOnHand,
                item.QuantityAvailable,
                item.QuantityReserved,
                item.MinimumStock,
                item.MaximumStock,
                item.AverageCost,
                item.StockValue,
                item.IsLowStock,
                item.QuantityOnHand == 0,
                product?.CostingMethod ?? CostingMethod.Average);
        }).ToList();

        return new StockItemsResult(dtos, totalCount, request.Page, request.PageSize);
    }
}
