using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using MediatR;

namespace FactuTrust.Application.Features.ProductCategories.Queries;

/// <summary>
/// Query to get product categories for dropdown (active only).
/// </summary>
public sealed record GetProductCategoriesQuery(bool ActiveOnly = true) : IRequest<IReadOnlyList<ProductCategorySelectDto>>;

/// <summary>
/// Handler for GetProductCategoriesQuery.
/// </summary>
public sealed class GetProductCategoriesQueryHandler : IRequestHandler<GetProductCategoriesQuery, IReadOnlyList<ProductCategorySelectDto>>
{
    private readonly IProductCategoryRepository _productCategoryRepository;

    public GetProductCategoriesQueryHandler(IProductCategoryRepository productCategoryRepository)
    {
        _productCategoryRepository = productCategoryRepository;
    }

    public async Task<IReadOnlyList<ProductCategorySelectDto>> Handle(GetProductCategoriesQuery request, CancellationToken cancellationToken)
    {
        var categories = request.ActiveOnly
            ? await _productCategoryRepository.GetActiveAsync(cancellationToken)
            : await _productCategoryRepository.GetAllAsync(cancellationToken);

        return categories.Select(c => new ProductCategorySelectDto
        {
            Id = c.Id,
            Name = c.Name
        }).ToList();
    }
}
