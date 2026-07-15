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
/// Moves a <c>Draft</c> storefront to <c>PendingReview</c> so the platform admin can moderate it.
/// Completeness invariants (description, logo, etc.) are enforced by the domain.
/// </summary>
public sealed record SubmitStorefrontForReviewCommand() : IRequest<Result<StorefrontProfileDto>>;

public sealed class SubmitStorefrontForReviewCommandHandler
    : IRequestHandler<SubmitStorefrontForReviewCommand, Result<StorefrontProfileDto>>
{
    private readonly IStorefrontProfileRepository _profileRepository;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly StorefrontOptions _options;

    public SubmitStorefrontForReviewCommandHandler(
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
        SubmitStorefrontForReviewCommand request,
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

        var submit = profile.SubmitForReview();
        if (submit.IsFailure)
            return Result.Failure<StorefrontProfileDto>(submit.Error);

        await _profileRepository.UpdateAsync(profile, cancellationToken);

        return Result.Success(StorefrontProfileMapper.ToDto(profile));
    }
}
