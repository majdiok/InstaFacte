using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Storefront;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Storefront.Platform.Queries;

/// <summary>
/// Lot A4 — KPIs agrégés pour la page Vitrines 3D du backoffice.
/// Renvoie les compteurs : Pending / Published / Rejected (Draft + reason) / Suspended.
/// </summary>
public sealed record GetStorefrontStatsQuery : IRequest<Result<StorefrontStatsDto>>;

public sealed class GetStorefrontStatsQueryHandler
    : IRequestHandler<GetStorefrontStatsQuery, Result<StorefrontStatsDto>>
{
    private readonly IStorefrontProfileRepository _profiles;
    private readonly StorefrontOptions _options;

    public GetStorefrontStatsQueryHandler(
        IStorefrontProfileRepository profiles,
        IOptions<StorefrontOptions> options)
    {
        _profiles = profiles;
        _options = options.Value;
    }

    public async Task<Result<StorefrontStatsDto>> Handle(
        GetStorefrontStatsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return Result.Failure<StorefrontStatsDto>(
                Error.Forbidden("La fonctionnalité Rue virtuelle est désactivée."));
        }

        var pendingList = await _profiles.ListByStatusAsync(StorefrontStatus.PendingReview, cancellationToken);
        var publishedCount = await _profiles.CountPublishedAsync(cancellationToken);
        var suspendedList = await _profiles.ListByStatusAsync(StorefrontStatus.Suspended, cancellationToken);
        var draftList = await _profiles.ListByStatusAsync(StorefrontStatus.Draft, cancellationToken);

        var stats = new StorefrontStatsDto
        {
            Pending = pendingList.Count,
            Published = publishedCount,
            Suspended = suspendedList.Count,
            Rejected = draftList.Count(p => !string.IsNullOrWhiteSpace(p.RejectionReason))
        };

        return Result.Success(stats);
    }
}
