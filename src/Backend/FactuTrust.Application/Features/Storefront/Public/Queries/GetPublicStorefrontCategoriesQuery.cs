using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Storefront.Public;
using FactuTrust.Domain.Common;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Storefront.Public.Queries;

public sealed record GetPublicStorefrontCategoriesQuery(string Slug)
    : IRequest<Result<IReadOnlyList<PublicStorefrontCategoryCountDto>>>;

public sealed class GetPublicStorefrontCategoriesQueryHandler
    : IRequestHandler<GetPublicStorefrontCategoriesQuery, Result<IReadOnlyList<PublicStorefrontCategoryCountDto>>>
{
    private readonly IPublicStorefrontReadRepository _read;
    private readonly StorefrontOptions _options;

    public GetPublicStorefrontCategoriesQueryHandler(
        IPublicStorefrontReadRepository read,
        IOptions<StorefrontOptions> options)
    {
        _read = read;
        _options = options.Value;
    }

    public async Task<Result<IReadOnlyList<PublicStorefrontCategoryCountDto>>> Handle(
        GetPublicStorefrontCategoriesQuery request,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return Result.Failure<IReadOnlyList<PublicStorefrontCategoryCountDto>>(Error.Forbidden("La fonctionnalité Rue virtuelle est désactivée."));

        var profile = await _read.GetPublishedDetailBySlugAsync(request.Slug, cancellationToken);
        if (profile is null)
            return Result.Failure<IReadOnlyList<PublicStorefrontCategoryCountDto>>(Error.Validation("Slug", "Vitrine introuvable ou non publiée."));

        var rows = await _read.GetPublishedCategoryCountsAsync(profile.Id, cancellationToken);
        return Result.Success(rows);
    }
}
