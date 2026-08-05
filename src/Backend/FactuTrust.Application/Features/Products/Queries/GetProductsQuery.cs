using System.Diagnostics;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Products;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Application.Features.Products.Queries;

/// <summary>
/// Query to get paginated products with optional search and filters.
/// </summary>
public sealed record GetProductsQuery(
    string? Search = null,
    ProductType? Type = null,
    bool? IsActive = null,
    Guid? CategoryId = null,
    int Page = 1,
    int PageSize = 20,
    Guid? WarehouseId = null) : IRequest<Result<PagedResult<ProductListDto>>>;

/// <summary>
/// Handler for GetProductsQuery.
/// </summary>
public sealed class GetProductsQueryHandler : IRequestHandler<GetProductsQuery, Result<PagedResult<ProductListDto>>>
{
    private readonly IProductRepository _productRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IStockItemRepository _stockItemRepository;
    private readonly ILogger<GetProductsQueryHandler> _logger;

    public GetProductsQueryHandler(
        IProductRepository productRepository,
        IWarehouseRepository warehouseRepository,
        IStockItemRepository stockItemRepository,
        ILogger<GetProductsQueryHandler> logger)
    {
        _productRepository = productRepository;
        _warehouseRepository = warehouseRepository;
        _stockItemRepository = stockItemRepository;
        _logger = logger;
    }

    public async Task<Result<PagedResult<ProductListDto>>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        var totalSw = Stopwatch.StartNew();

        var searchSw = Stopwatch.StartNew();
        var (items, totalCount) = await _productRepository.SearchAsync(
            request.Search,
            request.Type,
            request.IsActive,
            request.CategoryId,
            request.Page,
            request.PageSize,
            cancellationToken);
        searchSw.Stop();

        var stockSw = Stopwatch.StartNew();
        Warehouse? stockWarehouse;
        if (request.WarehouseId is { } wid)
        {
            var wh = await _warehouseRepository.GetByIdAsync(wid, cancellationToken);
            if (wh is null)
                return Result.Failure<PagedResult<ProductListDto>>(Error.NotFound("Warehouse", wid));
            if (!wh.IsActive)
                return Result.Failure<PagedResult<ProductListDto>>(
                    Error.Validation("Warehouse", "Cet entrepôt est désactivé."));
            stockWarehouse = wh;
        }
        else
        {
            stockWarehouse = await _warehouseRepository.GetDefaultAsync(cancellationToken);
        }

        var stockManagedIds = items.Where(p => p.IsStockManaged).Select(p => p.Id).Distinct().ToList();

        IReadOnlyDictionary<Guid, decimal> qtyByProduct = new Dictionary<Guid, decimal>();
        if (stockWarehouse != null && stockManagedIds.Count > 0)
        {
            qtyByProduct = await _stockItemRepository.GetAvailableQuantityByProductIdsAsync(
                stockWarehouse.Id,
                stockManagedIds,
                cancellationToken);
        }
        stockSw.Stop();

        var dtos = items.Select(p => ProductDetailMapper.ToListDto(
            p,
            p.IsStockManaged && stockWarehouse != null
                ? (qtyByProduct.TryGetValue(p.Id, out var q) ? q : 0m)
                : null)).ToList();

        totalSw.Stop();
        _logger.LogDebug(
            "GetProducts completed in {TotalMs}ms (search={SearchMs}ms, stock={StockMs}ms, searchEmpty={SearchEmpty}, pageSize={PageSize}, resultCount={ResultCount})",
            totalSw.ElapsedMilliseconds,
            searchSw.ElapsedMilliseconds,
            stockSw.ElapsedMilliseconds,
            string.IsNullOrWhiteSpace(request.Search),
            request.PageSize,
            dtos.Count);

        return Result.Success(PagedResult<ProductListDto>.Create(dtos, request.Page, request.PageSize, totalCount));
    }
}
