using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Storefront.Public;
using FactuTrust.Domain.Common;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Storefront.Public.Queries;

public sealed record GetPublicStorefrontProductsQuery(
    string Slug,
    int Page = 1,
    int PageSize = 24,
    string? CategoryLabel = null) : IRequest<Result<PagedResult<PublicStorefrontProductDto>>>;

public sealed class GetPublicStorefrontProductsQueryHandler
    : IRequestHandler<GetPublicStorefrontProductsQuery, Result<PagedResult<PublicStorefrontProductDto>>>
{
    private readonly IPublicStorefrontReadRepository _read;
    private readonly StorefrontOptions _options;

    public GetPublicStorefrontProductsQueryHandler(
        IPublicStorefrontReadRepository read,
        IOptions<StorefrontOptions> options)
    {
        _read = read;
        _options = options.Value;
    }

    public async Task<Result<PagedResult<PublicStorefrontProductDto>>> Handle(
        GetPublicStorefrontProductsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return Result.Failure<PagedResult<PublicStorefrontProductDto>>(Error.Forbidden("La fonctionnalité Rue virtuelle est désactivée."));

        var profile = await _read.GetPublishedDetailBySlugAsync(request.Slug, cancellationToken);
        if (profile is null)
            return Result.Failure<PagedResult<PublicStorefrontProductDto>>(Error.Validation("Slug", "Vitrine introuvable ou non publiée."));

        var page = await _read.GetPublishedProductsAsync(
            profile.Id,
            request.Page,
            request.PageSize,
            request.CategoryLabel,
            cancellationToken);

        return Result.Success(page);
    }
}
