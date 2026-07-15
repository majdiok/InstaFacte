using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Storefront;
using FactuTrust.Application.Features.Storefront.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Storefront;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Storefront.Platform.Commands;

public sealed record ApproveStorefrontCommand(Guid StorefrontProfileId) : IRequest<Result<StorefrontProfileDto>>;

public sealed class ApproveStorefrontCommandHandler : IRequestHandler<ApproveStorefrontCommand, Result<StorefrontProfileDto>>
{
    private readonly IStorefrontProfileRepository _profiles;
    private readonly StorefrontOptions _options;

    public ApproveStorefrontCommandHandler(
        IStorefrontProfileRepository profiles,
        IOptions<StorefrontOptions> options)
    {
        _profiles = profiles;
        _options = options.Value;
    }

    public async Task<Result<StorefrontProfileDto>> Handle(ApproveStorefrontCommand request, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return Result.Failure<StorefrontProfileDto>(Error.Forbidden("La fonctionnalité Rue virtuelle est désactivée."));

        var profile = await _profiles.GetByIdAsync(request.StorefrontProfileId, cancellationToken);
        if (profile is null)
            return Result.Failure<StorefrontProfileDto>(Error.NotFound(nameof(StorefrontProfile), request.StorefrontProfileId));

        var nextIndex = await _profiles.GetNextStreetPositionIndexAsync(cancellationToken);
        var approve = profile.Approve(nextIndex);
        if (approve.IsFailure)
            return Result.Failure<StorefrontProfileDto>(approve.Error);

        await _profiles.UpdateAsync(profile, cancellationToken);

        return Result.Success(StorefrontProfileMapper.ToDto(profile));
    }
}
