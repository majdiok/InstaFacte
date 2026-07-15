using System.Linq;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Storefront.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Storefront;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Storefront.Platform.Queries;

public sealed record ListPendingStorefrontsQuery : IRequest<Result<IReadOnlyList<StorefrontProfileDto>>>;

public sealed class ListPendingStorefrontsQueryHandler
    : IRequestHandler<ListPendingStorefrontsQuery, Result<IReadOnlyList<StorefrontProfileDto>>>
{
    private readonly IStorefrontProfileRepository _profiles;
    private readonly StorefrontOptions _options;

    public ListPendingStorefrontsQueryHandler(
        IStorefrontProfileRepository profiles,
        IOptions<StorefrontOptions> options)
    {
        _profiles = profiles;
        _options = options.Value;
    }

    public async Task<Result<IReadOnlyList<StorefrontProfileDto>>> Handle(
        ListPendingStorefrontsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return Result.Failure<IReadOnlyList<StorefrontProfileDto>>(Error.Forbidden("La fonctionnalité Rue virtuelle est désactivée."));

        var list = await _profiles.ListByStatusAsync(StorefrontStatus.PendingReview, cancellationToken);
        return Result.Success<IReadOnlyList<StorefrontProfileDto>>(
            list.Select(StorefrontProfileMapper.ToDto).ToList());
    }
}
