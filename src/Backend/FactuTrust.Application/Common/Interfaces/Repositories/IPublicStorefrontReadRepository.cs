using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Storefront.Public;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IPublicStorefrontReadRepository
{
    Task<IReadOnlyList<PublicStreetMapEntryDto>> GetPublishedStreetMapAsync(CancellationToken cancellationToken = default);

    Task<PublicStorefrontDetailDto?> GetPublishedDetailBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<PagedResult<PublicStorefrontProductDto>> GetPublishedProductsAsync(
        Guid storefrontProfileId,
        int page,
        int pageSize,
        string? categoryLabel,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PublicStorefrontCategoryCountDto>> GetPublishedCategoryCountsAsync(
        Guid storefrontProfileId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StorefrontProductSnapshot>> GetProductSnapshotsAsync(
        IReadOnlyCollection<Guid> storefrontProductIds,
        CancellationToken cancellationToken = default);
}

/// <summary>Minimal product snapshot for validating a public checkout cart.</summary>
public sealed record StorefrontProductSnapshot(
    Guid StorefrontProductId,
    Guid StorefrontProfileId,
    Guid TenantId,
    Guid SourceProductId,
    string Name,
    decimal UnitPriceAmount,
    string Currency,
    bool IsVisible);
