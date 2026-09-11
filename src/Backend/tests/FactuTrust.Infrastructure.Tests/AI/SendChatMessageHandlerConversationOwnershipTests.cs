using System.Runtime.CompilerServices;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.Commands;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Domain.Entities.AI;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Services.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

/// <summary>
/// Bucket B (IDOR) — SendChatMessageCommand.cs : la conversation ciblée par ConversationId doit être
/// filtrée par ICurrentUser.UserId (via GetByIdForChatAsync(id, userId, ...) / GetByIdForUserAsync)
/// avant tout traitement, comme GetConversationQuery / DeleteConversationCommand. On ne tire ici que le
/// premier événement du flux : il suffit à distinguer "propriétaire → passe la garde et démarre le
/// traitement" de "id d'un autre utilisateur → NotFound immédiat", sans avoir à simuler un fournisseur
/// LLM complet.
/// </summary>
public sealed class SendChatMessageHandlerConversationOwnershipTests
{
    private static SendChatMessageHandler CreateHandler(
        Mock<IConversationRepository> conversations,
        Guid currentUserId)
    {
        var platform = new Mock<IPlatformAiSettingsService>();
        platform.Setup(x => x.GetDefaultModelRefAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("ollama:mistral");
        platform.Setup(x => x.GetStudioAiModelRefAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(x => x.UserId).Returns(currentUserId);
        currentUser.SetupGet(x => x.TenantId).Returns(Guid.NewGuid());
        currentUser.SetupGet(x => x.Email).Returns("a@b.c");
        currentUser.SetupGet(x => x.Role).Returns(UserRole.Accountant);
        currentUser.SetupGet(x => x.IsAccountingFirmDelegatedContext).Returns(false);
        currentUser.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);

        var tenant = new Mock<ITenantContext>();
        tenant.SetupGet(x => x.TenantId).Returns(Guid.NewGuid());
        tenant.SetupGet(x => x.ConnectionString).Returns("Server=.;Database=t");

        var contextBuilder = new Mock<IAiContextBuilder>();
        contextBuilder.Setup(x => x.BuildSystemPromptAsync(
                It.IsAny<AssistantMode>(),
                It.IsAny<string?>(),
                It.IsAny<AssistantAgentScope>(),
                It.IsAny<StudioPromptOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Tu es l'assistant InstaFact.");

        return new SendChatMessageHandler(
            Mock.Of<IOllamaClient>(),
            Mock.Of<IOllamaGenerationGate>(),
            Mock.Of<IOpenAiChatCompletionsClient>(),
            Mock.Of<ICursorAgentClient>(),
            new CursorToolRunRegistry(),
            tenant.Object,
            platform.Object,
            Mock.Of<IModalCredentialsResolver>(),
            Mock.Of<IOllamaInferenceProfileResolver>(),
            Mock.Of<IAiToolExecutor>(),
            Mock.Of<IAiToolExecutorScopeFactory>(),
            contextBuilder.Object,
            AiVolatileContextFormatter.CreateDefault(),
            new AiScreenAnalysisEnricher(
                Mock.Of<IAiToolExecutor>(),
                Options.Create(new ScreenAnalysisOptions()),
                NullLogger<AiScreenAnalysisEnricher>.Instance),
            conversations.Object,
            currentUser.Object,
            NullLogger<SendChatMessageHandler>.Instance,
            Options.Create(new OllamaSettings { DefaultModel = "mistral" }),
            Options.Create(new ScreenAnalysisOptions()),
            Options.Create(new CursorSdkSettings { Enabled = true }));
    }

    /// <summary>Négatif : la conversation ciblée appartient à un autre utilisateur → NotFound immédiat, avant tout appel fournisseur LLM.</summary>
    [Fact]
    public async Task HandleAsync_throws_not_found_when_conversation_belongs_to_another_user()
    {
        var otherUserId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        var conversations = new Mock<IConversationRepository>(MockBehavior.Strict);
        conversations
            .Setup(x => x.GetByIdForChatAsync(conversationId, otherUserId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation?)null);

        var handler = CreateHandler(conversations, otherUserId);

        var enumerator = handler
            .HandleAsync(new SendChatMessageCommand(conversationId, "Bonjour"), otherUserId)
            .GetAsyncEnumerator();

        await Assert.ThrowsAsync<KeyNotFoundException>(async () => await enumerator.MoveNextAsync());
        conversations.VerifyAll();
    }

    /// <summary>Positif : le propriétaire peut reprendre sa propre conversation, qui passe la garde de propriété et démarre le traitement.</summary>
    [Fact]
    public async Task HandleAsync_passes_ownership_check_and_starts_processing_for_its_owner()
    {
        var ownerId = Guid.NewGuid();
        var conversation = Conversation.Create(ownerId, "Analyse ventes", "mistral");

        var conversations = new Mock<IConversationRepository>(MockBehavior.Strict);
        conversations
            .Setup(x => x.GetByIdForChatAsync(conversation.Id, ownerId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        var handler = CreateHandler(conversations, ownerId);

        var enumerator = handler
            .HandleAsync(new SendChatMessageCommand(conversation.Id, "Bonjour"), ownerId)
            .GetAsyncEnumerator();

        // On ne tire que le premier événement : il prouve que la garde de propriété est passée (pas
        // d'exception) et que le traitement démarre — pas besoin de simuler un fournisseur LLM complet.
        var hasFirst = await enumerator.MoveNextAsync();
        Assert.True(hasFirst);
        Assert.Equal("phase", enumerator.Current.Type);
        Assert.Equal("provider_availability", enumerator.Current.Phase);

        await enumerator.DisposeAsync();
        conversations.VerifyAll();
    }
}
