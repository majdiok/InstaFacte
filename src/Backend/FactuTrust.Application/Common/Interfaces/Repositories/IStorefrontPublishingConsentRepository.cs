using FactuTrust.Domain.Entities.Storefront;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>
/// Append-only repository for <see cref="StorefrontPublishingConsent"/> audit entries
/// recorded every time a tenant accepts the publication terms.
/// </summary>
public interface IStorefrontPublishingConsentRepository
{
    Task AddAsync(StorefrontPublishingConsent consent, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StorefrontPublishingConsent>> GetByStorefrontAsync(
        Guid storefrontProfileId,
        CancellationToken cancellationToken = default);
}
