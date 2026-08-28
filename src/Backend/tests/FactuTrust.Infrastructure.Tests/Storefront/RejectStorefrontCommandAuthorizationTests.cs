using System.Collections.Generic;
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
        var profile = StorefrontProfileTestData.CreateProfile(StorefrontStatus.Draft);
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
        var profile = StorefrontProfileTestData.CreateProfile(StorefrontStatus.PendingReview);
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

    /// <summary>
    /// CWE-117 (injection de log CR/LF) : un motif de rejet contenant \r/\n ne doit pas se
    /// retrouver tel quel dans le message de log (risque de fausses lignes forgées), mais
    /// l'état métier persisté (RejectionReason) n'est pas altéré par cette sanitisation qui
    /// s'applique uniquement au point de log.
    /// </summary>
    [Fact]
    public async Task Handle_sanitizes_crlf_in_reason_before_logging_but_persists_raw_reason()
    {
        var profile = StorefrontProfileTestData.CreateProfile(StorefrontStatus.PendingReview);
        var actorId = Guid.NewGuid();
        const string maliciousReason = "Contenu non conforme\r\nFAKE LOG LINE: admin granted access";

        var repo = new Mock<IStorefrontProfileRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetByIdAsync(profile.Id, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        repo.Setup(r => r.UpdateAsync(profile, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = CreateHandler(repo.Object, actorId, out var loggerMock);

        var capturedMessages = new List<string>();
        loggerMock
            .Setup(l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(new InvocationAction(invocation =>
            {
                var state = invocation.Arguments[2];
                var formatter = invocation.Arguments[4];
                var invokeMethod = formatter.GetType().GetMethod("Invoke");
                var formatted = (string)invokeMethod!.Invoke(formatter, new[] { state, null })!;
                capturedMessages.Add(formatted);
            }));

        var result = await handler.Handle(new RejectStorefrontCommand(profile.Id, maliciousReason), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Single(capturedMessages);
        var loggedMessage = capturedMessages[0];
        Assert.DoesNotContain("\r", loggedMessage);
        Assert.DoesNotContain("\n", loggedMessage);
        // Le contenu reste présent (ce n'est pas un filtre de contenu) mais, faute de \r/\n, il ne
        // peut plus être présenté comme une ligne de log distincte forgée par l'attaquant.
        Assert.Contains("FAKE LOG LINE", loggedMessage);

        // La sanitisation ne s'applique qu'au log : l'état métier persisté garde le motif brut.
        Assert.Equal(maliciousReason.Trim(), profile.RejectionReason);
    }

    /// <summary>CWE-117 : un motif de rejet anormalement long est tronqué dans le message de log.</summary>
    [Fact]
    public async Task Handle_truncates_overly_long_reason_before_logging()
    {
        var profile = StorefrontProfileTestData.CreateProfile(StorefrontStatus.PendingReview);
        var actorId = Guid.NewGuid();
        var longReason = new string('A', 400);

        var repo = new Mock<IStorefrontProfileRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetByIdAsync(profile.Id, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        repo.Setup(r => r.UpdateAsync(profile, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = CreateHandler(repo.Object, actorId, out var loggerMock);

        var capturedMessages = new List<string>();
        loggerMock
            .Setup(l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(new InvocationAction(invocation =>
            {
                var state = invocation.Arguments[2];
                var formatter = invocation.Arguments[4];
                var invokeMethod = formatter.GetType().GetMethod("Invoke");
                var formatted = (string)invokeMethod!.Invoke(formatter, new[] { state, null })!;
                capturedMessages.Add(formatted);
            }));

        var result = await handler.Handle(new RejectStorefrontCommand(profile.Id, longReason), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Single(capturedMessages);
        // Le message de log complet contient d'autres champs (acteur, ids) ; on vérifie seulement
        // que le motif tronqué (200 caractères) n'apparaît pas en entier (400 'A') dans le log.
        Assert.DoesNotContain(longReason, capturedMessages[0]);

        // L'état métier persisté garde le motif complet, non tronqué.
        Assert.Equal(longReason, profile.RejectionReason);
    }
}
