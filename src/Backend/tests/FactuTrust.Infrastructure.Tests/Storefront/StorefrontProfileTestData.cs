using FactuTrust.Domain.Entities.Storefront;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Storefront;

/// <summary>Fabrique partagée de StorefrontProfile pour les tests d'autorisation Approve/Reject.</summary>
internal static class StorefrontProfileTestData
{
    public static StorefrontProfile CreateProfile(StorefrontStatus status)
    {
        var result = StorefrontProfile.Create(
            tenantId: Guid.NewGuid(),
            slug: "acme-widgets",
            displayName: "Acme Widgets",
            publicContactEmail: "contact@acme.example",
            category: StorefrontCategory.Retail,
            facadeTheme: FacadeTheme.Modern,
            consentVersion: "1.0.0",
            consentAcceptedByUserId: Guid.NewGuid());

        Assert.True(result.IsSuccess, result.Error?.Description);
        var profile = result.Value;

        if (status == StorefrontStatus.PendingReview)
        {
            var update = profile.UpdateProfile(
                displayName: "Acme Widgets",
                tagline: null,
                descriptionMarkdown: "Catalogue complet.",
                brandPrimaryColorHex: "#2563EB",
                brandSecondaryColorHex: "#0EA5E9",
                category: StorefrontCategory.Retail,
                facadeTheme: FacadeTheme.Modern,
                publicContactEmail: "contact@acme.example",
                publicContactPhone: null,
                publicContactWhatsApp: null,
                publicLogoUrl: "https://cdn.factutrust.test/acme/logo.webp",
                publicCoverImageUrl: null,
                orderSubmissionEnabled: true);
            Assert.True(update.IsSuccess, update.Error?.Description);

            var submit = profile.SubmitForReview();
            Assert.True(submit.IsSuccess, submit.Error?.Description);
        }

        return profile;
    }
}
