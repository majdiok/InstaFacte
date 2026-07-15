using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.ProductCategories.Queries;

/// <summary>
/// Query to get a product category by ID.
/// </summary>
public sealed record GetProductCategoryByIdQuery(Guid Id) : IRequest<Result<ProductCategoryDto>>;

/// <summary>
/// Handler for GetProductCategoryByIdQuery.
/// </summary>
public sealed class GetProductCategoryByIdQueryHandler : IRequestHandler<GetProductCategoryByIdQuery, Result<ProductCategoryDto>>
{
    private readonly IProductCategoryRepository _productCategoryRepository;

    public GetProductCategoryByIdQueryHandler(IProductCategoryRepository productCategoryRepository)
    {
        _productCategoryRepository = productCategoryRepository;
    }

    public async Task<Result<ProductCategoryDto>> Handle(GetProductCategoryByIdQuery request, CancellationToken cancellationToken)
    {
        var category = await _productCategoryRepository.GetByIdAsync(request.Id, cancellationToken);
        if (category is null)
            return Result.Failure<ProductCategoryDto>(Error.NotFound("ProductCategory", request.Id));

        var dto = new ProductCategoryDto
        {
            Id = category.Id,
            Code = category.Code,
            Name = category.Name,
            DisplayOrder = category.DisplayOrder,
            IsActive = category.IsActive
        };

        return Result.Success(dto);
    }
}
