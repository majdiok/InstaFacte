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

public sealed class SendChatMessageHandlerModalTests
{
    [Fact]
    public async Task HandleAsync_Modal_StreamsContent_AndDoesNotCallOllamaOrCursor()
    {
        var openAi = new Mock<IOpenAiChatCompletionsClient>(MockBehavior.Strict);
        openAi.Setup(x => x.StreamChatAsOllamaCompatibleAsync(
                "https://example--ep-kimi-k3-server.us-west.modal.direct/v1",
                "wk-id.ws-secret",
                "moonshotai/Kimi-K3",
                It.IsAny<IReadOnlyList<OpenAiChatMessagePayload>>(),
                It.IsAny<IReadOnlyList<OllamaToolDefinition>>(),
                It.IsAny<double>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>(),
                It.Is<OpenAiCompatibleCallOptions?>(o =>
                    o != null
                    && o.HttpClientName == OpenAiCompatibleCallOptions.ModalClientName
                    && o.AddOpenRouterHeaders == false
                    && o.ReasoningEffort == "none"
                    && !string.IsNullOrWhiteSpace(o.SessionId))))
            .Returns(StreamChunks(
                "Le chiffre d'affaires d'avril 2026 s'élève à 12 500 TND hors taxes pour l'ensemble des factures."));

        var platform = new Mock<IPlatformAiSettingsService>();
        platform.Setup(x => x.GetDefaultModelRefAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("modal:moonshotai/Kimi-K3");
        platform.Setup(x => x.GetStudioAiModelRefAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var modalResolver = new Mock<IModalCredentialsResolver>();
        modalResolver.Setup(x => x.ResolveAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResolvedModalCredentials(
                true,
                "wk-id.ws-secret",
                "https://example--ep-kimi-k3-server.us-west.modal.direct/v1",
                ModalCredentialSource.Platform));

        var conversations = new Mock<IConversationRepository>();
        conversations.Setup(x => x.AddAsync(It.IsAny<Conversation>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        conversations.Setup(x => x.UpdateAsync(It.IsAny<Conversation>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
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
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Tu es l'assistant InstaFact.");

        var cursor = new Mock<ICursorAgentClient>(MockBehavior.Strict);

        var handler = new SendChatMessageHandler(
            Mock.Of<IOllamaClient>(),
            Mock.Of<IOllamaGenerationGate>(),
            openAi.Object,
            cursor.Object,
            new CursorToolRunRegistry(),
            tenant.Object,
            platform.Object,
            modalResolver.Object,
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
            Options.Create(new OllamaSettings { DefaultModel = "mistral", ConversationalFastPathEnabled = false }),
            Options.Create(new ScreenAnalysisOptions()),
            Options.Create(new CursorSdkSettings { Enabled = true }),
            modalSettings: Options.Create(new ModalSettings { EnableStickySessions = true, EnableTools = true }));

        var events = new List<ChatStreamEvent>();
        await foreach (var ev in handler.HandleAsync(
            new SendChatMessageCommand(null, "Quel est le chiffre d'affaires d'avril 2026 ?"),
            Guid.NewGuid()))
        {
            events.Add(ev);
        }

        var dump = string.Join(" || ", events.Select(e =>
            $"{e.Type}:{(e.Content ?? e.Error ?? e.Detail ?? "")}"));
        Assert.True(
            events.Any(e =>
                (e.Type == "content" || e.Type == "content_replace")
                && (e.Content ?? "").Contains("12 500")),
            dump);
        Assert.Contains(events, e => e.Type == "done");
        Assert.DoesNotContain(events, e => e.Type == "error");
        openAi.Verify(x => x.StreamChatAsOllamaCompatibleAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<IReadOnlyList<OpenAiChatMessagePayload>>(),
            It.IsAny<IReadOnlyList<OllamaToolDefinition>>(),
            It.IsAny<double>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<int?>(),
            It.IsAny<OpenAiCompatibleCallOptions?>()), Times.AtLeastOnce);
        cursor.VerifyNoOtherCalls();
    }

    private static async IAsyncEnumerable<OllamaChatChunk> StreamChunks(
        string text,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        yield return new OllamaChatChunk
        {
            Message = new OllamaChatMessage { Role = "assistant", Content = text },
            Done = true
        };
    }
}
