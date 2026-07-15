using FactuTrust.Application.Features.Storefront.DTOs;
using FactuTrust.Domain.Entities.Storefront;

namespace FactuTrust.Application.Features.Storefront;

/// <summary>
/// Maps <see cref="StorefrontProfile"/> aggregates to tenant-admin DTOs.
/// NOTE: This mapper is intentionally separate from the public-facing mapper used by the
/// anonymous street API (phase 4). The tenant-admin DTO exposes workflow fields (status,
/// rejection reason) that must NEVER leak to public visitors.
/// </summary>
internal static class StorefrontProfileMapper
{
    public static StorefrontProfileDto ToDto(StorefrontProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new StorefrontProfileDto
        {
            Id = profile.Id,
            TenantId = profile.TenantId,
            Slug = profile.Slug,
            DisplayName = profile.DisplayName,
            Tagline = profile.Tagline,
            DescriptionMarkdown = profile.DescriptionMarkdown,
            BrandPrimaryColorHex = profile.BrandPrimaryColorHex,
            BrandSecondaryColorHex = profile.BrandSecondaryColorHex,
            PublicLogoUrl = profile.PublicLogoUrl,
            PublicCoverImageUrl = profile.PublicCoverImageUrl,
            Category = profile.Category,
            FacadeTheme = profile.FacadeTheme,
            Status = profile.Status,
            PublicContactEmail = profile.PublicContactEmail,
            PublicContactPhone = profile.PublicContactPhone,
            PublicContactWhatsApp = profile.PublicContactWhatsApp,
            OrderSubmissionEnabled = profile.OrderSubmissionEnabled,
            StreetPositionIndex = profile.StreetPositionIndex,
            PublishedAt = profile.PublishedAt,
            SuspendedAt = profile.SuspendedAt,
            RejectionReason = profile.RejectionReason,
            SuspensionReason = profile.SuspensionReason,
            ConsentVersion = profile.ConsentVersion,
            ConsentAcceptedAt = profile.ConsentAcceptedAt
        };
    }
}
