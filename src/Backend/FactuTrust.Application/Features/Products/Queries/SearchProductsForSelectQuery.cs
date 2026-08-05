using System.Diagnostics;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Products;
using FactuTrust.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Application.Features.Products.Queries;

/// <summary>
/// Lightweight product search for autocomplete / select dropdowns.
/// Returns <see cref="ProductSelectDto"/> without stock or category joins.
/// </summary>
public sealed record SearchProductsForSelectQuery(
    string? Search = null,
    bool? IsActive = true,
    int Page = 1,
    int PageSize = 50) : IRequest<Result<PagedResult<ProductSelectDto>>>;

public sealed class SearchProductsForSelectQueryHandler
    : IRequestHandler<SearchProductsForSelectQuery, Result<PagedResult<ProductSelectDto>>>
{
    public const int MaxPageSize = 100;

    private readonly IProductRepository _productRepository;
    private readonly ILogger<SearchProductsForSelectQueryHandler> _logger;

    public SearchProductsForSelectQueryHandler(
        IProductRepository productRepository,
        ILogger<SearchProductsForSelectQueryHandler> logger)
    {
        _productRepository = productRepository;
        _logger = logger;
    }

    public async Task<Result<PagedResult<ProductSelectDto>>> Handle(
        SearchProductsForSelectQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1
            ? 50
            : Math.Min(request.PageSize, MaxPageSize);

        var sw = Stopwatch.StartNew();
        var items = await _productRepository.SearchForSelectAsync(
            request.Search,
            request.IsActive,
            pageSize,
            cancellationToken);
        sw.Stop();

        var dtos = items.Select(ProductDetailMapper.ToSelectDto).ToList();
        // Approximate total: avoid a second COUNT round-trip for autocomplete.
        var totalCount = dtos.Count == pageSize
            ? page * pageSize + 1
            : (page - 1) * pageSize + dtos.Count;

        _logger.LogDebug(
            "SearchProductsForSelect completed in {ElapsedMs}ms (searchEmpty={SearchEmpty}, pageSize={PageSize}, resultCount={ResultCount})",
            sw.ElapsedMilliseconds,
            string.IsNullOrWhiteSpace(request.Search),
            pageSize,
            dtos.Count);

        return Result.Success(PagedResult<ProductSelectDto>.Create(dtos, page, pageSize, totalCount));
    }
}
