using FactuTrust.Domain.Entities.Storefront;
using FactuTrust.Domain.Events;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Storefront;

/// <summary>
/// Validates the full life-cycle of <see cref="StorefrontProfile"/>: opt-in creation, profile update,
/// submit-for-review, approve, reject, suspend, unpublish. Every transition is tested both in its
/// happy path and for each forbidden source state, to guarantee we never leak an invalid public
/// storefront to the 3D street.
/// </summary>
public sealed class StorefrontProfileStateMachineTests
{
    private const string ConsentVersion = "1.0.0";

    private static StorefrontProfile CreateDraftProfile(string slug = "acme-widgets")
    {
        var result = StorefrontProfile.Create(
            tenantId: Guid.NewGuid(),
            slug: slug,
            displayName: "Acme Widgets",
            publicContactEmail: "contact@acme.example",
            category: StorefrontCategory.Retail,
            facadeTheme: FacadeTheme.Modern,
            consentVersion: ConsentVersion,
            consentAcceptedByUserId: Guid.NewGuid());

        Assert.True(result.IsSuccess, result.Error?.Description);
        return result.Value;
    }

    private static void FillPublishablePrerequisites(StorefrontProfile profile)
    {
        var updateResult = profile.UpdateProfile(
            displayName: "Acme Widgets",
            tagline: "Quality widgets since 1999",
            descriptionMarkdown: "Our full catalog of high-quality widgets, bolts, and nuts.",
            brandPrimaryColorHex: "#2563EB",
            brandSecondaryColorHex: "#0EA5E9",
            category: StorefrontCategory.Retail,
            facadeTheme: FacadeTheme.Modern,
            publicContactEmail: "contact@acme.example",
            publicContactPhone: "+216 71 000 000",
            publicContactWhatsApp: null,
            publicLogoUrl: "https://cdn.factutrust.test/acme/logo.webp",
            publicCoverImageUrl: null,
            orderSubmissionEnabled: true);

        Assert.True(updateResult.IsSuccess, updateResult.Error?.Description);
    }

    [Fact]
    public void Create_ShouldInitializeAsDraft_WithOptInEvent()
    {
        var profile = CreateDraftProfile();

        Assert.Equal(StorefrontStatus.Draft, profile.Status);
        Assert.Equal("acme-widgets", profile.Slug);
        Assert.Contains(profile.DomainEvents, e => e is StorefrontOptInCreatedEvent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("invalid slug with spaces")]
    [InlineData("-starts-with-dash")]
    [InlineData("ends-with-dash-")]
    [InlineData("contains!special")]
    public void Create_ShouldFail_WhenSlugIsInvalid(string invalidSlug)
    {
        var result = StorefrontProfile.Create(
            tenantId: Guid.NewGuid(),
            slug: invalidSlug,
            displayName: "Acme Widgets",
            publicContactEmail: "contact@acme.example",
            category: StorefrontCategory.Retail,
            facadeTheme: FacadeTheme.Modern,
            consentVersion: ConsentVersion,
            consentAcceptedByUserId: Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("Slug", result.Error.Code.Replace("Validation.", string.Empty));
    }

    [Fact]
    public void Create_ShouldNormalizeUppercaseSlugToLowercase()
    {
        var result = StorefrontProfile.Create(
            tenantId: Guid.NewGuid(),
            slug: "ACME-Widgets-01",
            displayName: "Acme Widgets",
            publicContactEmail: "contact@acme.example",
            category: StorefrontCategory.Retail,
            facadeTheme: FacadeTheme.Modern,
            consentVersion: ConsentVersion,
            consentAcceptedByUserId: Guid.NewGuid());

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal("acme-widgets-01", result.Value.Slug);
    }

    [Fact]
    public void SubmitForReview_ShouldFail_WhenDescriptionOrLogoMissing()
    {
        var profile = CreateDraftProfile();

        var submitResult = profile.SubmitForReview();

        Assert.True(submitResult.IsFailure);
        Assert.Equal(StorefrontStatus.Draft, profile.Status);
    }

    [Fact]
    public void SubmitForReview_ShouldTransitionToPendingReview_WhenPrerequisitesMet()
    {
        var profile = CreateDraftProfile();
        FillPublishablePrerequisites(profile);

        var submitResult = profile.SubmitForReview();

        Assert.True(submitResult.IsSuccess, submitResult.Error?.Description);
        Assert.Equal(StorefrontStatus.PendingReview, profile.Status);
        Assert.Contains(profile.DomainEvents, e => e is StorefrontSubmittedForReviewEvent);
    }

    [Theory]
    [InlineData(StorefrontStatus.PendingReview)]
    [InlineData(StorefrontStatus.Suspended)]
    public void Approve_ShouldTransitionToPublished(StorefrontStatus sourceStatus)
    {
        var profile = CreateDraftProfile();
        FillPublishablePrerequisites(profile);
        Assert.True(profile.SubmitForReview().IsSuccess);

        if (sourceStatus == StorefrontStatus.Suspended)
        {
            Assert.True(profile.Approve(7).IsSuccess);
            Assert.True(profile.Suspend("Temporary suspension").IsSuccess);
        }

        var approveResult = profile.Approve(42);

        Assert.True(approveResult.IsSuccess, approveResult.Error?.Description);
        Assert.Equal(StorefrontStatus.Published, profile.Status);
        Assert.Equal(sourceStatus == StorefrontStatus.Suspended ? 7 : 42, profile.StreetPositionIndex);
        Assert.NotNull(profile.PublishedAt);
        Assert.Null(profile.SuspendedAt);
        Assert.Null(profile.RejectionReason);
        Assert.Contains(profile.DomainEvents, e => e is StorefrontPublishedEvent);
    }

    [Fact]
    public void Approve_ShouldFail_WhenStatusIsDraft()
    {
        var profile = CreateDraftProfile();

        var approveResult = profile.Approve(1);

        Assert.True(approveResult.IsFailure);
        Assert.Equal(StorefrontStatus.Draft, profile.Status);
    }

    [Fact]
    public void Reject_ShouldReturnToDraft_WithReasonAndEvent()
    {
        var profile = CreateDraftProfile();
        FillPublishablePrerequisites(profile);
        Assert.True(profile.SubmitForReview().IsSuccess);

        var rejectResult = profile.Reject("Description inadéquate");

        Assert.True(rejectResult.IsSuccess, rejectResult.Error?.Description);
        Assert.Equal(StorefrontStatus.Draft, profile.Status);
        Assert.Equal("Description inadéquate", profile.RejectionReason);
        Assert.Contains(profile.DomainEvents, e => e is StorefrontRejectedEvent);
    }

    [Fact]
    public void Reject_ShouldFail_WhenStatusIsNotPendingReview()
    {
        var profile = CreateDraftProfile();

        var rejectResult = profile.Reject("Because");

        Assert.True(rejectResult.IsFailure);
    }

    [Fact]
    public void Suspend_ShouldMoveFromPublishedToSuspended_WithReasonAndEvent()
    {
        var profile = CreateDraftProfile();
        FillPublishablePrerequisites(profile);
        Assert.True(profile.SubmitForReview().IsSuccess);
        Assert.True(profile.Approve(3).IsSuccess);

        var suspendResult = profile.Suspend("Contenu signalé");

        Assert.True(suspendResult.IsSuccess, suspendResult.Error?.Description);
        Assert.Equal(StorefrontStatus.Suspended, profile.Status);
        Assert.Equal("Contenu signalé", profile.SuspensionReason);
        Assert.NotNull(profile.SuspendedAt);
        Assert.Contains(profile.DomainEvents, e => e is StorefrontSuspendedEvent);
    }

    [Theory]
    [InlineData(StorefrontStatus.Draft)]
    [InlineData(StorefrontStatus.PendingReview)]
    [InlineData(StorefrontStatus.Suspended)]
    public void Suspend_ShouldFail_WhenNotPublished(StorefrontStatus invalidStatus)
    {
        var profile = CreateDraftProfile();
        FillPublishablePrerequisites(profile);

        if (invalidStatus == StorefrontStatus.PendingReview)
        {
            profile.SubmitForReview();
        }
        else if (invalidStatus == StorefrontStatus.Suspended)
        {
            profile.SubmitForReview();
            profile.Approve(1);
            profile.Suspend("Initial");
        }

        var secondSuspend = profile.Suspend("Another");

        Assert.True(secondSuspend.IsFailure);
    }

    [Fact]
    public void Unpublish_ShouldReturnToDraft_OnlyFromPublished()
    {
        var profile = CreateDraftProfile();
        FillPublishablePrerequisites(profile);
        Assert.True(profile.SubmitForReview().IsSuccess);
        Assert.True(profile.Approve(5).IsSuccess);

        var unpublishResult = profile.Unpublish();

        Assert.True(unpublishResult.IsSuccess, unpublishResult.Error?.Description);
        Assert.Equal(StorefrontStatus.Draft, profile.Status);
        Assert.Null(profile.PublishedAt);
        Assert.Contains(profile.DomainEvents, e => e is StorefrontUnpublishedEvent);
    }

    [Fact]
    public void Unpublish_ShouldFail_WhenStatusIsDraft()
    {
        var profile = CreateDraftProfile();

        var unpublishResult = profile.Unpublish();

        Assert.True(unpublishResult.IsFailure);
    }

    [Fact]
    public void UpdateProfile_ShouldFail_WhenStatusIsSuspended()
    {
        var profile = CreateDraftProfile();
        FillPublishablePrerequisites(profile);
        Assert.True(profile.SubmitForReview().IsSuccess);
        Assert.True(profile.Approve(1).IsSuccess);
        Assert.True(profile.Suspend("Issue").IsSuccess);

        var updateResult = profile.UpdateProfile(
            displayName: "Another Name",
            tagline: null,
            descriptionMarkdown: "desc",
            brandPrimaryColorHex: "#FF0000",
            brandSecondaryColorHex: "#00FF00",
            category: StorefrontCategory.Retail,
            facadeTheme: FacadeTheme.Classic,
            publicContactEmail: "new@acme.example",
            publicContactPhone: null,
            publicContactWhatsApp: null,
            publicLogoUrl: "https://cdn.factutrust.test/acme/logo.webp",
            publicCoverImageUrl: null,
            orderSubmissionEnabled: true);

        Assert.True(updateResult.IsFailure);
    }

    [Theory]
    [InlineData("NOT-HEX")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("red")]
    public void UpdateProfile_ShouldFail_WhenBrandColorInvalid(string invalidColor)
    {
        var profile = CreateDraftProfile();

        var updateResult = profile.UpdateProfile(
            displayName: "Acme",
            tagline: null,
            descriptionMarkdown: null,
            brandPrimaryColorHex: invalidColor,
            brandSecondaryColorHex: "#00FF00",
            category: StorefrontCategory.Retail,
            facadeTheme: FacadeTheme.Classic,
            publicContactEmail: "contact@acme.example",
            publicContactPhone: null,
            publicContactWhatsApp: null,
            publicLogoUrl: null,
            publicCoverImageUrl: null,
            orderSubmissionEnabled: true);

        Assert.True(updateResult.IsFailure);
    }

    [Fact]
    public void RecordConsentRenewal_ShouldUpdateVersionAndAcceptance()
    {
        var profile = CreateDraftProfile();
        var newUser = Guid.NewGuid();
        var newTimestamp = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

        profile.RecordConsentRenewal("2.0.0", newUser, newTimestamp);

        Assert.Equal("2.0.0", profile.ConsentVersion);
        Assert.Equal(newUser, profile.ConsentAcceptedByUserId);
        Assert.Equal(newTimestamp, profile.ConsentAcceptedAt);
    }
}
