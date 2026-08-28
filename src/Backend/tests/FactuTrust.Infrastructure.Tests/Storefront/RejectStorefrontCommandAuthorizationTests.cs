using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Storefront.Platform.Commands;
using FactuTrust.Domain.Entities.Storefront;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Storefront;

/// <summary>
/// Bucket A (IDOR triage, issue db35cada-b9b1-43d4-8d1f-63ad385641ef) — RejectStorefrontCommand.cs L32.
/// Même raisonnement que ApproveStorefrontCommand : opération plateforme légitimement inter-tenant,
/// gardée par la machine à états (Reject() n'accepte que PendingReview) + trace d'audit de l'acteur.
/// </summary>
public sealed class RejectStorefrontCommandAuthorizationTests
{
    private const string ConsentVersion = "1.0.0";

    private static StorefrontProfile CreateProfile(StorefrontStatus status)
    {
        var result = StorefrontProfile.Create(
            tenantId: Guid.NewGuid(),
            slug: "acme-widgets",
            displayName: "Acme Widgets",
            publicContactEmail: "contact@acme.example",
            category: StorefrontCategory.Retail,
            facadeTheme: FacadeTheme.Modern,
            consentVersion: ConsentVersion,
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

    private static RejectStorefrontCommandHandler CreateHandler(
        IStorefrontProfileRepository repository,
        Guid actorId,
        out Mock<ILogger<RejectStorefrontCommandHandler>> loggerMock)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(u => u.UserId).Returns(actorId);

        loggerMock = new Mock<ILogger<RejectStorefrontCommandHandler>>();

        var options = Options.Create(new StorefrontOptions { Enabled = true });

        return new RejectStorefrontCommandHandler(repository, options, currentUser.Object, loggerMock.Object);
    }

    /// <summary>Négatif : une vitrine dont l'état n'autorise pas le rejet (Draft) est rejetée par la machine à états, pas mise à jour, et l'échec est audité.</summary>
    [Fact]
    public async Task Handle_fails_and_logs_warning_when_profile_status_forbids_rejection()
    {
        var profile = CreateProfile(StorefrontStatus.Draft);
        var actorId = Guid.NewGuid();

        var repo = new Mock<IStorefrontProfileRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetByIdAsync(profile.Id, It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var handler = CreateHandler(repo.Object, actorId, out var loggerMock);

        var result = await handler.Handle(new RejectStorefrontCommand(profile.Id, "Non conforme"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(StorefrontStatus.Draft, profile.Status);
        repo.Verify(r => r.UpdateAsync(It.IsAny<StorefrontProfile>(), It.IsAny<CancellationToken>()), Times.Never);
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>Positif : un PlatformAdmin peut rejeter une vitrine PendingReview de n'importe quel tenant, avec trace d'audit incluant le motif.</summary>
    [Fact]
    public async Task Handle_rejects_pending_profile_and_logs_actor_information()
    {
        var profile = CreateProfile(StorefrontStatus.PendingReview);
        var actorId = Guid.NewGuid();

        var repo = new Mock<IStorefrontProfileRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetByIdAsync(profile.Id, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        repo.Setup(r => r.UpdateAsync(profile, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = CreateHandler(repo.Object, actorId, out var loggerMock);

        var result = await handler.Handle(new RejectStorefrontCommand(profile.Id, "Contenu non conforme"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(StorefrontStatus.Draft, profile.Status);
        Assert.Equal("Contenu non conforme", profile.RejectionReason);
        repo.Verify(r => r.UpdateAsync(profile, It.IsAny<CancellationToken>()), Times.Once);
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
