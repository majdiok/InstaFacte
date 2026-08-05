using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Products.Queries;

/// <summary>FODEC applicability flag for a single product.</summary>
public sealed record ProductFodecFlagDto
{
    public Guid Id { get; init; }
    public bool IsFodecApplicable { get; init; }
}

/// <summary>
/// Bulk FODEC flag lookup — avoids N× GET /products/{id} on draft/import reload.
/// </summary>
public sealed record GetProductFodecFlagsQuery(
    IReadOnlyList<Guid> ProductIds) : IRequest<Result<IReadOnlyList<ProductFodecFlagDto>>>;

public sealed class GetProductFodecFlagsQueryHandler
    : IRequestHandler<GetProductFodecFlagsQuery, Result<IReadOnlyList<ProductFodecFlagDto>>>
{
    private const int MaxIds = 200;

    private readonly IProductRepository _productRepository;

    public GetProductFodecFlagsQueryHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<Result<IReadOnlyList<ProductFodecFlagDto>>> Handle(
        GetProductFodecFlagsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.ProductIds is null || request.ProductIds.Count == 0)
            return Result.Success<IReadOnlyList<ProductFodecFlagDto>>(Array.Empty<ProductFodecFlagDto>());

        if (request.ProductIds.Count > MaxIds)
        {
            return Result.Failure<IReadOnlyList<ProductFodecFlagDto>>(
                Error.Validation("ProductIds", $"Un lot ne peut pas dépasser {MaxIds} produits"));
        }

        var distinct = request.ProductIds.Where(id => id != Guid.Empty).Distinct().ToList();
        var flags = await _productRepository.GetFodecFlagsByIdsAsync(distinct, cancellationToken);

        var dtos = flags
            .Select(kv => new ProductFodecFlagDto { Id = kv.Key, IsFodecApplicable = kv.Value })
            .ToList();

        return Result.Success<IReadOnlyList<ProductFodecFlagDto>>(dtos);
    }
}
