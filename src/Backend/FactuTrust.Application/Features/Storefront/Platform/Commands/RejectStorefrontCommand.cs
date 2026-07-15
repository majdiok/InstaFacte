using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.Storefront;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Storefront.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Storefront;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Storefront.Platform.Commands;

public sealed record RejectStorefrontCommand(Guid StorefrontProfileId, string Reason) : IRequest<Result<StorefrontProfileDto>>;

public sealed class RejectStorefrontCommandHandler : IRequestHandler<RejectStorefrontCommand, Result<StorefrontProfileDto>>
{
    private readonly IStorefrontProfileRepository _profiles;
    private readonly StorefrontOptions _options;

    public RejectStorefrontCommandHandler(
        IStorefrontProfileRepository profiles,
        IOptions<StorefrontOptions> options)
    {
        _profiles = profiles;
        _options = options.Value;
    }

    public async Task<Result<StorefrontProfileDto>> Handle(RejectStorefrontCommand request, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return Result.Failure<StorefrontProfileDto>(Error.Forbidden("La fonctionnalité Rue virtuelle est désactivée."));

        var profile = await _profiles.GetByIdAsync(request.StorefrontProfileId, cancellationToken);
        if (profile is null)
            return Result.Failure<StorefrontProfileDto>(Error.NotFound(nameof(StorefrontProfile), request.StorefrontProfileId));

        var reject = profile.Reject(request.Reason);
        if (reject.IsFailure)
            return Result.Failure<StorefrontProfileDto>(reject.Error);

        await _profiles.UpdateAsync(profile, cancellationToken);

        return Result.Success(StorefrontProfileMapper.ToDto(profile));
    }
}
