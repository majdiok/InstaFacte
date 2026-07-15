using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.Storefront;

/// <summary>
/// Immutable audit trail of a tenant accepting the public publication CGU.
/// Append-only: every acceptance is recorded; the tenant can renew consent
/// on <c>StorefrontProfile</c> but this table keeps the history.
/// </summary>
public sealed class StorefrontPublishingConsent : Entity
{
    public Guid TenantId { get; private set; }
    public Guid StorefrontProfileId { get; private set; }
    public DateTime AcceptedAt { get; private set; }
    public Guid AcceptedByUserId { get; private set; }
    public string TermsVersion { get; private set; } = null!;
    public string? IpAddressHash { get; private set; }
    public string? UserAgent { get; private set; }

    private StorefrontPublishingConsent() { }

    public static Result<StorefrontPublishingConsent> Record(
        Guid tenantId,
        Guid storefrontProfileId,
        Guid acceptedByUserId,
        string termsVersion,
        string? ipAddressHash,
        string? userAgent,
        DateTime? acceptedAt = null)
    {
        if (tenantId == Guid.Empty)
            return Result.Failure<StorefrontPublishingConsent>(Error.Validation("TenantId", "Société obligatoire"));
        if (storefrontProfileId == Guid.Empty)
            return Result.Failure<StorefrontPublishingConsent>(Error.Validation("StorefrontProfileId", "Profil vitrine obligatoire"));
        if (acceptedByUserId == Guid.Empty)
            return Result.Failure<StorefrontPublishingConsent>(Error.Validation("AcceptedByUserId", "Utilisateur obligatoire"));
        if (string.IsNullOrWhiteSpace(termsVersion))
            return Result.Failure<StorefrontPublishingConsent>(Error.Validation("TermsVersion", "Version CGU obligatoire"));

        return Result.Success(new StorefrontPublishingConsent
        {
            TenantId = tenantId,
            StorefrontProfileId = storefrontProfileId,
            AcceptedByUserId = acceptedByUserId,
            TermsVersion = termsVersion.Trim(),
            IpAddressHash = ipAddressHash,
            UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent[..Math.Min(userAgent.Length, 300)],
            AcceptedAt = acceptedAt ?? DateTime.UtcNow
        });
    }
}
