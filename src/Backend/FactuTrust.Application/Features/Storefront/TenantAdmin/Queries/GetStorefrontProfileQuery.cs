using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Storefront.DTOs;
using FactuTrust.Domain.Common;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Storefront.TenantAdmin.Queries;

/// <summary>
/// Returns the tenant's own storefront profile, or <c>null</c> when the tenant has not yet
/// opted in. Used by the <c>factutrust-web</c> storefront-admin feature to render the activation
/// screen vs the management screen.
/// </summary>
public sealed record GetStorefrontProfileQuery() : IRequest<Result<StorefrontProfileDto?>>;

public sealed class GetStorefrontProfileQueryHandler
    : IRequestHandler<GetStorefrontProfileQuery, Result<StorefrontProfileDto?>>
{
    private readonly IStorefrontProfileRepository _profileRepository;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly StorefrontOptions _options;

    public GetStorefrontProfileQueryHandler(
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

    public async Task<Result<StorefrontProfileDto?>> Handle(
        GetStorefrontProfileQuery request,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return Result.Failure<StorefrontProfileDto?>(Error.Forbidden("La fonctionnalité Rue virtuelle est désactivée."));

        var tenantId = _tenantContext.TenantId ?? _currentUser.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            return Result.Failure<StorefrontProfileDto?>(Error.Unauthorized("Aucun contexte d'entreprise disponible."));

        var profile = await _profileRepository.GetByTenantIdAsync(tenantId.Value, cancellationToken);
        return Result.Success(profile is null ? null : StorefrontProfileMapper.ToDto(profile));
    }
}
