using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.Storefront;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Storefront.TenantAdmin.Commands;

/// <summary>
/// Toggles whether a tenant product is listed on the public storefront.
/// The product state change and the outbox message are written to the tenant database in a single
/// SQL transaction, ensuring the public projection eventually reflects the tenant's decision without
/// risking split-brain state.
/// </summary>
/// <remarks>
/// This command is idempotent: calling it with the same value is a no-op and does not write to the
/// outbox. A tenant can only publish products when their storefront profile is not <c>Suspended</c>.
/// Inactive products cannot be made publicly visible (defense-in-depth against accidental disclosure).
/// </remarks>
public sealed record SetProductPublicVisibilityCommand(Guid ProductId, bool IsPubliclyListed)
    : IRequest<Result<bool>>;

public sealed class SetProductPublicVisibilityCommandValidator : AbstractValidator<SetProductPublicVisibilityCommand>
{
    public SetProductPublicVisibilityCommandValidator()
    {
        RuleFor(c => c.ProductId).NotEmpty();
    }
}

public sealed class SetProductPublicVisibilityCommandHandler
    : IRequestHandler<SetProductPublicVisibilityCommand, Result<bool>>
{
    private readonly IStorefrontProfileRepository _profileRepository;
    private readonly IStorefrontTenantWriter _tenantWriter;
    private readonly IProductCategoryRepository _categoryRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly StorefrontOptions _options;

    public SetProductPublicVisibilityCommandHandler(
        IStorefrontProfileRepository profileRepository,
        IStorefrontTenantWriter tenantWriter,
        IProductCategoryRepository categoryRepository,
        IProductRepository productRepository,
        ICurrentUser currentUser,
        ITenantContext tenantContext,
        IOptions<StorefrontOptions> options)
    {
        _profileRepository = profileRepository;
        _tenantWriter = tenantWriter;
        _categoryRepository = categoryRepository;
        _productRepository = productRepository;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _options = options.Value;
    }

    public async Task<Result<bool>> Handle(SetProductPublicVisibilityCommand request, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return Result.Failure<bool>(Error.Forbidden("La fonctionnalité Rue virtuelle est désactivée."));

        var tenantId = _tenantContext.TenantId ?? _currentUser.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            return Result.Failure<bool>(Error.Unauthorized("Aucun contexte d'entreprise disponible."));

        var profile = await _profileRepository.GetByTenantIdAsync(tenantId.Value, cancellationToken);
        if (profile is null)
            return Result.Failure<bool>(Error.Validation(
                "Storefront",
                "Aucune vitrine publique n'est configurée pour cette société. Activez d'abord la vitrine."));

        if (profile.Status == StorefrontStatus.Suspended)
            return Result.Failure<bool>(Error.Validation(
                "Storefront",
                "La vitrine est suspendue. La visibilité publique des produits est verrouillée."));

        if (request.IsPubliclyListed)
        {
            var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
            if (product is null)
                return Result.Failure<bool>(Error.NotFound(nameof(Product), request.ProductId));
            if (!product.IsActive)
                return Result.Failure<bool>(Error.Validation(
                    "Product",
                    "Un produit inactif ne peut pas être publié sur la vitrine publique."));
            if (product.IsVariantTemplate)
                return Result.Failure<bool>(Error.Validation(
                    "Product",
                    "Un modèle de variantes ne peut pas être publié. Publiez les SKU enfants."));
        }

        var writeResult = await _tenantWriter.SetProductPublicVisibilityAsync(
            productId: request.ProductId,
            isPubliclyListed: request.IsPubliclyListed,
            categoryLabelResolver: ResolveCategoryLabelAsync,
            storefrontProfileId: profile.Id,
            tenantId: profile.TenantId,
            cancellationToken: cancellationToken);

        if (!writeResult.ProductFound)
            return Result.Failure<bool>(Error.NotFound(nameof(Product), request.ProductId));

        return Result.Success(writeResult.Changed);
    }

    private async Task<string?> ResolveCategoryLabelAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        if (categoryId == Guid.Empty)
            return null;
        var category = await _categoryRepository.GetByIdAsync(categoryId, cancellationToken);
        return category?.Name;
    }
}
