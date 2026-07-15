using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using MediatR;

namespace FactuTrust.Application.Features.ProductCategories.Queries;

/// <summary>
/// Query to get the full list of product categories for admin (list page).
/// </summary>
public sealed record GetProductCategoriesListQuery : IRequest<IReadOnlyList<ProductCategoryDto>>;

/// <summary>
/// Handler for GetProductCategoriesListQuery.
/// </summary>
public sealed class GetProductCategoriesListQueryHandler : IRequestHandler<GetProductCategoriesListQuery, IReadOnlyList<ProductCategoryDto>>
{
    private readonly IProductCategoryRepository _productCategoryRepository;

    public GetProductCategoriesListQueryHandler(IProductCategoryRepository productCategoryRepository)
    {
        _productCategoryRepository = productCategoryRepository;
    }

    public async Task<IReadOnlyList<ProductCategoryDto>> Handle(GetProductCategoriesListQuery request, CancellationToken cancellationToken)
    {
        var categories = await _productCategoryRepository.GetAllAsync(cancellationToken);
        return categories.Select(c => new ProductCategoryDto
        {
            Id = c.Id,
            Code = c.Code,
            Name = c.Name,
            DisplayOrder = c.DisplayOrder,
            IsActive = c.IsActive
        }).ToList();
    }
}
