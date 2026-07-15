using FactuTrust.Domain.Entities.Storefront;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>
/// Read/write access to <see cref="StorefrontProfile"/> aggregates, which are persisted
/// in the Master database (not in any tenant database).
/// </summary>
/// <remarks>
/// Uniqueness constraints enforced by the database:
/// <list type="bullet">
///   <item><c>TenantId</c> is unique (one storefront per tenant).</item>
///   <item><c>Slug</c> is unique across the whole Master database (global namespace).</item>
/// </list>
/// </remarks>
public interface IStorefrontProfileRepository
{
    Task<StorefrontProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<StorefrontProfile?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<StorefrontProfile?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>Returns <c>true</c> when another storefront already uses the given slug.</summary>
    Task<bool> SlugExistsAsync(string slug, Guid? excludingStorefrontId = null, CancellationToken cancellationToken = default);

    /// <summary>Atomically persists a new profile and its first publication consent (same SQL transaction).</summary>
    Task AddOptInAsync(
        StorefrontProfile profile,
        StorefrontPublishingConsent initialConsent,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(StorefrontProfile profile, CancellationToken cancellationToken = default);

    /// <summary>Returns the number of storefronts currently in <see cref="StorefrontStatus.Published"/> state.</summary>
    Task<int> CountPublishedAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StorefrontProfile>> ListByStatusAsync(
        StorefrontStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>Next slot index on the virtual street (max of published positions + 1).</summary>
    Task<int> GetNextStreetPositionIndexAsync(CancellationToken cancellationToken = default);
}
