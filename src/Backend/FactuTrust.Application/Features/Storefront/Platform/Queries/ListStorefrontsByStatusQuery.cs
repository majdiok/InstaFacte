using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Storefront.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Storefront;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Storefront.Platform.Queries;

/// <summary>
/// Lot A4 — Liste les vitrines par statut pour le backoffice plateforme.
/// Spécificité métier : la valeur logique « rejected » correspond à
/// <see cref="StorefrontStatus.Draft"/> filtrée sur <c>RejectionReason</c> non null
/// (le refus renvoie la vitrine en brouillon avec un motif).
/// </summary>
public sealed record ListStorefrontsByStatusQuery(string Filter)
    : IRequest<Result<IReadOnlyList<StorefrontProfileDto>>>;

public sealed class ListStorefrontsByStatusQueryHandler
    : IRequestHandler<ListStorefrontsByStatusQuery, Result<IReadOnlyList<StorefrontProfileDto>>>
{
    private readonly IStorefrontProfileRepository _profiles;
    private readonly StorefrontOptions _options;

    public ListStorefrontsByStatusQueryHandler(
        IStorefrontProfileRepository profiles,
        IOptions<StorefrontOptions> options)
    {
        _profiles = profiles;
        _options = options.Value;
    }

    public async Task<Result<IReadOnlyList<StorefrontProfileDto>>> Handle(
        ListStorefrontsByStatusQuery request,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return Result.Failure<IReadOnlyList<StorefrontProfileDto>>(
                Error.Forbidden("La fonctionnalité Rue virtuelle est désactivée."));
        }

        var filter = (request.Filter ?? string.Empty).Trim().ToLowerInvariant();

        IEnumerable<StorefrontProfile> source = filter switch
        {
            "pending" or "pendingreview" =>
                await _profiles.ListByStatusAsync(StorefrontStatus.PendingReview, cancellationToken),
            "published" =>
                await _profiles.ListByStatusAsync(StorefrontStatus.Published, cancellationToken),
            "suspended" =>
                await _profiles.ListByStatusAsync(StorefrontStatus.Suspended, cancellationToken),
            "rejected" =>
                (await _profiles.ListByStatusAsync(StorefrontStatus.Draft, cancellationToken))
                .Where(p => !string.IsNullOrWhiteSpace(p.RejectionReason)),
            "draft" =>
                await _profiles.ListByStatusAsync(StorefrontStatus.Draft, cancellationToken),
            _ => Array.Empty<StorefrontProfile>()
        };

        return Result.Success<IReadOnlyList<StorefrontProfileDto>>(
            source.Select(StorefrontProfileMapper.ToDto).ToList());
    }
}
