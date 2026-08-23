using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Storefront.DTOs;
using FactuTrust.Domain.Common;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Storefront.TenantAdmin.Queries;

/// <summary>
/// Returns the tenant's products with their current public-visibility flag so the admin UI can
/// display toggles. Results are paged to keep the response bounded.
/// </summary>
public sealed record GetStorefrontEligibleProductsQuery(
    string? SearchTerm,
    bool? OnlyPubliclyListed,
    int Page = 1,
    int PageSize = 25)
    : IRequest<Result<PagedResult<StorefrontEligibleProductDto>>>;

public sealed class GetStorefrontEligibleProductsQueryValidator
    : AbstractValidator<GetStorefrontEligibleProductsQuery>
{
    public GetStorefrontEligibleProductsQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThan(0);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
        RuleFor(q => q.SearchTerm).MaximumLength(120);
    }
}

public sealed class GetStorefrontEligibleProductsQueryHandler
    : IRequestHandler<GetStorefrontEligibleProductsQuery, Result<PagedResult<StorefrontEligibleProductDto>>>
{
    private readonly IProductRepository _productRepository;
    private readonly IProductCategoryRepository _categoryRepository;
    private readonly IStorefrontProfileRepository _profileRepository;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly StorefrontOptions _options;

    public GetStorefrontEligibleProductsQueryHandler(
        IProductRepository productRepository,
        IProductCategoryRepository categoryRepository,
        IStorefrontProfileRepository profileRepository,
        ICurrentUser currentUser,
        ITenantContext tenantContext,
        IOptions<StorefrontOptions> options)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
        _profileRepository = profileRepository;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _options = options.Value;
    }

    public async Task<Result<PagedResult<StorefrontEligibleProductDto>>> Handle(
        GetStorefrontEligibleProductsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return Result.Failure<PagedResult<StorefrontEligibleProductDto>>(
                Error.Forbidden("La fonctionnalité Rue virtuelle est désactivée."));

        var tenantId = _tenantContext.TenantId ?? _currentUser.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            return Result.Failure<PagedResult<StorefrontEligibleProductDto>>(
                Error.Unauthorized("Aucun contexte d'entreprise disponible."));

        // Defensive: if the tenant does not yet have a storefront profile, listings still work
        // so the UI can show 'activate then publish' flow. No access is leaked.
        _ = await _profileRepository.GetByTenantIdAsync(tenantId.Value, cancellationToken);

        var (items, totalCount) = await _productRepository.SearchAsync(
            searchTerm: request.SearchTerm,
            type: null,
            isActive: true,
            categoryId: null,
            page: request.Page,
            pageSize: request.PageSize,
            cancellationToken);

        var categories = await _categoryRepository.GetAllAsync(cancellationToken);
        var categoryMap = categories.ToDictionary(c => c.Id, c => c.Name);

        var eligible = items.Where(p => !p.IsVariantTemplate).ToList();
        var filtered = request.OnlyPubliclyListed is true
            ? eligible.Where(p => p.IsPubliclyListed).ToList()
            : (IReadOnlyList<Domain.Entities.Product>)eligible;

        var dtos = filtered.Select(p => new StorefrontEligibleProductDto
        {
            Id = p.Id,
            Code = p.Code,
            Name = p.Name,
            UnitPrice = p.UnitPrice.Amount,
            Currency = p.UnitPrice.Currency,
            CategoryLabel = categoryMap.TryGetValue(p.CategoryId, out var label) ? label : null,
            ImageUrl = p.ImageUrl,
            IsActive = p.IsActive,
            IsPubliclyListed = p.IsPubliclyListed,
        }).ToList();

        var paged = PagedResult<StorefrontEligibleProductDto>.Create(
            dtos,
            request.Page,
            request.PageSize,
            request.OnlyPubliclyListed is true ? dtos.Count : totalCount);

        return Result.Success(paged);
    }
}
