using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Storefront.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Storefront;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Storefront.TenantAdmin.Commands;

/// <summary>
/// Lets a tenant take their own public storefront back offline (e.g. RGPD right-to-withdraw).
/// Transitions <c>Published</c> back to <c>Draft</c>. The profile (and its audit trail) is preserved
/// so the tenant can resubmit it later; the public projection is scheduled for removal by the
/// background sync service.
/// </summary>
public sealed record UnpublishStorefrontCommand() : IRequest<Result<StorefrontProfileDto>>;

public sealed class UnpublishStorefrontCommandHandler
    : IRequestHandler<UnpublishStorefrontCommand, Result<StorefrontProfileDto>>
{
    private readonly IStorefrontProfileRepository _profileRepository;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly StorefrontOptions _options;

    public UnpublishStorefrontCommandHandler(
        IStorefrontProfileRepository profileRepository,
        ICurrentUser currentUser,
        ITenantContext tenantContext,
        IOptions<StorefrontOptions> options)
    {
        _profileRepository = profileRepository;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _options = options.Value;
    }

    public async Task<Result<StorefrontProfileDto>> Handle(
        UnpublishStorefrontCommand request,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return Result.Failure<StorefrontProfileDto>(Error.Forbidden("La fonctionnalité Rue virtuelle est désactivée."));

        var tenantId = _tenantContext.TenantId ?? _currentUser.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            return Result.Failure<StorefrontProfileDto>(Error.Unauthorized("Aucun contexte d'entreprise disponible."));

        var profile = await _profileRepository.GetByTenantIdAsync(tenantId.Value, cancellationToken);
        if (profile is null)
            return Result.Failure<StorefrontProfileDto>(
                Error.NotFound(nameof(StorefrontProfile), tenantId.Value));

        var unpublish = profile.Unpublish();
        if (unpublish.IsFailure)
            return Result.Failure<StorefrontProfileDto>(unpublish.Error);

        await _profileRepository.UpdateAsync(profile, cancellationToken);

        return Result.Success(StorefrontProfileMapper.ToDto(profile));
    }
}
