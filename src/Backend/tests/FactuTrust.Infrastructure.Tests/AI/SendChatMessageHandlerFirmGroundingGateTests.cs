using System.Runtime.CompilerServices;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.Commands;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.AI.Tools;
using FactuTrust.Domain.Entities.AI;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Services.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

/// <summary>
/// Lot 1.3 / Lot 6 — tests d'intégration « tour de chat firm complet » avec un LLM factice, couvrant
/// les 8 scénarios du plan v3 §7 (lignes 475-484) : raccourci pré-exécuté avant le contenu, secours
/// sur LLM muet, outils en erreur ⇒ repli honnête (+ variante Ok non exploitable), nom fabriqué
/// persisté tel quel (limite v1 figée), follow-up demandant de nouvelles données, chemin tenant
/// scope None inchangé, chemin Cursor dédié, budget de générations de bout en bout.
/// </summary>
public sealed class SendChatMessageHandlerFirmGroundingGateTests
{
    private sealed class FakeGateLease : IOllamaGateLease
    {
        public long WaitMilliseconds => 0;
        public void Dispose() { }
    }

    // ── 1. Raccourci pré-exécuté avant le contenu, sources non vides, ≤ 2 générations ───────────

    [Fact]
    public async Task Scenario1_Shortcut_pre_calls_overview_before_any_significant_content()
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
                // Le raccourci a déjà pré-exécuté overview ; le modèle synthétise (texte ≥ 80 chars).
                return StreamMeaningfulText();
            });

        var toolExecutor = new Mock<IAiToolExecutor>();
        toolExecutor.Setup(x => x.ExecuteAsync(
                FirmAgentTools.PortfolioOverview,
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<AiToolExecutionContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiToolResult.Ok("""{"dossiersActifs":3,"echeancesEnRetard":1,"montantEnRetard":2500}""", groundedData: true));

        var handler = BuildHandler(ollama, toolExecutor, ollamaSettings: DefaultFirmSettings());
        var events = await RunHandlerAsync(handler, "Où en est le portefeuille du cabinet aujourd'hui ?");

        // Le tool_call_start du raccourci arrive AVANT tout content significatif.
        var firstToolCallStart = events.FindIndex(e => e.Type == "tool_call_start" && e.ToolName == FirmAgentTools.PortfolioOverview);
        var firstContent = events.FindIndex(e => e.Type == "content" && !string.IsNullOrWhiteSpace(e.Content));
        Assert.True(firstToolCallStart >= 0, "Expected tool_call_start for get_firm_portfolio_overview.");
        Assert.True(firstContent < 0 || firstToolCallStart < firstContent,
            $"tool_call_start ({firstToolCallStart}) must precede first content ({firstContent}).");

        // Sources non vides.
        Assert.Contains(events, e => e.Type == "sources");

        // ≤ 2 générations LLM.
        Assert.True(streamCallCount <= 2, $"Expected ≤ 2 provider calls, got {streamCallCount}.");

        // Corps persisté (content_replace) ≠ prose pré-outil (il n'y en a pas — le raccourci est avant).
        var lastReplace = GetFinalAssistantContent(events);
        Assert.NotNull(lastReplace);
        Assert.False(string.IsNullOrWhiteSpace(lastReplace!));
        Assert.Contains("3", lastReplace!); // dossiersActifs = 3 dans les données

        Assert.Contains(events, e => e.Type == "done");
        Assert.DoesNotContain(events, e => e.Type == "error");

        toolExecutor.Verify(x => x.ExecuteAsync(
            FirmAgentTools.PortfolioOverview,
            It.IsAny<Dictionary<string, object?>>(),
            It.IsAny<AiToolExecutionContext>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── 2. LLM muet sur les outils ⇒ secours overview + synthèse ────────────────────────────────

    [Fact]
    public async Task Scenario2_Llm_mute_on_tools_triggers_rescue_overview_then_synthesis()
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
                // Call 1 (boucle) : muet (texte court < 80). Call 2 (synthèse de secours) : texte significatif.
                return streamCallCount == 1 ? StreamShortText() : StreamMeaningfulText();
            });

        var toolExecutor = new Mock<IAiToolExecutor>();
        toolExecutor.Setup(x => x.ExecuteAsync(
                FirmAgentTools.PortfolioOverview,
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<AiToolExecutionContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiToolResult.Ok("""{"dossiersActifs":5}""", groundedData: true));

        // Question qui déclenche l'heuristique (quel) mais PAS le raccourci : pas de pré-exécution,
        // donc 0 grounded reads après la boucle → le secours déclenche.
        var handler = BuildHandler(ollama, toolExecutor, ollamaSettings: DefaultFirmSettings());
        var events = await RunHandlerAsync(handler, "Quel est le bilan global du cabinet ?");

        // Le secours a pré-exécuté overview (tool_call_start après la boucle, dans le bloc de synthèse).
        Assert.Contains(events, e => e.Type == "tool_call_start" && e.ToolName == FirmAgentTools.PortfolioOverview);

        // La synthèse de secours a été lancée.
        Assert.Contains(events, e => e.Type == "phase" && e.Phase == "llm_forced_synthesis");

        // Le corps final est non vide (synthèse ancrée sur la lecture de secours).
        var lastReplace = GetFinalAssistantContent(events);
        Assert.NotNull(lastReplace);
        Assert.False(string.IsNullOrWhiteSpace(lastReplace!));

        Assert.Contains(events, e => e.Type == "done");
        Assert.DoesNotContain(events, e => e.Type == "error");

        // L'outil de secours a été appelé (par le bloc rescue, pas par le raccourci).
        toolExecutor.Verify(x => x.ExecuteAsync(
            FirmAgentTools.PortfolioOverview,
            It.IsAny<Dictionary<string, object?>>(),
            It.IsAny<AiToolExecutionContext>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── 3. Outils firm tous en erreur ⇒ repli honnête, jamais la prose initiale ─────────────────

    [Fact]
    public async Task Scenario3a_All_firm_tools_in_error_yields_honest_fallback_never_initial_prose()
    {
        var ollama = new Mock<IOllamaClient>();
        ollama.Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        ollama.Setup(x => x.IsModelInstalledAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        ollama.Setup(x => x.StreamChatAsync(
                It.IsAny<OllamaChatRequest>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>()))
            .Returns(StreamHallucinatedProse()); // Le modèle hallucine des noms (prose ≥ 80 chars).

        var toolExecutor = new Mock<IAiToolExecutor>();
        toolExecutor.Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<AiToolExecutionContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiToolResult.Error("Connexion base de données indisponible."));

        var handler = BuildHandler(ollama, toolExecutor, ollamaSettings: DefaultFirmSettings());
        var events = await RunHandlerAsync(handler, "Où en est le portefeuille du cabinet aujourd'hui ?");

        // Le repli honnête déterministe est persisté (content_replace final).
        var lastReplace = GetFinalAssistantContent(events);
        Assert.NotNull(lastReplace);
        Assert.Contains("pas pu consulter", lastReplace!);

        // La prose hallucinée initiale n'apparaît PAS dans le contenu final.
        Assert.DoesNotContain("Société Fictive", lastReplace!);

        Assert.Contains(events, e => e.Type == "done");
        Assert.DoesNotContain(events, e => e.Type == "error");

        // Le raccourci a tenté overview (échec), puis le secours aussi (échec) : 2 appels.
        toolExecutor.Verify(x => x.ExecuteAsync(
            FirmAgentTools.PortfolioOverview,
            It.IsAny<Dictionary<string, object?>>(),
            It.IsAny<AiToolExecutionContext>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Scenario3b_Ok_non_exploitable_fanout_failure_yields_honest_fallback()
    {
        var ollama = new Mock<IOllamaClient>();
        ollama.Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        ollama.Setup(x => x.IsModelInstalledAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        ollama.Setup(x => x.StreamChatAsync(
                It.IsAny<OllamaChatRequest>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>()))
            .Returns(StreamHallucinatedProse());

        // Ok mais non exploitable : fan-out totalement en échec (DossiersRead=0, DossiersFailed>0)
        // ⇒ GroundedData=false. Même traitement que l'erreur : repli honnête.
        var toolExecutor = new Mock<IAiToolExecutor>();
        toolExecutor.Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<AiToolExecutionContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiToolResult.Ok(
                """{"dossiersRead":0,"dossiersFailed":3,"lectureIncomplete":true}""",
                groundedData: false));

        var handler = BuildHandler(ollama, toolExecutor, ollamaSettings: DefaultFirmSettings());
        var events = await RunHandlerAsync(handler, "Où en est le portefeuille du cabinet aujourd'hui ?");

        var lastReplace = GetFinalAssistantContent(events);
        Assert.NotNull(lastReplace);
        Assert.Contains("pas pu consulter", lastReplace!);

        Assert.Contains(events, e => e.Type == "done");
        Assert.DoesNotContain(events, e => e.Type == "error");
    }

    // ── 4. LLM fabriquant un nom absent des résultats ⇒ prose ancrée persistée telle quelle ──────

    [Fact]
    public async Task Scenario4_Fabricated_name_in_anchored_prose_persisted_as_is_freezes_v1_limit()
    {
        // Test documentaire (plan §7 ligne 480) : en v1, il n'y a PAS de validation d'entités —
        // un nom absent des résultats d'outils peut théoriquement subsister. Le test fige cette
        // limite : la prose ancrée (grounded reads ≥ 1) est persistée telle quelle.
        var ollama = new Mock<IOllamaClient>();
        ollama.Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        ollama.Setup(x => x.IsModelInstalledAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        ollama.Setup(x => x.StreamChatAsync(
                It.IsAny<OllamaChatRequest>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>()))
            .Returns(StreamFabricatedNameProse()); // Nom absent des données d'outils.

        var toolExecutor = new Mock<IAiToolExecutor>();
        toolExecutor.Setup(x => x.ExecuteAsync(
                FirmAgentTools.PortfolioOverview,
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<AiToolExecutionContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiToolResult.Ok("""{"dossiersActifs":3,"echeancesEnRetard":0}""", groundedData: true));

        var handler = BuildHandler(ollama, toolExecutor, ollamaSettings: DefaultFirmSettings());
        var events = await RunHandlerAsync(handler, "Où en est le portefeuille du cabinet aujourd'hui ?");

        // La prose ancrée est persistée telle quelle (pas de validation d'entités en v1).
        var lastReplace = GetFinalAssistantContent(events);
        Assert.NotNull(lastReplace);
        Assert.Contains("Société Inventée", lastReplace!);

        Assert.Contains(events, e => e.Type == "done");
        Assert.DoesNotContain(events, e => e.Type == "error");
    }

    // ── 5. Follow-up (ConversationalFollowUp=true) demandant de nouvelles figures ⇒ gate appliqué ─

    [Fact]
    public async Task Scenario5_Follow_up_demanding_new_figures_applies_gate_and_executes_firm_read()
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
                return StreamMeaningfulText();
            });

        var toolExecutor = new Mock<IAiToolExecutor>();
        toolExecutor.Setup(x => x.ExecuteAsync(
                FirmAgentTools.PortfolioOverview,
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<AiToolExecutionContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiToolResult.Ok("""{"dossiersActifs":7,"echeancesEnRetard":2}""", groundedData: true));

        var handler = BuildHandler(ollama, toolExecutor, ollamaSettings: DefaultFirmSettings());
        var command = new SendChatMessageCommand(
            null,
            "Combien de dossiers ont une échéance dans les 7 prochains jours ?",
            Options: new ChatRequestOptionsDto
            {
                AgentScope = AssistantAgentScope.FirmMission,
                ConversationalFollowUp = true
            });

        var events = new List<ChatStreamEvent>();
        await foreach (var ev in handler.HandleAsync(command, Guid.NewGuid()))
            events.Add(ev);

        // Le raccourci firm a pré-exécuté overview (le follow-up demande de nouvelles données).
        Assert.Contains(events, e => e.Type == "tool_call_start" && e.ToolName == FirmAgentTools.PortfolioOverview);

        // Sources non vides (lecture firm exécutée).
        Assert.Contains(events, e => e.Type == "sources");

        // Réponse ancrée persistée.
        var lastReplace = GetFinalAssistantContent(events);
        Assert.NotNull(lastReplace);
        Assert.False(string.IsNullOrWhiteSpace(lastReplace!));

        Assert.Contains(events, e => e.Type == "done");
        Assert.DoesNotContain(events, e => e.Type == "error");

        toolExecutor.Verify(x => x.ExecuteAsync(
            FirmAgentTools.PortfolioOverview,
            It.IsAny<Dictionary<string, object?>>(),
            It.IsAny<AiToolExecutionContext>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── 6. Chemin tenant complet (scope None) : aucun pré-appel firm, raccourci ventes intact ────

    [Fact]
    public async Task Scenario6_Tenant_scope_none_no_firm_pre_call_sales_shortcut_intact()
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
                return StreamMeaningfulText();
            });

        var toolExecutor = new Mock<IAiToolExecutor>();
        toolExecutor.Setup(x => x.ExecuteAsync(
                "get_sales_revenue",
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<AiToolExecutionContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiToolResult.Ok("""{"revenue":12500}"""));

        // Scope None (pas FirmMission) : le raccourci firm ne se déclenche pas, le gate firm est inactif.
        var settings = new OllamaSettings
        {
            DefaultModel = "mistral",
            ConversationalFastPathEnabled = false,
            FirmMissionShortcutEnabled = true,
            FirmMissionGroundingGateEnabled = true
        };
        var handler = BuildHandler(ollama, toolExecutor, ollamaSettings: settings);
        var command = new SendChatMessageCommand(
            null,
            "Quel est mon CA aujourd'hui ?",
            Options: new ChatRequestOptionsDto { AgentScope = AssistantAgentScope.None });

        var events = new List<ChatStreamEvent>();
        await foreach (var ev in handler.HandleAsync(command, Guid.NewGuid()))
            events.Add(ev);

        // Aucun pré-appel firm.
        Assert.DoesNotContain(events, e => e.Type == "tool_call_start" && (e.ToolName ?? "").StartsWith("get_firm_", StringComparison.Ordinal));

        // Le raccourci ventes est intact (get_sales_revenue pré-exécuté).
        Assert.Contains(events, e => e.Type == "tool_call_start" && e.ToolName == "get_sales_revenue");

        // Aucune trace du gate firm (pas de repli honnête).
        var lastReplace = GetFinalAssistantContent(events);
        Assert.NotNull(lastReplace);
        Assert.DoesNotContain("pas pu consulter", lastReplace!);

        Assert.Contains(events, e => e.Type == "done");
        Assert.DoesNotContain(events, e => e.Type == "error");
    }

    // ── 7. Chemin Cursor dédié : erreur outil ⇒ gate ; lecture exploitable ⇒ réponse persistée ──

    [Fact]
    public async Task Scenario7a_Cursor_path_tool_error_gate_applied_honest_fallback()
    {
        var registry = new CursorToolRunRegistry();
        var toolExecutor = new Mock<IAiToolExecutor>();
        toolExecutor.Setup(x => x.ExecuteAsync(
                FirmAgentTools.PortfolioOverview,
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<AiToolExecutionContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiToolResult.Error("DB indisponible."));

        var cursor = new Mock<ICursorAgentClient>();
        cursor.Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        cursor.Setup(x => x.RunChatAsync(It.IsAny<CursorChatRunRequest>(), It.IsAny<CancellationToken>()))
            .Returns((CursorChatRunRequest request, CancellationToken ct) =>
                SimulateCursorFirmTool(registry, toolExecutor.Object, request, ct, FirmAgentTools.PortfolioOverview));

        var platform = new Mock<IPlatformAiSettingsService>();
        platform.Setup(x => x.GetDefaultModelRefAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("cursor:composer-2.5");
        platform.Setup(x => x.GetStudioAiModelRefAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        platform.Setup(x => x.GetCursorCredentialsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlatformCursorCredentials(true, "cursor_test_key"));

        var settings = new OllamaSettings
        {
            DefaultModel = "mistral",
            ConversationalFastPathEnabled = false,
            FirmMissionShortcutEnabled = true,
            FirmMissionGroundingGateEnabled = true
        };
        var handler = BuildHandlerWithCursor(cursor, registry, platform, toolExecutor, settings);
        var events = await RunHandlerAsync(handler, "Où en est le portefeuille du cabinet aujourd'hui ?");

        // Le repli honnête est persisté (outil en erreur ⇒ 0 grounded reads ⇒ gate).
        var lastReplace = GetFinalAssistantContent(events);
        Assert.NotNull(lastReplace);
        Assert.Contains("pas pu consulter", lastReplace!);

        Assert.Contains(events, e => e.Type == "done");
        Assert.DoesNotContain(events, e => e.Type == "error");
    }

    [Fact]
    public async Task Scenario7b_Cursor_path_exploitable_read_counter_incremented_response_persisted()
    {
        var registry = new CursorToolRunRegistry();
        var toolExecutor = new Mock<IAiToolExecutor>();
        toolExecutor.Setup(x => x.ExecuteAsync(
                FirmAgentTools.PortfolioOverview,
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<AiToolExecutionContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiToolResult.Ok("""{"dossiersActifs":4,"echeancesEnRetard":1}""", groundedData: true));

        var cursor = new Mock<ICursorAgentClient>();
        cursor.Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        cursor.Setup(x => x.RunChatAsync(It.IsAny<CursorChatRunRequest>(), It.IsAny<CancellationToken>()))
            .Returns((CursorChatRunRequest request, CancellationToken ct) =>
                SimulateCursorFirmTool(registry, toolExecutor.Object, request, ct, FirmAgentTools.PortfolioOverview));

        var platform = new Mock<IPlatformAiSettingsService>();
        platform.Setup(x => x.GetDefaultModelRefAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("cursor:composer-2.5");
        platform.Setup(x => x.GetStudioAiModelRefAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        platform.Setup(x => x.GetCursorCredentialsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlatformCursorCredentials(true, "cursor_test_key"));

        var settings = new OllamaSettings
        {
            DefaultModel = "mistral",
            ConversationalFastPathEnabled = false,
            FirmMissionShortcutEnabled = true,
            FirmMissionGroundingGateEnabled = true
        };
        var handler = BuildHandlerWithCursor(cursor, registry, platform, toolExecutor, settings);
        var events = await RunHandlerAsync(handler, "Où en est le portefeuille du cabinet aujourd'hui ?");

        // La réponse ancrée est persistée (lecture exploitable ⇒ ctx.FirmGroundedReads ≥ 1).
        var lastReplace = GetFinalAssistantContent(events);
        Assert.NotNull(lastReplace);
        Assert.False(string.IsNullOrWhiteSpace(lastReplace!));
        Assert.DoesNotContain("pas pu consulter", lastReplace!);

        Assert.Contains(events, e => e.Type == "done");
        Assert.DoesNotContain(events, e => e.Type == "error");
    }

    // ── 8. Budget de générations de bout en bout (gate actif) ────────────────────────────────────

    [Fact]
    public async Task Scenario8_End_to_end_budget_tool_at_round1_short_text_at_round2_only_2_calls()
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
                // Call 1 : le raccourci a déjà pré-exécuté overview ; le modèle appelle dossier_health.
                // Call 2 (round 2) : texte final < 80 chars. Call 3+ ne doit jamais arriver (budget ≤ 2).
                return streamCallCount == 1 ? StreamFirmToolCall() : StreamShortText();
            });

        var toolExecutor = new Mock<IAiToolExecutor>();
        toolExecutor.Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<AiToolExecutionContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string toolName, Dictionary<string, object?> _, AiToolExecutionContext _, CancellationToken _) =>
                AiToolResult.Ok($"{{\"tool\":\"{toolName}\"}}", groundedData: true));

        var handler = BuildHandler(ollama, toolExecutor, ollamaSettings: DefaultFirmSettings());
        var events = await RunHandlerAsync(handler, "Où en est le portefeuille du cabinet aujourd'hui ?");

        // Le provider n'est appelé que 2 fois (round outils + round rédaction) ; le budget firm ≤ 2
        // bloque la synthèse forcée qui aurait été une 3ᵉ génération.
        Assert.True(streamCallCount == 2, $"Expected exactly 2 provider calls, got {streamCallCount}.");

        // Aucune synthèse forcée lancée (budget épuisé).
        Assert.DoesNotContain(events, e => e.Type == "phase" && e.Phase == "llm_forced_synthesis");

        // Le repli déterministe a pris le relais (content non vide).
        var lastReplace = GetFinalAssistantContent(events);
        Assert.NotNull(lastReplace);
        Assert.False(string.IsNullOrWhiteSpace(lastReplace!));

        Assert.Contains(events, e => e.Type == "done");
        Assert.DoesNotContain(events, e => e.Type == "error");
    }

    // ── Helpers partagés ────────────────────────────────────────────────────────────────────────

    private static OllamaSettings DefaultFirmSettings() => new()
    {
        DefaultModel = "mistral",
        ConversationalFastPathEnabled = false,
        FirmMissionShortcutEnabled = true,
        FirmMissionGroundingGateEnabled = true
    };

    private static SendChatMessageHandler BuildHandler(
        Mock<IOllamaClient> ollama,
        Mock<IAiToolExecutor> toolExecutor,
        OllamaSettings ollamaSettings)
    {
        var gate = new Mock<IOllamaGenerationGate>();
        gate.Setup(x => x.AcquireAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new FakeGateLease());

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

        return new SendChatMessageHandler(
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
            Options.Create(ollamaSettings),
            Options.Create(new ScreenAnalysisOptions()),
            Options.Create(new CursorSdkSettings { Enabled = false }));
    }

    private static SendChatMessageHandler BuildHandlerWithCursor(
        Mock<ICursorAgentClient> cursor,
        CursorToolRunRegistry registry,
        Mock<IPlatformAiSettingsService> platform,
        Mock<IAiToolExecutor> toolExecutor,
        OllamaSettings ollamaSettings)
    {
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

        return new SendChatMessageHandler(
            Mock.Of<IOllamaClient>(),
            Mock.Of<IOllamaGenerationGate>(),
            Mock.Of<IOpenAiChatCompletionsClient>(),
            cursor.Object,
            registry,
            tenant.Object,
            platform.Object,
            Mock.Of<IModalCredentialsResolver>(),
            Mock.Of<IOllamaInferenceProfileResolver>(),
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
            Options.Create(ollamaSettings),
            Options.Create(new ScreenAnalysisOptions()),
            Options.Create(new CursorSdkSettings { Enabled = true, ToolCallbackBaseUrl = "http://127.0.0.1:7000" }));
    }

    private static async Task<List<ChatStreamEvent>> RunHandlerAsync(
        SendChatMessageHandler handler, string message)
    {
        var command = new SendChatMessageCommand(
            null,
            message,
            Options: new ChatRequestOptionsDto { AgentScope = AssistantAgentScope.FirmMission });

        var events = new List<ChatStreamEvent>();
        await foreach (var ev in handler.HandleAsync(command, Guid.NewGuid()))
            events.Add(ev);
        return events;
    }

    /// <summary>
    /// Retourne le contenu assistant final visible : le dernier <c>content_replace</c> non vide
    /// (qui écrase tout le contenu précédent) sinon le dernier <c>content</c> non vide (chunk non
    /// streamé quand le texte est &lt; 160 caractères et <c>liveStreamedAny</c> reste faux).
    /// </summary>
    private static string? GetFinalAssistantContent(List<ChatStreamEvent> events)
    {
        string? lastContent = null;
        foreach (var e in events)
        {
            if ((e.Type == "content_replace" || e.Type == "content")
                && !string.IsNullOrWhiteSpace(e.Content))
            {
                lastContent = e.Content;
            }
        }
        return lastContent;
    }

    // ── Streams LLM factices ────────────────────────────────────────────────────────────────────

    private static async IAsyncEnumerable<OllamaChatChunk> StreamMeaningfulText(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        yield return new OllamaChatChunk
        {
            Message = new OllamaChatMessage
            {
                Role = "assistant",
                Content = "Le portefeuille du cabinet compte 3 dossiers actifs avec 1 échéance en retard pour un montant de 2 500 TND."
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

    private static async IAsyncEnumerable<OllamaChatChunk> StreamFirmToolCall(
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
                            Name = FirmAgentTools.DossierHealth,
                            Arguments = new Dictionary<string, object?>()
                        }
                    }
                }
            },
            Done = true
        };
    }

    private static async IAsyncEnumerable<OllamaChatChunk> StreamHallucinatedProse(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        yield return new OllamaChatChunk
        {
            Message = new OllamaChatMessage
            {
                Role = "assistant",
                Content = "Le portefeuille contient 40 dossiers actifs. La Société Fictive a une échéance importante cette semaine."
            },
            Done = true
        };
    }

    private static async IAsyncEnumerable<OllamaChatChunk> StreamFabricatedNameProse(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        yield return new OllamaChatChunk
        {
            Message = new OllamaChatMessage
            {
                Role = "assistant",
                Content = "Le portefeuille compte 3 dossiers actifs. Le dossier Société Inventée semble prioritaire avec un retard important."
            },
            Done = true
        };
    }

    // ── Simulation d'un appel d'outil firm via le callback Cursor ────────────────────────────────

    private static async IAsyncEnumerable<CursorAgentStreamEvent> SimulateCursorFirmTool(
        ICursorToolRunRegistry registry,
        IAiToolExecutor executor,
        CursorChatRunRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken,
        string toolName)
    {
        await Task.Yield();
        if (registry.TryGet(request.RunId, out var ctx))
        {
            ctx.ExtraEvents.Enqueue(ChatStreamEvent.ToolCallStart(toolName, "cursor-c1"));
            var result = await executor.ExecuteAsync(
                toolName,
                new Dictionary<string, object?>(),
                new AiToolExecutionContext(ctx.CorrelationId, ctx.Conversation.Id),
                cancellationToken);
            ctx.ToolsExecuted++;
            ctx.ToolSources.Add((toolName, "cursor-c1"));
            // Lot 1.3 : incrémente ctx.FirmGroundedReads uniquement si la lecture firm est réussie
            // ET exploitable — mirror de CursorToolCallbackService.
            if (result.Success && result.GroundedData)
                ctx.FirmGroundedReads++;
            ctx.ExtraEvents.Enqueue(ChatStreamEvent.ToolCallEnd(toolName, "cursor-c1", 1));
        }

        // Le Cursor stream produit du texte (le modèle synthétise à partir des résultats d'outils).
        yield return new CursorAgentStreamEvent(
            "assistant_text",
            Text: "Le portefeuille du cabinet compte 4 dossiers actifs avec 1 échéance en retard.");
        yield return new CursorAgentStreamEvent("done", Status: "finished");
    }
}
