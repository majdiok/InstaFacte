using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Storefront.Public;
using FactuTrust.Domain.Common;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Storefront.Public.Queries;

public sealed record GetPublicStorefrontBySlugQuery(string Slug) : IRequest<Result<PublicStorefrontDetailDto?>>;

public sealed class GetPublicStorefrontBySlugQueryHandler
    : IRequestHandler<GetPublicStorefrontBySlugQuery, Result<PublicStorefrontDetailDto?>>
{
    private readonly IPublicStorefrontReadRepository _read;
    private readonly StorefrontOptions _options;

    public GetPublicStorefrontBySlugQueryHandler(
        IPublicStorefrontReadRepository read,
        IOptions<StorefrontOptions> options)
    {
        _read = read;
        _options = options.Value;
    }

    public async Task<Result<PublicStorefrontDetailDto?>> Handle(
        GetPublicStorefrontBySlugQuery request,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return Result.Failure<PublicStorefrontDetailDto?>(Error.Forbidden("La fonctionnalité Rue virtuelle est désactivée."));

        var detail = await _read.GetPublishedDetailBySlugAsync(request.Slug, cancellationToken);
        return Result.Success(detail);
    }
}
