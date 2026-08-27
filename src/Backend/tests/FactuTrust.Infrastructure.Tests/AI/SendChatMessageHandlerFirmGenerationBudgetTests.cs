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
/// Lot 2.2 — test obligatoire du budget de générations pour le tour firm : « outil exécuté au
/// round 1, texte final insuffisant au round 2 » ⇒ aucune 3ᵉ génération (le provider factice
/// compte ses appels), repli déterministe persisté. Cf. plan v3, §3 Lot 2.2 et §7 Lot 6.
/// </summary>
public sealed class SendChatMessageHandlerFirmGenerationBudgetTests
{
    private sealed class FakeGateLease : IOllamaGateLease
    {
        public long WaitMilliseconds => 0;
        public void Dispose() { }
    }

    [Fact]
    public async Task FirmMission_Cpu_ToolAtRound1_ShortTextAtRound2_NeverCallsProviderAThirdTime()
    {
        var streamCallCount = 0;

        var ollama = new Mock<IOllamaClient>();
        ollama.Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        ollama.Setup(x => x.IsModelInstalledAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        ollama.Setup(x => x.StreamChatAsync(
                It.IsAny<OllamaChatRequest>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>()))
            .Returns(() =>
            {
                streamCallCount++;
                // Round 1 : le modèle appelle l'outil firm (aucun texte). Round 2 (et tout appel
                // ultérieur, s'il survenait par erreur) : texte final < 80 caractères, insuffisant
                // pour clore le tour sans synthèse forcée.
                return streamCallCount == 1 ? StreamToolCall() : StreamShortText();
            });

        var gate = new Mock<IOllamaGenerationGate>();
        gate.Setup(x => x.AcquireAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new FakeGateLease());

        var toolExecutor = new Mock<IAiToolExecutor>();
        toolExecutor.Setup(x => x.ExecuteAsync(
                "get_firm_portfolio_overview",
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<AiToolExecutionContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiToolResult.Ok("""{"dossiers":2}"""));

        var platform = new Mock<IPlatformAiSettingsService>();
        platform.Setup(x => x.GetDefaultModelRefAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        platform.Setup(x => x.GetStudioAiModelRefAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var inferenceResolver = new Mock<IOllamaInferenceProfileResolver>();
        inferenceResolver.Setup(x => x.ResolveForPlatformAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaInferenceProfile(
                OllamaInferenceDevice.CpuOnly, NumGpu: 0, NumThread: 8, NumBatch: 128, PreferAdaptiveChatNumCtx: false));

        var conversations = new Mock<IConversationRepository>();
        conversations.Setup(x => x.AddAsync(It.IsAny<Conversation>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        conversations.Setup(x => x.UpdateAsync(It.IsAny<Conversation>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(x => x.TenantId).Returns(Guid.NewGuid());
        currentUser.SetupGet(x => x.Email).Returns("chef@cabinet.tn");
        currentUser.SetupGet(x => x.Role).Returns(UserRole.FirmManager);
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
            .ReturnsAsync("Tu es l'assistant Chef de mission.");

        var handler = new SendChatMessageHandler(
            ollama.Object,
            gate.Object,
            Mock.Of<IOpenAiChatCompletionsClient>(),
            Mock.Of<ICursorAgentClient>(),
            new CursorToolRunRegistry(),
            tenant.Object,
            platform.Object,
            Mock.Of<IModalCredentialsResolver>(),
            inferenceResolver.Object,
            toolExecutor.Object,
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
            Options.Create(new OllamaSettings
            {
                DefaultModel = "mistral",
                ConversationalFastPathEnabled = false,
                // Lot 1 (raccourci + grounding gate) désactivé : ce test isole le budget de générations
                // du Lot 2.2 (round outils + rédaction ≤ 2 générations) du comportement Lot 1 — le gate
                // et le raccourci firm ont leurs propres tests d'intégration. Flags off ⇒ comportement
                // strictement identique à avant le Lot 1 (garantie de non-régression du plan v3).
                FirmMissionShortcutEnabled = false,
                FirmMissionGroundingGateEnabled = false
            }),
            Options.Create(new ScreenAnalysisOptions()),
            Options.Create(new CursorSdkSettings { Enabled = false }));

        var command = new SendChatMessageCommand(
            null,
            "Où en est le portefeuille du cabinet aujourd'hui ?",
            Options: new ChatRequestOptionsDto { AgentScope = AssistantAgentScope.FirmMission });

        var events = new List<ChatStreamEvent>();
        await foreach (var ev in handler.HandleAsync(command, Guid.NewGuid()))
            events.Add(ev);

        var dump = string.Join(" || ", events.Select(e =>
            $"{e.Type}:{e.Phase}:{e.PhaseStatus}:{(e.Content ?? e.Error ?? e.Detail ?? "")}"));

        // Le provider factice ne doit jamais être appelé une 3ᵉ fois : round 1 (outil) + round 2
        // (texte insuffisant) = budget de 2 générations épuisé ⇒ la synthèse forcée est sautée.
        Assert.True(streamCallCount == 2, $"Expected exactly 2 provider calls, got {streamCallCount}. Events: {dump}");
        ollama.Verify(x => x.StreamChatAsync(
            It.IsAny<OllamaChatRequest>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<bool>()), Times.Exactly(2));

        // Aucune synthèse forcée n'a été lancée (budget déjà épuisé au moment où elle serait éligible).
        Assert.DoesNotContain(events, e => e.Type == "phase" && e.Phase == "llm_forced_synthesis");

        // Le repli déterministe (existant, non modifié) a bien pris le relais : contenu non vide,
        // différent du texte insuffisant du round 2, réconcilié via content_replace.
        var lastReplace = events.LastOrDefault(e => e.Type == "content_replace");
        Assert.NotNull(lastReplace);
        Assert.False(string.IsNullOrWhiteSpace(lastReplace!.Content));
        Assert.NotEqual("Ok.", lastReplace.Content);

        Assert.Contains(events, e => e.Type == "done");
        Assert.DoesNotContain(events, e => e.Type == "error");
        toolExecutor.Verify(x => x.ExecuteAsync(
            "get_firm_portfolio_overview",
            It.IsAny<Dictionary<string, object?>>(),
            It.IsAny<AiToolExecutionContext>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static async IAsyncEnumerable<OllamaChatChunk> StreamToolCall(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        yield return new OllamaChatChunk
        {
            Message = new OllamaChatMessage
            {
                Role = "assistant",
                Content = string.Empty,
                ToolCalls = new List<OllamaToolCall>
                {
                    new()
                    {
                        Id = "call-1",
                        Function = new OllamaToolCallFunction
                        {
                            Name = "get_firm_portfolio_overview",
                            Arguments = new Dictionary<string, object?>()
                        }
                    }
                }
            },
            Done = true
        };
    }

    private static async IAsyncEnumerable<OllamaChatChunk> StreamShortText(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        yield return new OllamaChatChunk
        {
            Message = new OllamaChatMessage { Role = "assistant", Content = "Ok." },
            Done = true
        };
    }
}
