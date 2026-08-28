using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.Storefront;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Storefront.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Storefront;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Storefront.Platform.Commands;

public sealed record RejectStorefrontCommand(Guid StorefrontProfileId, string Reason) : IRequest<Result<StorefrontProfileDto>>;

public sealed class RejectStorefrontCommandHandler : IRequestHandler<RejectStorefrontCommand, Result<StorefrontProfileDto>>
{
    private readonly IStorefrontProfileRepository _profiles;
    private readonly StorefrontOptions _options;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<RejectStorefrontCommandHandler> _logger;

    public RejectStorefrontCommandHandler(
        IStorefrontProfileRepository profiles,
        IOptions<StorefrontOptions> options,
        ICurrentUser currentUser,
        ILogger<RejectStorefrontCommandHandler> logger)
    {
        _profiles = profiles;
        _options = options.Value;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<StorefrontProfileDto>> Handle(RejectStorefrontCommand request, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return Result.Failure<StorefrontProfileDto>(Error.Forbidden("La fonctionnalité Rue virtuelle est désactivée."));

        var profile = await _profiles.GetByIdAsync(request.StorefrontProfileId, cancellationToken);
        if (profile is null)
            return Result.Failure<StorefrontProfileDto>(Error.NotFound(nameof(StorefrontProfile), request.StorefrontProfileId));

        // Cf. ApproveStorefrontCommand : opération plateforme légitimement inter-tenant, gardée
        // par la machine à états métier (Reject() n'accepte que PendingReview) + trace d'audit.
        var reject = profile.Reject(request.Reason);
        if (reject.IsFailure)
        {
            _logger.LogWarning(
                "Platform admin {ActorId} failed to reject storefront {ProfileId} (tenant {TenantId}): {Error}",
                _currentUser.UserId, profile.Id, profile.TenantId, reject.Error.Description);
            return Result.Failure<StorefrontProfileDto>(reject.Error);
        }

        await _profiles.UpdateAsync(profile, cancellationToken);

        _logger.LogInformation(
            "Platform admin {ActorId} rejected storefront {ProfileId} for tenant {TenantId}: {Reason}",
            _currentUser.UserId, profile.Id, profile.TenantId, request.Reason);

        return Result.Success(StorefrontProfileMapper.ToDto(profile));
    }
}
