using System.Runtime.CompilerServices;
using System.Text.Json;
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
/// PR 1.2 — bascule « modèle avancé » par requête en mode StudioBuilder (décision D3) :
/// <list type="bullet">
/// <item>demandé + drapeau actif + référence configurée ⇒ le tour utilise le modèle avancé et l'événement
/// SSE <c>meta</c> annonce <c>usedAdvancedModel=true</c> ;</item>
/// <item>drapeau inactif ⇒ repli silencieux sur le modèle standard, <c>advancedModelFallbackReason="disabled"</c> ;</item>
/// <item>référence vide ⇒ repli, raison <c>"not_configured"</c> ;</item>
/// <item>fournisseur avancé indisponible ⇒ repli, raison <c>"unavailable"</c>, JAMAIS d'événement <c>error</c> ;</item>
/// <item>hors StudioBuilder l'option est ignorée : aucune lecture Master du modèle avancé, aucun <c>meta</c> ;</item>
/// <item>les lectures Master (<c>IPlatformAiSettingsService</c>) restent strictement séquentielles.</item>
/// </list>
/// </summary>
public sealed class SendChatMessageHandlerStudioAdvancedModelTests
{
    private const string StandardModelRef = "openrouter:mistralai/mistral-small";
    private const string AdvancedModelRef = "openrouter:anthropic/claude-sonnet-4";

    private sealed class Harness
    {
        public Mock<IOpenAiChatCompletionsClient> OpenAi { get; } = new();
        public Mock<IPlatformAiSettingsService> Platform { get; } = new();
        public Mock<IAiContextBuilder> ContextBuilder { get; } = new();
        public Mock<IOllamaClient> Ollama { get; } = new();
        public List<string> ModelsCalled { get; } = new();
        public List<StudioPromptOptions?> PromptOptionsSeen { get; } = new();
        public OllamaSettings Settings { get; } = new()
        {
            DefaultModel = "mistral",
            ConversationalFastPathEnabled = false,
            EnableStudioAiAdvancedModel = true
        };

        public SendChatMessageHandler Build()
        {
            OpenAi.Setup(x => x.StreamChatAsOllamaCompatibleAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyList<OpenAiChatMessagePayload>>(),
                    It.IsAny<IReadOnlyList<OllamaToolDefinition>>(),
                    It.IsAny<double>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<int?>(),
                    It.IsAny<OpenAiCompatibleCallOptions?>()))
                .Returns((string _, string _, string model, IReadOnlyList<OpenAiChatMessagePayload> _,
                    IReadOnlyList<OllamaToolDefinition> _, double _, int _, CancellationToken _, int? _,
                    OpenAiCompatibleCallOptions? _) =>
                {
                    ModelsCalled.Add(model);
                    return StreamChunks("Voici le plan : table Employés (nom, poste) et table Congés (employé, dates, statut).");
                });

            Platform.Setup(x => x.GetDefaultModelRefAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(StandardModelRef);
            Platform.Setup(x => x.GetStudioAiModelRefAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(StandardModelRef);
            Platform.Setup(x => x.GetStudioAiAdvancedModelRefAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(AdvancedModelRef);
            Platform.Setup(x => x.GetOpenRouterCredentialsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PlatformOpenRouterCredentials(true, "sk-or-test", "https://openrouter.ai/api/v1"));

            var conversations = new Mock<IConversationRepository>();
            conversations.Setup(x => x.AddAsync(It.IsAny<Conversation>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            conversations.Setup(x => x.UpdateAsync(It.IsAny<Conversation>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var currentUser = new Mock<ICurrentUser>();
            currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
            currentUser.SetupGet(x => x.TenantId).Returns(Guid.NewGuid());
            currentUser.SetupGet(x => x.Email).Returns("a@b.c");
            currentUser.SetupGet(x => x.Role).Returns(UserRole.Administrator);
            currentUser.SetupGet(x => x.IsAccountingFirmDelegatedContext).Returns(false);
            currentUser.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);

            var tenant = new Mock<ITenantContext>();
            tenant.SetupGet(x => x.TenantId).Returns(Guid.NewGuid());
            tenant.SetupGet(x => x.ConnectionString).Returns("Server=.;Database=t");

            ContextBuilder.Setup(x => x.BuildSystemPromptAsync(
                    It.IsAny<AssistantMode>(),
                    It.IsAny<string?>(),
                    It.IsAny<AssistantAgentScope>(),
                    It.IsAny<StudioPromptOptions?>(),
                    It.IsAny<CancellationToken>()))
                .Callback((AssistantMode _, string? _, AssistantAgentScope _, StudioPromptOptions? o, CancellationToken _) =>
                    PromptOptionsSeen.Add(o))
                .ReturnsAsync("Tu es l'assistant Studio.");

            return new SendChatMessageHandler(
                Ollama.Object,
                Mock.Of<IOllamaGenerationGate>(),
                OpenAi.Object,
                Mock.Of<ICursorAgentClient>(),
                new CursorToolRunRegistry(),
                tenant.Object,
                Platform.Object,
                Mock.Of<IModalCredentialsResolver>(),
                Mock.Of<IOllamaInferenceProfileResolver>(),
                Mock.Of<IAiToolExecutor>(),
                Mock.Of<IAiToolExecutorScopeFactory>(),
                ContextBuilder.Object,
                AiVolatileContextFormatter.CreateDefault(),
                new AiScreenAnalysisEnricher(
                    Mock.Of<IAiToolExecutor>(),
                    Options.Create(new ScreenAnalysisOptions()),
                    NullLogger<AiScreenAnalysisEnricher>.Instance),
                conversations.Object,
                currentUser.Object,
                NullLogger<SendChatMessageHandler>.Instance,
                Options.Create(Settings),
                Options.Create(new ScreenAnalysisOptions()),
                Options.Create(new CursorSdkSettings { Enabled = false }),
                modalSettings: Options.Create(new ModalSettings()));
        }
    }

    private static SendChatMessageCommand StudioCommand(bool useAdvanced, string? intent = null) => new(
        null,
        "Crée un système de gestion des congés",
        Options: new ChatRequestOptionsDto
        {
            AssistantMode = AssistantMode.StudioBuilder,
            UseAdvancedModel = useAdvanced,
            StudioIntent = intent
        });

    private static async Task<List<ChatStreamEvent>> RunAsync(SendChatMessageHandler handler, SendChatMessageCommand command)
    {
        var events = new List<ChatStreamEvent>();
        await foreach (var ev in handler.HandleAsync(command, Guid.NewGuid()))
            events.Add(ev);
        return events;
    }

    private static JsonElement? ReadMeta(IReadOnlyList<ChatStreamEvent> events)
    {
        var meta = events.SingleOrDefault(e => e.Type == "meta");
        return meta is null ? null : JsonDocument.Parse(meta.Content!).RootElement;
    }

    private static string Dump(IEnumerable<ChatStreamEvent> events) =>
        string.Join(" || ", events.Select(e => $"{e.Type}:{e.Content ?? e.Error ?? e.Detail ?? ""}"));

    [Fact]
    public async Task Advanced_requested_and_configured_uses_advanced_model_and_announces_it()
    {
        var h = new Harness();
        var handler = h.Build();

        var events = await RunAsync(handler, StudioCommand(useAdvanced: true, intent: "system"));

        Assert.DoesNotContain(events, e => e.Type == "error");
        Assert.Contains(events, e => e.Type == "done");
        Assert.Equal(["anthropic/claude-sonnet-4"], h.ModelsCalled.Distinct());

        var meta = ReadMeta(events);
        Assert.NotNull(meta);
        Assert.True(meta!.Value.GetProperty("usedAdvancedModel").GetBoolean(), Dump(events));
        Assert.Equal(JsonValueKind.Null, meta.Value.GetProperty("advancedModelFallbackReason").ValueKind);
        Assert.Equal("anthropic/claude-sonnet-4", meta.Value.GetProperty("model").GetString());

        // Le `meta` précède le premier token : l'atelier affiche la bannière avant la réponse.
        var metaIndex = events.FindIndex(e => e.Type == "meta");
        var firstContent = events.FindIndex(e => e.Type is "content" or "content_replace");
        Assert.True(metaIndex >= 0 && (firstContent < 0 || metaIndex < firstContent), Dump(events));

        // Le prompt est bâti avec le budget « avancé » et l'intention normalisée.
        var options = Assert.Single(h.PromptOptionsSeen);
        Assert.NotNull(options);
        Assert.True(options!.UseAdvancedModel);
        Assert.Equal("system", options.NormalizedIntent);
    }

    [Fact]
    public async Task Flag_disabled_falls_back_silently_with_reason_disabled()
    {
        var h = new Harness();
        h.Settings.EnableStudioAiAdvancedModel = false;
        var handler = h.Build();

        var events = await RunAsync(handler, StudioCommand(useAdvanced: true));

        Assert.DoesNotContain(events, e => e.Type == "error");
        Assert.Contains(events, e => e.Type == "done");
        Assert.Equal(["mistralai/mistral-small"], h.ModelsCalled.Distinct());

        var meta = ReadMeta(events);
        Assert.NotNull(meta);
        Assert.False(meta!.Value.GetProperty("usedAdvancedModel").GetBoolean());
        Assert.Equal("disabled", meta.Value.GetProperty("advancedModelFallbackReason").GetString());
        Assert.Equal("mistralai/mistral-small", meta.Value.GetProperty("model").GetString());

        // Drapeau inactif ⇒ pas même une lecture Master de la référence avancée.
        h.Platform.Verify(x => x.GetStudioAiAdvancedModelRefAsync(It.IsAny<CancellationToken>()), Times.Never);
        var options = Assert.Single(h.PromptOptionsSeen);
        Assert.False(options!.UseAdvancedModel);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Empty_advanced_reference_falls_back_with_reason_not_configured(string? configured)
    {
        var h = new Harness();
        var handler = h.Build(); // les redéfinitions ci-dessous priment sur les réglages par défaut du harnais
        h.Platform.Setup(x => x.GetStudioAiAdvancedModelRefAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configured);

        var events = await RunAsync(handler, StudioCommand(useAdvanced: true));

        Assert.DoesNotContain(events, e => e.Type == "error");
        Assert.Equal(["mistralai/mistral-small"], h.ModelsCalled.Distinct());
        var meta = ReadMeta(events);
        Assert.NotNull(meta);
        Assert.False(meta!.Value.GetProperty("usedAdvancedModel").GetBoolean());
        Assert.Equal("not_configured", meta.Value.GetProperty("advancedModelFallbackReason").GetString());
    }

    [Fact]
    public async Task Unavailable_advanced_provider_falls_back_to_standard_with_reason_unavailable()
    {
        // Modèle avancé sur Ollama, non installé ⇒ indisponible. Le standard (OpenRouter) prend le relais.
        var h = new Harness();
        var handler = h.Build();
        h.Platform.Setup(x => x.GetStudioAiAdvancedModelRefAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("ollama:qwen3:32b");
        h.Ollama.Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        h.Ollama.Setup(x => x.IsModelInstalledAsync("qwen3:32b", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var events = await RunAsync(handler, StudioCommand(useAdvanced: true));

        Assert.DoesNotContain(events, e => e.Type == "error");
        Assert.Contains(events, e => e.Type == "done");
        Assert.Equal(["mistralai/mistral-small"], h.ModelsCalled.Distinct());

        var meta = ReadMeta(events);
        Assert.NotNull(meta);
        Assert.False(meta!.Value.GetProperty("usedAdvancedModel").GetBoolean());
        Assert.Equal("unavailable", meta.Value.GetProperty("advancedModelFallbackReason").GetString());
        Assert.Equal("mistralai/mistral-small", meta.Value.GetProperty("model").GetString());

        // Aucune phase provider_availability « failed » : le repli n'est pas une erreur.
        Assert.DoesNotContain(events, e => e.Type == "phase" && e.PhaseStatus == "failed");
        // Deux vérifications de disponibilité : l'avancé (échec silencieux) puis le standard (succès).
        Assert.Equal(2, events.Count(e => e.Type == "phase" && e.Phase == "provider_availability" && e.PhaseStatus == "running"));
        // Le prompt a été rebâti au budget CPU après le repli.
        Assert.Equal(2, h.PromptOptionsSeen.Count);
        Assert.True(h.PromptOptionsSeen[0]!.UseAdvancedModel);
        Assert.False(h.PromptOptionsSeen[1]!.UseAdvancedModel);
    }

    [Fact]
    public async Task Default_mode_ignores_the_option_and_emits_no_meta()
    {
        var h = new Harness();
        var handler = h.Build();

        var events = await RunAsync(handler, new SendChatMessageCommand(
            null,
            "Quel est le chiffre d'affaires d'avril 2026 ?",
            Options: new ChatRequestOptionsDto { UseAdvancedModel = true, StudioIntent = "system" }));

        Assert.DoesNotContain(events, e => e.Type == "error");
        Assert.DoesNotContain(events, e => e.Type == "meta");
        Assert.Equal(["mistralai/mistral-small"], h.ModelsCalled.Distinct());
        h.Platform.Verify(x => x.GetStudioAiAdvancedModelRefAsync(It.IsAny<CancellationToken>()), Times.Never);
        h.Platform.Verify(x => x.GetStudioAiModelRefAsync(It.IsAny<CancellationToken>()), Times.Never);
        var options = Assert.Single(h.PromptOptionsSeen);
        Assert.Null(options);
    }

    [Fact]
    public async Task Master_reads_are_strictly_sequential()
    {
        // Les getters IPlatformAiSettingsService partagent un MasterDbContext scoped : deux lectures
        // simultanées provoquent « A second operation was started on this context ». Chaque getter
        // simule une latence et échoue si un autre getter est encore en cours.
        var h = new Harness();
        var inFlight = 0;
        var overlapDetected = false;

        async Task<string?> Guarded(string? value)
        {
            if (Interlocked.Increment(ref inFlight) > 1)
                overlapDetected = true;
            await Task.Delay(20);
            Interlocked.Decrement(ref inFlight);
            return value;
        }

        var handler = h.Build();
        h.Platform.Setup(x => x.GetDefaultModelRefAsync(It.IsAny<CancellationToken>()))
            .Returns(() => Guarded(StandardModelRef));
        h.Platform.Setup(x => x.GetStudioAiModelRefAsync(It.IsAny<CancellationToken>()))
            .Returns(() => Guarded(StandardModelRef));
        h.Platform.Setup(x => x.GetStudioAiAdvancedModelRefAsync(It.IsAny<CancellationToken>()))
            .Returns(() => Guarded(AdvancedModelRef));

        var events = await RunAsync(handler, StudioCommand(useAdvanced: true));

        Assert.DoesNotContain(events, e => e.Type == "error");
        Assert.False(overlapDetected, "Deux lectures Master se sont chevauchées.");
        h.Platform.Verify(x => x.GetDefaultModelRefAsync(It.IsAny<CancellationToken>()), Times.Once);
        h.Platform.Verify(x => x.GetStudioAiModelRefAsync(It.IsAny<CancellationToken>()), Times.Once);
        h.Platform.Verify(x => x.GetStudioAiAdvancedModelRefAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Unavailable_standard_provider_is_still_an_error_after_the_loop_refactor()
    {
        // Verrou de non-régression de la restructuration en boucle : quand c'est le modèle STANDARD
        // (mode Default, Ollama arrêté) qui est indisponible, la garde de disponibilité émet exactement
        // comme avant — phase provider_availability « failed » puis l'erreur française — et s'arrête.
        var h = new Harness();
        var handler = h.Build();
        h.Platform.Setup(x => x.GetDefaultModelRefAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("ollama:qwen2.5:7b-instruct");
        h.Ollama.Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var events = await RunAsync(handler, new SendChatMessageCommand(null, "Quel est le chiffre d'affaires d'avril 2026 ?"));

        var failed = Assert.Single(events, e => e.Type == "phase" && e.Phase == "provider_availability" && e.PhaseStatus == "failed");
        Assert.Equal("ollama:qwen2.5:7b-instruct", failed.Detail);
        var error = Assert.Single(events, e => e.Type == "error");
        Assert.Equal(
            "Le moteur IA InstaFact est indisponible. Vérifiez que le service est démarré sur le serveur, ou choisissez un modèle cloud.",
            error.Error);
        Assert.Same(error, events[^1]);
        Assert.Equal(1, events.Count(e => e.Type == "phase" && e.Phase == "provider_availability" && e.PhaseStatus == "running"));
        Assert.DoesNotContain(events, e => e.Type is "meta" or "content" or "done");
        Assert.Empty(h.ModelsCalled);
    }

    [Fact]
    public async Task Unavailable_standard_provider_in_studio_mode_emits_error_without_meta()
    {
        // Même garde en StudioBuilder sans bascule : l'événement `meta` n'est émis qu'après une
        // disponibilité confirmée — un échec du standard reste une erreur, pas un repli.
        var h = new Harness();
        var handler = h.Build();
        h.Platform.Setup(x => x.GetStudioAiModelRefAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("ollama:qwen2.5:7b-instruct");
        h.Ollama.Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        h.Ollama.Setup(x => x.IsModelInstalledAsync("qwen2.5:7b-instruct", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var events = await RunAsync(handler, StudioCommand(useAdvanced: false));

        Assert.Single(events, e => e.Type == "phase" && e.Phase == "provider_availability" && e.PhaseStatus == "failed");
        var error = Assert.Single(events, e => e.Type == "error");
        Assert.Equal(
            "Le modèle configuré pour l'assistant n'est pas installé sur le moteur IA InstaFact. Contactez l'administrateur plateforme.",
            error.Error);
        Assert.DoesNotContain(events, e => e.Type == "meta");
        Assert.Empty(h.ModelsCalled);
    }

    [Theory]
    [InlineData("openrouter:anthropic/claude-sonnet-4", "anthropic/claude-sonnet-4")]
    [InlineData("ollama:qwen2.5:7b-instruct", "qwen2.5:7b-instruct")]
    [InlineData("qwen2.5:7b-instruct", "qwen2.5:7b-instruct")]
    [InlineData("modal:meta/llama-3-70b", "meta/llama-3-70b")]
    [InlineData("cursor:gpt-5|reasoning=high", "gpt-5")]
    [InlineData("  ollama:  ", "ollama:")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void HumanLabel_is_shared_between_capabilities_and_meta(string? raw, string expected)
    {
        // Le libellé de `meta.model` et celui des capacités (`advancedModelLabel`) viennent de la même
        // fonction : l'atelier affiche le même nom dans la bannière et dans la bascule.
        Assert.Equal(expected, ModelRef.HumanLabel(raw));
        if (!string.IsNullOrWhiteSpace(raw))
        {
            var parsed = ModelRef.Parse(raw);
            using var doc = JsonDocument.Parse(SendChatMessageHandler.BuildStudioMetaJson(true, null, parsed));
            Assert.Equal(ModelRef.HumanLabel(parsed.CanonicalModelRef), doc.RootElement.GetProperty("model").GetString());
        }
    }

    [Fact]
    public void BuildStudioMetaJson_uses_provider_model_id_as_label()
    {
        var json = SendChatMessageHandler.BuildStudioMetaJson(
            usedAdvancedModel: false,
            advancedModelFallbackReason: SendChatMessageHandler.StudioAdvancedFallbackDisabled,
            ModelRef.Parse("openrouter:anthropic/claude-sonnet-4"));

        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("usedAdvancedModel").GetBoolean());
        Assert.Equal("disabled", doc.RootElement.GetProperty("advancedModelFallbackReason").GetString());
        Assert.Equal("anthropic/claude-sonnet-4", doc.RootElement.GetProperty("model").GetString());
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
