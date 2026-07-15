using FactuTrust.Domain.Entities.Storefront;

namespace FactuTrust.Application.Features.Storefront.DTOs;

/// <summary>
/// Full DTO representing the tenant's own storefront profile, including workflow fields
/// (<see cref="Status"/>, <see cref="RejectionReason"/>, etc.). Returned only to authenticated tenant admins.
/// </summary>
public sealed record StorefrontProfileDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string Slug { get; init; } = null!;
    public string DisplayName { get; init; } = null!;
    public string? Tagline { get; init; }
    public string? DescriptionMarkdown { get; init; }
    public string BrandPrimaryColorHex { get; init; } = "#2563eb";
    public string BrandSecondaryColorHex { get; init; } = "#0ea5e9";
    public string? PublicLogoUrl { get; init; }
    public string? PublicCoverImageUrl { get; init; }
    public StorefrontCategory Category { get; init; }
    public FacadeTheme FacadeTheme { get; init; }
    public StorefrontStatus Status { get; init; }
    public string PublicContactEmail { get; init; } = null!;
    public string? PublicContactPhone { get; init; }
    public string? PublicContactWhatsApp { get; init; }
    public bool OrderSubmissionEnabled { get; init; }
    public int? StreetPositionIndex { get; init; }
    public DateTime? PublishedAt { get; init; }
    public DateTime? SuspendedAt { get; init; }
    public string? RejectionReason { get; init; }
    public string? SuspensionReason { get; init; }
    public string ConsentVersion { get; init; } = null!;
    public DateTime ConsentAcceptedAt { get; init; }
}

/// <summary>
/// Payload used when the tenant admin opts-in to the public storefront programme.
/// The consent fields are captured server-side from the authenticated user.
/// </summary>
public sealed record OptInStorefrontRequest
{
    public string Slug { get; init; } = null!;
    public string DisplayName { get; init; } = null!;
    public string PublicContactEmail { get; init; } = null!;
    public StorefrontCategory Category { get; init; }
    public FacadeTheme FacadeTheme { get; init; }

    /// <summary>
    /// Version of the publication terms accepted by the tenant admin (must match the current server-side version).
    /// </summary>
    public string AcceptedTermsVersion { get; init; } = null!;
}

/// <summary>
/// Payload for updating the tenant's storefront profile.
/// </summary>
public sealed record UpdateStorefrontProfileRequest
{
    public string DisplayName { get; init; } = null!;
    public string? Tagline { get; init; }
    public string? DescriptionMarkdown { get; init; }
    public string BrandPrimaryColorHex { get; init; } = "#2563eb";
    public string BrandSecondaryColorHex { get; init; } = "#0ea5e9";
    public StorefrontCategory Category { get; init; }
    public FacadeTheme FacadeTheme { get; init; }
    public string PublicContactEmail { get; init; } = null!;
    public string? PublicContactPhone { get; init; }
    public string? PublicContactWhatsApp { get; init; }
    public string? PublicLogoUrl { get; init; }
    public string? PublicCoverImageUrl { get; init; }
    public bool OrderSubmissionEnabled { get; init; } = true;
}

/// <summary>
/// DTO representing a tenant product listed for public visibility toggling.
/// Exposes only the fields necessary for the "products" tab of the storefront admin.
/// </summary>
public sealed record StorefrontEligibleProductDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public decimal UnitPrice { get; init; }
    public string Currency { get; init; } = "TND";
    public string? CategoryLabel { get; init; }
    public string? ImageUrl { get; init; }
    public bool IsActive { get; init; }
    public bool IsPubliclyListed { get; init; }
}
