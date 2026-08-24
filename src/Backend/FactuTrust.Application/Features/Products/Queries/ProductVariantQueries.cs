using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Products.Queries;

public sealed record GetProductVariantsQuery(
    Guid ParentProductId,
    int Page = 1,
    int PageSize = 100,
    Guid? WarehouseId = null) : IRequest<Result<PagedResult<ProductVariantChildDto>>>;

public sealed class GetProductVariantsQueryHandler
    : IRequestHandler<GetProductVariantsQuery, Result<PagedResult<ProductVariantChildDto>>>
{
    private readonly IProductRepository _products;
    private readonly IProductAttributeRepository _attributes;
    private readonly IWarehouseRepository _warehouses;
    private readonly IStockItemRepository _stockItems;

    public GetProductVariantsQueryHandler(
        IProductRepository products,
        IProductAttributeRepository attributes,
        IWarehouseRepository warehouses,
        IStockItemRepository stockItems)
    {
        _products = products;
        _attributes = attributes;
        _warehouses = warehouses;
        _stockItems = stockItems;
    }

    public async Task<Result<PagedResult<ProductVariantChildDto>>> Handle(
        GetProductVariantsQuery request,
        CancellationToken cancellationToken)
    {
        var parent = await _products.GetByIdAsync(request.ParentProductId, cancellationToken);
        if (parent is null)
            return Result.Failure<PagedResult<ProductVariantChildDto>>(Error.NotFound("Product", request.ParentProductId));

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 100 : Math.Min(request.PageSize, 500);

        var (children, totalCount) = await _products.GetChildrenByParentIdAsync(
            request.ParentProductId, page, pageSize, cancellationToken);

        var childIds = children.Select(c => c.Id).ToList();
        var attrsByProduct = await _attributes.GetVariantAttributesByProductIdsAsync(childIds, cancellationToken);

        var warehouse = request.WarehouseId is { } wid
            ? await _warehouses.GetByIdAsync(wid, cancellationToken)
            : await _warehouses.GetDefaultAsync(cancellationToken);

        IReadOnlyDictionary<Guid, decimal> qtyByProduct = new Dictionary<Guid, decimal>();
        if (warehouse is not null)
        {
            var stockManagedIds = children.Where(c => c.IsStockManaged).Select(c => c.Id).ToList();
            if (stockManagedIds.Count > 0)
            {
                qtyByProduct = await _stockItems.GetAvailableQuantityByProductIdsAsync(
                    warehouse.Id, stockManagedIds, cancellationToken);
            }
        }

        var dtos = children.Select(c => new ProductVariantChildDto
        {
            Id = c.Id,
            Code = c.Code,
            Name = c.Name,
            UnitPrice = c.UnitPrice.Amount,
            PurchasePrice = c.PurchasePrice?.Amount,
            SalePriceTtc = c.CalculateSalePriceTtc(),
            Barcode = c.Barcode?.Value,
            IsActive = c.IsActive,
            QuantityAvailable = c.IsStockManaged && warehouse is not null
                ? (qtyByProduct.TryGetValue(c.Id, out var q) ? q : 0m)
                : null,
            Attributes = attrsByProduct.TryGetValue(c.Id, out var attrs)
                ? attrs
                : Array.Empty<ProductVariantAttributePairDto>()
        }).ToList();

        return Result.Success(PagedResult<ProductVariantChildDto>.Create(dtos, page, pageSize, totalCount));
    }
}

public sealed record GetProductVariantAxesQuery(Guid ParentProductId)
    : IRequest<Result<IReadOnlyList<ProductVariantAxisDto>>>;

public sealed class GetProductVariantAxesQueryHandler
    : IRequestHandler<GetProductVariantAxesQuery, Result<IReadOnlyList<ProductVariantAxisDto>>>
{
    private readonly IProductRepository _products;
    private readonly IProductAttributeRepository _attributes;

    public GetProductVariantAxesQueryHandler(
        IProductRepository products,
        IProductAttributeRepository attributes)
    {
        _products = products;
        _attributes = attributes;
    }

    public async Task<Result<IReadOnlyList<ProductVariantAxisDto>>> Handle(
        GetProductVariantAxesQuery request,
        CancellationToken cancellationToken)
    {
        var parent = await _products.GetByIdAsync(request.ParentProductId, cancellationToken);
        if (parent is null)
            return Result.Failure<IReadOnlyList<ProductVariantAxisDto>>(Error.NotFound("Product", request.ParentProductId));

        var axes = await _attributes.ListAxesAsync(request.ParentProductId, cancellationToken);
        if (axes.Count == 0)
            return Result.Success<IReadOnlyList<ProductVariantAxisDto>>(Array.Empty<ProductVariantAxisDto>());

        var (children, _) = await _products.GetChildrenByParentIdAsync(
            request.ParentProductId, 1, 500, cancellationToken);
        var childIds = children.Select(c => c.Id).ToList();
        var attrsByChild = await _attributes.GetVariantAttributesByProductIdsAsync(childIds, cancellationToken);

        var selectedByDefinition = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var pairs in attrsByChild.Values)
        {
            foreach (var pair in pairs)
            {
                if (!selectedByDefinition.TryGetValue(pair.DefinitionId, out var set))
                {
                    set = new HashSet<Guid>();
                    selectedByDefinition[pair.DefinitionId] = set;
                }
                set.Add(pair.ValueId);
            }
        }

        var dtos = new List<ProductVariantAxisDto>();
        foreach (var axis in axes)
        {
            var definition = await _attributes.GetByIdWithValuesAsync(axis.DefinitionId, cancellationToken);
            if (definition is null)
                continue;

            selectedByDefinition.TryGetValue(axis.DefinitionId, out var selected);
            dtos.Add(new ProductVariantAxisDto
            {
                DefinitionId = definition.Id,
                Code = definition.Code,
                Name = definition.Name,
                SortOrder = axis.SortOrder,
                SelectedValueIds = selected is null ? Array.Empty<Guid>() : selected.ToList()
            });
        }

        return Result.Success<IReadOnlyList<ProductVariantAxisDto>>(dtos);
    }
}

public sealed record GetProductVariantProfileQuery(Guid ProductId)
    : IRequest<Result<ProductVariantProfileDto>>;

public sealed class GetProductVariantProfileQueryHandler
    : IRequestHandler<GetProductVariantProfileQuery, Result<ProductVariantProfileDto>>
{
    private readonly IProductRepository _products;
    private readonly IProductAttributeRepository _attributes;

    public GetProductVariantProfileQueryHandler(
        IProductRepository products,
        IProductAttributeRepository attributes)
    {
        _products = products;
        _attributes = attributes;
    }

    public async Task<Result<ProductVariantProfileDto>> Handle(
        GetProductVariantProfileQuery request,
        CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null)
            return Result.Failure<ProductVariantProfileDto>(Error.NotFound("Product", request.ProductId));

        if (!product.ParentProductId.HasValue)
            return Result.Failure<ProductVariantProfileDto>(
                Error.Validation("ParentProductId", "Ce produit n'est pas une variante enfant."));

        var parent = await _products.GetByIdAsync(product.ParentProductId.Value, cancellationToken);
        if (parent is null)
            return Result.Failure<ProductVariantProfileDto>(Error.NotFound("Product", product.ParentProductId.Value));

        var attrs = await _attributes.GetVariantAttributesByProductIdsAsync(
            new[] { product.Id }, cancellationToken);

        return Result.Success(new ProductVariantProfileDto
        {
            ProductId = product.Id,
            ParentProductId = parent.Id,
            ParentCode = parent.Code,
            ParentName = parent.Name,
            Attributes = attrs.TryGetValue(product.Id, out var pairs)
                ? pairs
                : Array.Empty<ProductVariantAttributePairDto>()
        });
    }
}

public sealed record SearchProductTemplatesForSelectQuery(
    string? Search = null,
    bool? IsActive = true,
    int PageSize = 50) : IRequest<Result<IReadOnlyList<ProductSelectDto>>>;

public sealed class SearchProductTemplatesForSelectQueryHandler
    : IRequestHandler<SearchProductTemplatesForSelectQuery, Result<IReadOnlyList<ProductSelectDto>>>
{
    private readonly IProductRepository _products;

    public SearchProductTemplatesForSelectQueryHandler(IProductRepository products)
    {
        _products = products;
    }

    public async Task<Result<IReadOnlyList<ProductSelectDto>>> Handle(
        SearchProductTemplatesForSelectQuery request,
        CancellationToken cancellationToken)
    {
        var pageSize = request.PageSize < 1 ? 50 : Math.Min(request.PageSize, 100);
        var items = await _products.SearchTemplatesForSelectAsync(
            request.Search, request.IsActive, pageSize, cancellationToken);

        var dtos = items.Select(p => new ProductSelectDto
        {
            Id = p.Id,
            Code = p.Code,
            Name = p.Name,
            UnitPrice = p.UnitPrice.Amount,
            PurchasePrice = p.PurchasePrice?.Amount,
            VatRatePercent = (int)p.VatRate,
            Unit = p.Unit,
            IsFodecApplicable = p.IsFodecApplicable,
            IsDiscountEnabled = p.IsDiscountEnabled,
            MaxDiscountPercent = p.MaxDiscountPercent,
            IsVariantTemplate = true,
            ParentProductId = null
        }).ToList();

        return Result.Success<IReadOnlyList<ProductSelectDto>>(dtos);
    }
}
