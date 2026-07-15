using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Storefront.Public;
using FactuTrust.Domain.Common;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Storefront.Public.Queries;

public sealed record GetStreetMapQuery : IRequest<Result<IReadOnlyList<PublicStreetMapEntryDto>>>;

public sealed class GetStreetMapQueryHandler : IRequestHandler<GetStreetMapQuery, Result<IReadOnlyList<PublicStreetMapEntryDto>>>
{
    private readonly IPublicStorefrontReadRepository _read;
    private readonly StorefrontOptions _options;

    public GetStreetMapQueryHandler(
        IPublicStorefrontReadRepository read,
        IOptions<StorefrontOptions> options)
    {
        _read = read;
        _options = options.Value;
    }

    public async Task<Result<IReadOnlyList<PublicStreetMapEntryDto>>> Handle(
        GetStreetMapQuery request,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return Result.Failure<IReadOnlyList<PublicStreetMapEntryDto>>(Error.Forbidden("La fonctionnalité Rue virtuelle est désactivée."));

        var map = await _read.GetPublishedStreetMapAsync(cancellationToken);
        return Result.Success(map);
    }
}
