using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Storefront;
using FactuTrust.Application.Features.Storefront.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Storefront;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Storefront.Platform.Commands;

public sealed record ApproveStorefrontCommand(Guid StorefrontProfileId) : IRequest<Result<StorefrontProfileDto>>;

public sealed class ApproveStorefrontCommandHandler : IRequestHandler<ApproveStorefrontCommand, Result<StorefrontProfileDto>>
{
    private readonly IStorefrontProfileRepository _profiles;
    private readonly StorefrontOptions _options;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<ApproveStorefrontCommandHandler> _logger;

    public ApproveStorefrontCommandHandler(
        IStorefrontProfileRepository profiles,
        IOptions<StorefrontOptions> options,
        ICurrentUser currentUser,
        ILogger<ApproveStorefrontCommandHandler> logger)
    {
        _profiles = profiles;
        _options = options.Value;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<StorefrontProfileDto>> Handle(ApproveStorefrontCommand request, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return Result.Failure<StorefrontProfileDto>(Error.Forbidden("La fonctionnalité Rue virtuelle est désactivée."));

        var profile = await _profiles.GetByIdAsync(request.StorefrontProfileId, cancellationToken);
        if (profile is null)
            return Result.Failure<StorefrontProfileDto>(Error.NotFound(nameof(StorefrontProfile), request.StorefrontProfileId));

        var nextIndex = await _profiles.GetNextStreetPositionIndexAsync(cancellationToken);
        // Opération plateforme légitimement inter-tenant (un PlatformAdmin peut approuver la
        // vitrine de n'importe quel tenant) : la garde pertinente n'est pas un rapprochement de
        // tenant mais la machine à états métier (Approve() n'accepte que PendingReview/Suspended,
        // cf. StorefrontProfile.cs) + une trace d'audit de l'acteur plateforme.
        var approve = profile.Approve(nextIndex);
        if (approve.IsFailure)
        {
            _logger.LogWarning(
                "Platform admin {ActorId} failed to approve storefront {ProfileId} (tenant {TenantId}): {Error}",
                _currentUser.UserId, profile.Id, profile.TenantId, approve.Error.Description);
            return Result.Failure<StorefrontProfileDto>(approve.Error);
        }

        await _profiles.UpdateAsync(profile, cancellationToken);

        _logger.LogInformation(
            "Platform admin {ActorId} approved storefront {ProfileId} for tenant {TenantId}.",
            _currentUser.UserId, profile.Id, profile.TenantId);

        return Result.Success(StorefrontProfileMapper.ToDto(profile));
    }
}
