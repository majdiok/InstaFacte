using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Products.Queries;

/// <summary>
/// Query to get product details by ID.
/// </summary>
public sealed record GetProductByIdQuery(Guid Id) : IRequest<Result<ProductDetailDto>>;

/// <summary>
/// Handler for GetProductByIdQuery.
/// </summary>
public sealed class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, Result<ProductDetailDto>>
{
    private readonly IProductRepository _productRepository;

    public GetProductByIdQueryHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<Result<ProductDetailDto>> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken);
        if (product is null)
            return Result.Failure<ProductDetailDto>(Error.NotFound("Product", request.Id));

        var dto = new ProductDetailDto
        {
            Id = product.Id,
            Code = product.Code,
            Name = product.Name,
            Description = product.Description,
            Type = product.Type,
            TypeDisplay = product.Type.ToDisplayString(),
            UnitPrice = product.UnitPrice.Amount,
            PurchasePrice = product.PurchasePrice?.Amount,
            Currency = product.UnitPrice.Currency,
            VatRate = product.VatRate,
            VatRatePercent = (int)product.VatRate,
            VatRateDisplay = product.VatRate.ToDisplayString(),
            Unit = product.Unit,
            IsActive = product.IsActive,
            IsStockManaged = product.IsStockManaged,
            IsFodecApplicable = product.IsFodecApplicable,
            CategoryId = product.CategoryId,
            CategoryName = product.Category.Name,
            ImageUrl = product.ImageUrl,
            PreferredSupplierId = product.PreferredSupplierId,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt
        };

        return Result.Success(dto);
    }
}
