using System.Text.RegularExpressions;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Events;

namespace FactuTrust.Domain.Entities.Storefront;

/// <summary>
/// Public storefront configuration, persisted in the Master database.
/// Represents a tenant's opt-in to expose a public 3D shop on the virtual street.
/// Strict allowlist of public fields: no NIF, no TaxRegime, no margin data.
/// </summary>
public sealed partial class StorefrontProfile : AggregateRoot
{
    public const int DescriptionMaxLength = 4000;
    public const int TaglineMaxLength = 120;
    public const int DisplayNameMaxLength = 120;
    public const int RejectionReasonMaxLength = 500;
    private const string SlugPattern = "^[a-z0-9]([a-z0-9-]{1,58}[a-z0-9])?$";
    private const string HexColorPattern = "^#[0-9A-Fa-f]{6}$";

    public Guid TenantId { get; private set; }
    public string Slug { get; private set; } = null!;

    public string DisplayName { get; private set; } = null!;
    public string? Tagline { get; private set; }
    public string? DescriptionMarkdown { get; private set; }

    public string BrandPrimaryColorHex { get; private set; } = "#2563eb";
    public string BrandSecondaryColorHex { get; private set; } = "#0ea5e9";

    public string? PublicLogoUrl { get; private set; }
    public string? PublicCoverImageUrl { get; private set; }

    public StorefrontCategory Category { get; private set; }
    public StorefrontStatus Status { get; private set; }
    public FacadeTheme FacadeTheme { get; private set; }

    public string PublicContactEmail { get; private set; } = null!;
    public string? PublicContactPhone { get; private set; }
    public string? PublicContactWhatsApp { get; private set; }

    /// <summary>
    /// Deterministic index assigned at publish time. Determines the slot on the 3D street.
    /// Unique among Published storefronts.
    /// </summary>
    public int? StreetPositionIndex { get; private set; }

    /// <summary>Allows the tenant to temporarily disable checkout without unpublishing.</summary>
    public bool OrderSubmissionEnabled { get; private set; } = true;

    public DateTime? PublishedAt { get; private set; }
    public DateTime? SuspendedAt { get; private set; }
    public string? RejectionReason { get; private set; }
    public string? SuspensionReason { get; private set; }

    public string ConsentVersion { get; private set; } = null!;
    public DateTime ConsentAcceptedAt { get; private set; }
    public Guid ConsentAcceptedByUserId { get; private set; }

    private StorefrontProfile() { }

    public static Result<StorefrontProfile> Create(
        Guid tenantId,
        string slug,
        string displayName,
        string publicContactEmail,
        StorefrontCategory category,
        FacadeTheme facadeTheme,
        string consentVersion,
        Guid consentAcceptedByUserId,
        DateTime? consentAcceptedAt = null)
    {
        if (tenantId == Guid.Empty)
            return Result.Failure<StorefrontProfile>(Error.Validation("TenantId", "L'identifiant de la société est obligatoire"));

        var slugValidation = ValidateSlug(slug);
        if (slugValidation.IsFailure)
            return Result.Failure<StorefrontProfile>(slugValidation.Error);

        if (string.IsNullOrWhiteSpace(displayName))
            return Result.Failure<StorefrontProfile>(Error.Validation("DisplayName", "Le nom public de la vitrine est obligatoire"));

        if (displayName.Length > DisplayNameMaxLength)
            return Result.Failure<StorefrontProfile>(Error.Validation("DisplayName", $"Le nom public ne peut pas dépasser {DisplayNameMaxLength} caractères"));

        if (string.IsNullOrWhiteSpace(publicContactEmail))
            return Result.Failure<StorefrontProfile>(Error.Validation("PublicContactEmail", "Un email public de contact est obligatoire"));

        if (string.IsNullOrWhiteSpace(consentVersion))
            return Result.Failure<StorefrontProfile>(Error.Validation("ConsentVersion", "La version des CGU est obligatoire"));

        if (consentAcceptedByUserId == Guid.Empty)
            return Result.Failure<StorefrontProfile>(Error.Validation("ConsentAcceptedByUserId", "L'utilisateur acceptant les CGU est obligatoire"));

        var profile = new StorefrontProfile
        {
            TenantId = tenantId,
            Slug = slug.Trim().ToLowerInvariant(),
            DisplayName = displayName.Trim(),
            PublicContactEmail = publicContactEmail.Trim().ToLowerInvariant(),
            Category = category,
            FacadeTheme = facadeTheme,
            Status = StorefrontStatus.Draft,
            ConsentVersion = consentVersion.Trim(),
            ConsentAcceptedByUserId = consentAcceptedByUserId,
            ConsentAcceptedAt = consentAcceptedAt ?? DateTime.UtcNow
        };

        profile.AddDomainEvent(new StorefrontOptInCreatedEvent(profile.Id, profile.TenantId, profile.Slug));
        return Result.Success(profile);
    }

    public Result UpdateProfile(
        string displayName,
        string? tagline,
        string? descriptionMarkdown,
        string brandPrimaryColorHex,
        string brandSecondaryColorHex,
        StorefrontCategory category,
        FacadeTheme facadeTheme,
        string publicContactEmail,
        string? publicContactPhone,
        string? publicContactWhatsApp,
        string? publicLogoUrl,
        string? publicCoverImageUrl,
        bool orderSubmissionEnabled)
    {
        if (Status == StorefrontStatus.Suspended)
            return Result.Failure(Error.Validation("Status", "Une vitrine suspendue ne peut pas être modifiée. Contactez le support."));

        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > DisplayNameMaxLength)
            return Result.Failure(Error.Validation("DisplayName", $"Le nom public est obligatoire et limité à {DisplayNameMaxLength} caractères"));

        if (!string.IsNullOrWhiteSpace(tagline) && tagline.Length > TaglineMaxLength)
            return Result.Failure(Error.Validation("Tagline", $"Le slogan est limité à {TaglineMaxLength} caractères"));

        if (!string.IsNullOrWhiteSpace(descriptionMarkdown) && descriptionMarkdown.Length > DescriptionMaxLength)
            return Result.Failure(Error.Validation("Description", $"La description est limitée à {DescriptionMaxLength} caractères"));

        if (!HexColorRegex().IsMatch(brandPrimaryColorHex))
            return Result.Failure(Error.Validation("BrandPrimaryColorHex", "La couleur primaire doit être au format hexadécimal #RRGGBB"));

        if (!HexColorRegex().IsMatch(brandSecondaryColorHex))
            return Result.Failure(Error.Validation("BrandSecondaryColorHex", "La couleur secondaire doit être au format hexadécimal #RRGGBB"));

        if (string.IsNullOrWhiteSpace(publicContactEmail))
            return Result.Failure(Error.Validation("PublicContactEmail", "Un email public de contact est obligatoire"));

        DisplayName = displayName.Trim();
        Tagline = string.IsNullOrWhiteSpace(tagline) ? null : tagline.Trim();
        DescriptionMarkdown = string.IsNullOrWhiteSpace(descriptionMarkdown) ? null : descriptionMarkdown.Trim();
        BrandPrimaryColorHex = brandPrimaryColorHex.ToUpperInvariant();
        BrandSecondaryColorHex = brandSecondaryColorHex.ToUpperInvariant();
        Category = category;
        FacadeTheme = facadeTheme;
        PublicContactEmail = publicContactEmail.Trim().ToLowerInvariant();
        PublicContactPhone = string.IsNullOrWhiteSpace(publicContactPhone) ? null : publicContactPhone.Trim();
        PublicContactWhatsApp = string.IsNullOrWhiteSpace(publicContactWhatsApp) ? null : publicContactWhatsApp.Trim();
        PublicLogoUrl = string.IsNullOrWhiteSpace(publicLogoUrl) ? null : publicLogoUrl.Trim();
        PublicCoverImageUrl = string.IsNullOrWhiteSpace(publicCoverImageUrl) ? null : publicCoverImageUrl.Trim();
        OrderSubmissionEnabled = orderSubmissionEnabled;

        AddDomainEvent(new StorefrontProfileUpdatedEvent(Id, TenantId));
        return Result.Success();
    }

    public Result SubmitForReview()
    {
        if (Status != StorefrontStatus.Draft)
            return Result.Failure(Error.Validation("Status", "Seule une vitrine en brouillon peut être soumise à validation"));

        if (string.IsNullOrWhiteSpace(DescriptionMarkdown))
            return Result.Failure(Error.Validation("Description", "Une description est obligatoire avant soumission"));

        if (string.IsNullOrWhiteSpace(PublicLogoUrl))
            return Result.Failure(Error.Validation("PublicLogoUrl", "Un logo public est obligatoire avant soumission"));

        Status = StorefrontStatus.PendingReview;
        RejectionReason = null;
        AddDomainEvent(new StorefrontSubmittedForReviewEvent(Id, TenantId));
        return Result.Success();
    }

    public Result Approve(int streetPositionIndex)
    {
        if (Status != StorefrontStatus.PendingReview && Status != StorefrontStatus.Suspended)
            return Result.Failure(Error.Validation("Status", "Seule une vitrine en attente ou suspendue peut être publiée"));

        if (streetPositionIndex < 0)
            return Result.Failure(Error.Validation("StreetPositionIndex", "La position dans la rue doit être positive"));

        Status = StorefrontStatus.Published;
        if (StreetPositionIndex is null)
            StreetPositionIndex = streetPositionIndex;
        PublishedAt = DateTime.UtcNow;
        SuspendedAt = null;
        SuspensionReason = null;
        RejectionReason = null;
        AddDomainEvent(new StorefrontPublishedEvent(Id, TenantId, Slug));
        return Result.Success();
    }

    public Result Reject(string reason)
    {
        if (Status != StorefrontStatus.PendingReview)
            return Result.Failure(Error.Validation("Status", "Seule une vitrine en attente peut être rejetée"));

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure(Error.Validation("Reason", "Un motif de rejet est obligatoire"));

        if (reason.Length > RejectionReasonMaxLength)
            return Result.Failure(Error.Validation("Reason", $"Le motif est limité à {RejectionReasonMaxLength} caractères"));

        Status = StorefrontStatus.Draft;
        RejectionReason = reason.Trim();
        AddDomainEvent(new StorefrontRejectedEvent(Id, TenantId, RejectionReason));
        return Result.Success();
    }

    public Result Suspend(string reason)
    {
        if (Status != StorefrontStatus.Published)
            return Result.Failure(Error.Validation("Status", "Seule une vitrine publiée peut être suspendue"));

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure(Error.Validation("Reason", "Un motif de suspension est obligatoire"));

        if (reason.Length > RejectionReasonMaxLength)
            return Result.Failure(Error.Validation("Reason", $"Le motif est limité à {RejectionReasonMaxLength} caractères"));

        Status = StorefrontStatus.Suspended;
        SuspendedAt = DateTime.UtcNow;
        SuspensionReason = reason.Trim();
        AddDomainEvent(new StorefrontSuspendedEvent(Id, TenantId, SuspensionReason));
        return Result.Success();
    }

    public Result Unpublish()
    {
        if (Status != StorefrontStatus.Published)
            return Result.Failure(Error.Validation("Status", "Seule une vitrine publiée peut être dépubliée"));

        Status = StorefrontStatus.Draft;
        PublishedAt = null;
        AddDomainEvent(new StorefrontUnpublishedEvent(Id, TenantId));
        return Result.Success();
    }

    /// <summary>Renews the recorded consent (e.g. when the user accepts a new CGU version).</summary>
    public void RecordConsentRenewal(string newConsentVersion, Guid userId, DateTime? acceptedAt = null)
    {
        if (string.IsNullOrWhiteSpace(newConsentVersion))
            throw new ArgumentException("La version des CGU est obligatoire", nameof(newConsentVersion));
        if (userId == Guid.Empty)
            throw new ArgumentException("L'utilisateur est obligatoire", nameof(userId));

        ConsentVersion = newConsentVersion.Trim();
        ConsentAcceptedAt = acceptedAt ?? DateTime.UtcNow;
        ConsentAcceptedByUserId = userId;
    }

    public static Result ValidateSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return Result.Failure(Error.Validation("Slug", "Le slug public est obligatoire"));

        var normalized = slug.Trim().ToLowerInvariant();
        if (!SlugRegex().IsMatch(normalized))
            return Result.Failure(Error.Validation("Slug", "Le slug doit contenir 3 à 60 caractères alphanumériques, minuscules, avec tirets intercalaires"));

        return Result.Success();
    }

    [GeneratedRegex(SlugPattern, RegexOptions.Compiled)]
    private static partial Regex SlugRegex();

    [GeneratedRegex(HexColorPattern, RegexOptions.Compiled)]
    private static partial Regex HexColorRegex();
}
