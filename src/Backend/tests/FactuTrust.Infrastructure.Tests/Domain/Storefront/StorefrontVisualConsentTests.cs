using FactuTrust.Domain.Entities.Storefront;
using FactuTrust.Infrastructure.Tests.Storefront;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Storefront;

public sealed class StorefrontVisualConsentTests
{
    // Consent is deliberately unattached. Profile snapshots below verify the legacy fixture
    // stays independent; they do not certify an integrated persistence/authorization workflow.
    private const string ProfileKey = "vp-c12";
    private const string Revision = "r1";
    private const string TermsVersion = "visual-1.0";
    private static readonly Guid OwnerId = Guid.Parse("f9d7e97a-734f-44ce-bc04-3a027c12bf8d");
    private static readonly DateTime AcceptedAt = new(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    public void Accept_ShouldRejectUnknownStorefrontStatus(int status)
    {
        var consent = new StorefrontVisualConsent();

        var result = consent.AcceptVisualConsent((StorefrontStatus)status, ProfileKey, Revision, TermsVersion, true, OwnerId, AcceptedAt);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Status", result.Error.Code);
        AssertAbsent(consent);
    }

    [Theory]
    [InlineData(StorefrontStatus.Draft)]
    [InlineData(StorefrontStatus.PendingReview)]
    [InlineData(StorefrontStatus.Published)]
    [InlineData(StorefrontStatus.Suspended)]
    public void OldProfileFixtureAndStandaloneConsent_ShouldRequireNoNewInputs(StorefrontStatus status)
    {
        var profile = CreateProfile(status);
        var consent = new StorefrontVisualConsent();

        AssertAbsent(consent);
        Assert.Equal("1.0.0", profile.ConsentVersion);
        Assert.NotEqual(Guid.Empty, profile.ConsentAcceptedByUserId);
        Assert.Equal(status, profile.Status);
    }

    [Theory]
    [InlineData(StorefrontStatus.Draft)]
    [InlineData(StorefrontStatus.PendingReview)]
    [InlineData(StorefrontStatus.Published)]
    public void Accept_ShouldSetCompleteConsent_WithoutChangingOtherState(StorefrontStatus status)
    {
        var profile = CreateProfile(status);
        var consent = new StorefrontVisualConsent();
        var before = NonVisualState(profile);

        var result = consent.AcceptVisualConsent(profile.Status, ProfileKey, Revision, TermsVersion, true, OwnerId, AcceptedAt);

        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.True(result.Value);
        AssertAccepted(consent);
        Assert.Equal(before, NonVisualState(profile));
        Assert.Empty(profile.DomainEvents); // The standalone state does not emit persisted-profile events.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Accept_ShouldRequireExplicitAcceptance_WithoutMutation(bool hasConsent)
    {
        var profile = CreateProfile(StorefrontStatus.Published);
        var consent = new StorefrontVisualConsent();
        if (hasConsent) Accept(consent, profile.Status);
        var before = VisualState(consent);

        var result = consent.AcceptVisualConsent(profile.Status, ProfileKey, Revision, TermsVersion, false, OwnerId, AcceptedAt);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.VisualConsent", result.Error.Code);
        Assert.Equal(before, VisualState(consent));
    }

    public static IEnumerable<object?[]> InvalidReferences()
    {
        foreach (var key in new[] { null, "", " ", "VP-C12", "vp_c12", "-vp-c12", "vp-c12-", "vp--c12", "vp/c12", "https://example.test", "vp-c12\n", new string('a', 65) })
            yield return new object?[] { key, Revision, TermsVersion, "PublicVisualProfileKey" };
        foreach (var revision in new[] { null, "", " ", "r0", "r01", "R1", "1", "r1.0", "r-1", "r1\n", "r" + new string('1', 64) })
            yield return new object?[] { ProfileKey, revision, TermsVersion, "PublicVisualProfileRevision" };
        foreach (var version in new[] { null, "", " ", "visual\n1", "visual\0v1", new string('a', 65) })
            yield return new object?[] { ProfileKey, Revision, version, "VisualConsentVersion" };
    }

    [Theory]
    [MemberData(nameof(InvalidReferences))]
    public void Accept_ShouldRejectInvalidReferenceAtomically(string? key, string? revision, string? version, string field)
    {
        // A rejected first acceptance stays all-null; a rejected replacement keeps all old fields.
        foreach (var hasConsent in new[] { false, true })
        {
            var profile = CreateProfile(StorefrontStatus.Published);
            var consent = new StorefrontVisualConsent();
            if (hasConsent) Accept(consent, profile.Status);
            var before = VisualState(consent);
            var otherState = NonVisualState(profile);

            var result = consent.AcceptVisualConsent(profile.Status, key!, revision!, version!, true, OwnerId, AcceptedAt.AddDays(1));

            Assert.True(result.IsFailure);
            Assert.Equal($"Validation.{field}", result.Error.Code);
            Assert.Equal(before, VisualState(consent));
            Assert.Equal(otherState, NonVisualState(profile));
        }
    }

    [Fact]
    public void Accept_ShouldAcceptBoundaries_AndTrimTermsVersionOnly()
    {
        var profile = CreateProfile(StorefrontStatus.Draft);
        var consent = new StorefrontVisualConsent();
        var key = new string('a', StorefrontVisualConsent.PublicVisualProfileKeyMaxLength);
        var revision = "r" + new string('9', StorefrontVisualConsent.PublicVisualProfileRevisionMaxLength - 1);
        var version = new string('v', StorefrontVisualConsent.VisualConsentVersionMaxLength - 2);

        var result = consent.AcceptVisualConsent(profile.Status, key, revision, $" {version} ", true, OwnerId, AcceptedAt);

        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.Equal(key, consent.PublicVisualProfileKey);
        Assert.Equal(revision, consent.PublicVisualProfileRevision);
        Assert.Equal(version, consent.VisualConsentVersion);
        Assert.Equal(AcceptedAt, consent.VisualConsentAcceptedAt);
        Assert.Equal(OwnerId, consent.VisualConsentAcceptedByUserId);
    }

    [Fact]
    public void Accept_ShouldRejectEmptyActor_WithoutReplacingProof()
    {
        var profile = CreateProfile(StorefrontStatus.Published);
        var consent = new StorefrontVisualConsent();
        Accept(consent, profile.Status);
        var before = VisualState(consent);

        var result = consent.AcceptVisualConsent(profile.Status, ProfileKey, "r2", TermsVersion, true, Guid.Empty, AcceptedAt);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.VisualConsentAcceptedByUserId", result.Error.Code);
        Assert.Equal(before, VisualState(consent));
    }

    public static IEnumerable<object[]> InvalidDates()
    {
        yield return new object[] { default(DateTime) };
        yield return new object[] { DateTime.SpecifyKind(AcceptedAt, DateTimeKind.Unspecified) };
        yield return new object[] { DateTime.SpecifyKind(AcceptedAt, DateTimeKind.Local) };
    }

    [Theory]
    [MemberData(nameof(InvalidDates))]
    public void Accept_ShouldRejectMissingOrNonUtcDate_WithoutReplacingProof(DateTime timestamp)
    {
        var profile = CreateProfile(StorefrontStatus.Published);
        var consent = new StorefrontVisualConsent();
        Accept(consent, profile.Status);
        var before = VisualState(consent);

        var result = consent.AcceptVisualConsent(profile.Status, ProfileKey, "r2", TermsVersion, true, OwnerId, timestamp);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.VisualConsentAcceptedAt", result.Error.Code);
        Assert.Equal(before, VisualState(consent));
    }

    [Fact]
    public void AcceptSameIdentityAndTerms_ShouldBeNoOp_AndPreserveOriginalActorAndDate()
    {
        var profile = CreateProfile(StorefrontStatus.Published);
        var consent = new StorefrontVisualConsent();
        Accept(consent, profile.Status);

        var result = consent.AcceptVisualConsent(profile.Status, ProfileKey, Revision, $" {TermsVersion} ", true, Guid.NewGuid(), AcceptedAt.AddDays(1));

        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.False(result.Value);
        AssertAccepted(consent);
        Assert.Empty(profile.DomainEvents);
    }

    [Theory]
    [InlineData("vp-c17", Revision, TermsVersion)]
    [InlineData(ProfileKey, "r2", TermsVersion)]
    [InlineData(ProfileKey, Revision, "visual-2.0")]
    public void AcceptDifferentIdentityOrTerms_ShouldReplaceCompleteProof(string key, string revision, string version)
    {
        var profile = CreateProfile(StorefrontStatus.Published);
        var consent = new StorefrontVisualConsent();
        Accept(consent, profile.Status);
        var owner = Guid.NewGuid();
        var timestamp = AcceptedAt.AddDays(1);
        var before = NonVisualState(profile);

        var result = consent.AcceptVisualConsent(profile.Status, key, revision, version, true, owner, timestamp);

        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.True(result.Value);
        Assert.Equal(new object?[] { key, revision, version, timestamp, owner }, VisualState(consent));
        Assert.Equal(before, NonVisualState(profile));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AcceptWhileSuspended_ShouldFail_EvenForAnOtherwiseIdempotentRepeat(bool hasConsent)
    {
        var profile = CreateProfile(StorefrontStatus.Published);
        var consent = new StorefrontVisualConsent();
        if (hasConsent) Accept(consent, profile.Status);
        Assert.True(profile.Suspend("Moderation hold").IsSuccess);
        var before = VisualState(consent);
        var otherState = NonVisualState(profile);

        foreach (var revision in new[] { Revision, "r2" })
        {
            var result = consent.AcceptVisualConsent(profile.Status, ProfileKey, revision, TermsVersion, true, OwnerId, AcceptedAt);
            Assert.True(result.IsFailure);
            Assert.Equal("Validation.Status", result.Error.Code);
            Assert.Equal(before, VisualState(consent));
            Assert.Equal(otherState, NonVisualState(profile));
        }
    }

    [Theory]
    [InlineData(StorefrontStatus.Draft)]
    [InlineData(StorefrontStatus.PendingReview)]
    [InlineData(StorefrontStatus.Published)]
    [InlineData(StorefrontStatus.Suspended)]
    public void Withdraw_ShouldClearAllFiveFieldsOnly_AndBeIdempotent(StorefrontStatus status)
    {
        var profile = CreateProfile(status == StorefrontStatus.Suspended ? StorefrontStatus.Published : status);
        var consent = new StorefrontVisualConsent();
        Accept(consent, profile.Status);
        if (status == StorefrontStatus.Suspended)
            Assert.True(profile.Suspend("Moderation hold").IsSuccess);
        var before = NonVisualState(profile);
        var events = profile.DomainEvents.ToArray();

        var first = consent.WithdrawVisualConsent();
        var repeat = consent.WithdrawVisualConsent();

        Assert.True(first.IsSuccess);
        Assert.True(first.Value);
        Assert.True(repeat.IsSuccess);
        Assert.False(repeat.Value);
        AssertAbsent(consent);
        Assert.Equal(before, NonVisualState(profile));
        Assert.Equal(events, profile.DomainEvents);
    }

    [Theory]
    [InlineData(StorefrontStatus.Draft)]
    [InlineData(StorefrontStatus.Suspended)]
    public void WithdrawAbsentConsent_ShouldBeNoOp(StorefrontStatus status)
    {
        var profile = CreateProfile(status);
        var consent = new StorefrontVisualConsent();
        var before = NonVisualState(profile);

        var result = consent.WithdrawVisualConsent();

        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
        AssertAbsent(consent);
        Assert.Equal(before, NonVisualState(profile));
        Assert.Empty(profile.DomainEvents);
    }

    [Fact]
    public void WithdrawWhileSuspended_ShouldNotPermitAcceptance_OrResurrectConsentOnApproval()
    {
        var profile = CreateProfile(StorefrontStatus.Published);
        var consent = new StorefrontVisualConsent();
        Accept(consent, profile.Status);
        Assert.True(profile.Suspend("Moderation hold").IsSuccess);
        Assert.True(consent.WithdrawVisualConsent().Value);

        var accept = consent.AcceptVisualConsent(profile.Status, ProfileKey, Revision, TermsVersion, true, OwnerId, AcceptedAt.AddDays(1));

        Assert.True(accept.IsFailure);
        Assert.Equal(StorefrontStatus.Suspended, profile.Status);
        AssertAbsent(consent);
        Assert.True(profile.Approve(99).IsSuccess);
        AssertAbsent(consent);
        Assert.Equal(7, profile.StreetPositionIndex);
    }

    [Fact]
    public void CguRenewalAndBrandingChange_ShouldNotCreateOrReplaceVisualConsent()
    {
        var profile = CreateProfile(StorefrontStatus.Draft);
        var consent = new StorefrontVisualConsent();
        profile.RecordConsentRenewal("2.0.0", Guid.NewGuid(), AcceptedAt);
        AssertAbsent(consent);
        Accept(consent, profile.Status);
        var before = VisualState(consent);

        profile.RecordConsentRenewal("3.0.0", Guid.NewGuid(), AcceptedAt.AddDays(1));
        var update = profile.UpdateProfile("New branding", null, "Description", "#112233", "#445566",
            StorefrontCategory.Services, FacadeTheme.Classic, "new@example.test", null, null, null, null, false);

        Assert.True(update.IsSuccess, update.Error.Description);
        Assert.Equal(before, VisualState(consent));
        Assert.True(consent.WithdrawVisualConsent().Value);
        profile.RecordConsentRenewal("4.0.0", Guid.NewGuid(), AcceptedAt.AddDays(2));
        AssertAbsent(consent);
    }

    private static StorefrontProfile CreateProfile(StorefrontStatus status)
    {
        // Keep the pre-L1 fixture unchanged: creation never needs a visual reference.
        var profile = StorefrontProfileTestData.CreateProfile(status == StorefrontStatus.Draft
            ? StorefrontStatus.Draft : StorefrontStatus.PendingReview);
        if (status is StorefrontStatus.Published or StorefrontStatus.Suspended)
            Assert.True(profile.Approve(7).IsSuccess);
        if (status == StorefrontStatus.Suspended)
            Assert.True(profile.Suspend("Moderation hold").IsSuccess);
        profile.ClearDomainEvents();
        return profile;
    }

    private static void Accept(StorefrontVisualConsent consent, StorefrontStatus status)
    {
        var result = consent.AcceptVisualConsent(status, ProfileKey, Revision, TermsVersion, true, OwnerId, AcceptedAt);
        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.True(result.Value);
    }

    private static void AssertAccepted(StorefrontVisualConsent consent) =>
        Assert.Equal(new object?[] { ProfileKey, Revision, TermsVersion, AcceptedAt, OwnerId }, VisualState(consent));

    private static void AssertAbsent(StorefrontVisualConsent consent) =>
        Assert.All(VisualState(consent), value => Assert.Null(value));

    private static object?[] VisualState(StorefrontVisualConsent consent) => new object?[]
    {
        consent.PublicVisualProfileKey, consent.PublicVisualProfileRevision, consent.VisualConsentVersion,
        consent.VisualConsentAcceptedAt, consent.VisualConsentAcceptedByUserId
    };

    private static object NonVisualState(StorefrontProfile profile) => new
    {
        profile.Id, profile.TenantId, profile.Slug, profile.DisplayName, profile.Tagline,
        profile.DescriptionMarkdown, profile.BrandPrimaryColorHex, profile.BrandSecondaryColorHex,
        profile.PublicLogoUrl, profile.PublicCoverImageUrl, profile.Category, profile.Status, profile.FacadeTheme,
        profile.PublicContactEmail, profile.PublicContactPhone, profile.PublicContactWhatsApp,
        profile.StreetPositionIndex, profile.OrderSubmissionEnabled, profile.PublishedAt, profile.SuspendedAt,
        profile.RejectionReason, profile.SuspensionReason, profile.ConsentVersion,
        profile.ConsentAcceptedAt, profile.ConsentAcceptedByUserId,
        profile.Version, profile.CreatedAt, profile.UpdatedAt, profile.CreatedBy, profile.UpdatedBy
    };
}
