using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.Storefront;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Builds JSON payloads used by <c>StorefrontOutboxMessage.PayloadJson</c>. These payloads are the
/// public-facing snapshot of a tenant aggregate (product, company, profile) and must therefore be
/// stripped of any private data (NIF, margin, supplier, stock quantity).
/// </summary>
/// <remarks>
/// All serialization goes through this service so that the public allowlist is defined in exactly
/// one place (security sensitive).
/// </remarks>
public interface IStorefrontOutboxPayloadBuilder
{
    /// <summary>Serializes the projection of a tenant product.</summary>
    string BuildProductPayload(
        Guid storefrontProfileId,
        Guid tenantId,
        Product product,
        string? categoryLabel);

    /// <summary>Serializes a "visibility removed" marker for a product.</summary>
    string BuildProductRemovedPayload(Guid storefrontProfileId, Guid tenantId, Guid productId);

    /// <summary>Serializes the projection of the whole profile (identity + branding).</summary>
    string BuildProfilePayload(StorefrontProfile profile);
}
