using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Products;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using MediatR;

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

    public GetProductsQueryHandler(
        IProductRepository productRepository,
        IWarehouseRepository warehouseRepository,
        IStockItemRepository stockItemRepository)
    {
        _productRepository = productRepository;
        _warehouseRepository = warehouseRepository;
        _stockItemRepository = stockItemRepository;
    }

    public async Task<Result<PagedResult<ProductListDto>>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _productRepository.SearchAsync(
            request.Search,
            request.Type,
            request.IsActive,
            request.CategoryId,
            request.Page,
            request.PageSize,
            cancellationToken);

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

        var dtos = items.Select(p => ProductDetailMapper.ToListDto(
            p,
            p.IsStockManaged && stockWarehouse != null
                ? (qtyByProduct.TryGetValue(p.Id, out var q) ? q : 0m)
                : null)).ToList();

        return Result.Success(PagedResult<ProductListDto>.Create(dtos, request.Page, request.PageSize, totalCount));
    }
}
