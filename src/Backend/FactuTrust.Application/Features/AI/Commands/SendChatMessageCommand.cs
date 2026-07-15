using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.AI.Tools;
using FactuTrust.Application.Features.Studio.Ai;
using FactuTrust.Domain.Entities.AI;
using FactuTrust.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.AI.Commands;

public sealed record SendChatMessageCommand(
    Guid? ConversationId,
    string Message,
    // Déprécié — ignoré : le modèle est résolu depuis la configuration du tenant (back-office).
    string? Model = null,
    ChatUiContextDto? UiContext = null,
    ChatRequestOptionsDto? Options = null,
    IReadOnlyList<ChatAttachmentInput>? Attachments = null);

public sealed class SendChatMessageHandler
{
    private readonly IOllamaClient _ollamaClient;
    private readonly IOllamaGenerationGate _ollamaGenerationGate;
    private readonly IOpenAiChatCompletionsClient _openAiClient;
    private readonly ITenantAiProviderRepository _tenantAiProviderRepository;
    private readonly IPlatformAiSettingsService _platformAiSettings;
    private readonly IOllamaInferenceProfileResolver _inferenceProfileResolver;
    private readonly IAiToolExecutor _toolExecutor;
    private readonly IAiToolExecutorScopeFactory _toolExecutorScopeFactory;
    private readonly IAiContextBuilder _contextBuilder;
    private readonly AiVolatileContextFormatter _volatileContextFormatter;
    private readonly AiScreenAnalysisEnricher _screenAnalysisEnricher;
    private readonly IConversationRepository _conversationRepository;
    private readonly ILogger<SendChatMessageHandler> _logger;
    private readonly OllamaSettings _ollamaSettings;
    private readonly OpenRouterSettings _openRouterSettings;
    private readonly ScreenAnalysisOptions _screenAnalysisOptions;

    private static readonly JsonSerializerOptions SourcesJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SendChatMessageHandler(
        IOllamaClient ollamaClient,
        IOllamaGenerationGate ollamaGenerationGate,
        IOpenAiChatCompletionsClient openAiClient,
        ITenantAiProviderRepository tenantAiProviderRepository,
        IPlatformAiSettingsService platformAiSettings,
        IOllamaInferenceProfileResolver inferenceProfileResolver,
        IAiToolExecutor toolExecutor,
        IAiToolExecutorScopeFactory toolExecutorScopeFactory,
        IAiContextBuilder contextBuilder,
        AiVolatileContextFormatter volatileContextFormatter,
        AiScreenAnalysisEnricher screenAnalysisEnricher,
        IConversationRepository conversationRepository,
        ILogger<SendChatMessageHandler> logger,
        IOptions<OllamaSettings> ollamaSettings,
        IOptions<OpenRouterSettings> openRouterSettings,
        IOptions<ScreenAnalysisOptions> screenAnalysisOptions)
    {
        _ollamaClient = ollamaClient;
        _ollamaGenerationGate = ollamaGenerationGate;
        _openAiClient = openAiClient;
        _tenantAiProviderRepository = tenantAiProviderRepository;
        _platformAiSettings = platformAiSettings;
        _inferenceProfileResolver = inferenceProfileResolver;
        _toolExecutor = toolExecutor;
        _toolExecutorScopeFactory = toolExecutorScopeFactory;
        _contextBuilder = contextBuilder;
        _volatileContextFormatter = volatileContextFormatter;
        _screenAnalysisEnricher = screenAnalysisEnricher;
        _conversationRepository = conversationRepository;
        _logger = logger;
        _ollamaSettings = ollamaSettings.Value;
        _openRouterSettings = openRouterSettings.Value;
        _screenAnalysisOptions = screenAnalysisOptions.Value;
    }

    public async IAsyncEnumerable<ChatStreamEvent> HandleAsync(
        SendChatMessageCommand command,
        Guid userId,
        string? correlationId = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var total = Stopwatch.StartNew();

        void LogPhase(string phase, long elapsedMs) =>
            _logger.LogInformation(
                "AI chat {CorrelationId} phase={Phase} elapsed_ms={ElapsedMs}",
                correlationId ?? "-", phase, elapsedMs);

        var sw = Stopwatch.StartNew();

        var defaultModel = string.IsNullOrWhiteSpace(_ollamaSettings.DefaultModel)
            ? "mistral"
            : _ollamaSettings.DefaultModel.Trim();

        var assistantMode = AssistantModeResolver.Resolve(command);
        var screenId = AiScreenAnalysisModeDetector.ResolveScreenId(command.UiContext);
        // Scope effectif (prompt + catalogue) : uniquement en mode Default et si le kill-switch est actif.
        var agentScope = AssistantModeResolver.ResolveAgentScope(command, assistantMode, _ollamaSettings.EnableAgentScopes);
        // Scope demandé par le client : stampé sur la conversation à sa création (classement par historique),
        // même quand le mode courant (ex. ScreenAnalysis) ignore le scope pour le prompt et les outils.
        var requestedAgentScope = !_ollamaSettings.EnableAgentScopes
            ? AssistantAgentScope.None
            : command.Options?.AgentScope is { } reqScope && Enum.IsDefined(reqScope)
                ? reqScope
                : AssistantAgentScope.None;

        // Pré-chargement parallèle : modèle actif, historique conversation, prompt système (cache mémoire).
        var configuredModelTask = _platformAiSettings.GetDefaultModelRefAsync(cancellationToken);
        var systemPromptTask = _contextBuilder.BuildSystemPromptAsync(assistantMode, screenId, agentScope, cancellationToken);

        Task<Conversation?>? conversationLoadTask = null;
        if (command.ConversationId.HasValue)
        {
            var loadLimit = _ollamaSettings.LoadPartialConversationMessages
                ? Math.Max(1, _ollamaSettings.MaxContextMessages + Math.Max(0, _ollamaSettings.ConversationLoadMessageSlack))
                : 0;
            conversationLoadTask = loadLimit > 0
                ? _conversationRepository.GetByIdForChatAsync(command.ConversationId.Value, loadLimit, cancellationToken)
                : _conversationRepository.GetByIdAsync(command.ConversationId.Value, cancellationToken);
        }

        await Task.WhenAll(
            configuredModelTask,
            systemPromptTask,
            conversationLoadTask ?? Task.FromResult<Conversation?>(null));

        Conversation? existing = conversationLoadTask is not null ? await conversationLoadTask : null;
        if (command.ConversationId.HasValue && existing is null)
            throw new KeyNotFoundException($"Conversation {command.ConversationId} introuvable.");

        // Le modèle est imposé par la configuration globale de la plateforme (back-office) ;
        // le modèle éventuellement transmis par le client et celui de la conversation sont ignorés.
        var configuredModel = await configuredModelTask;
        var rawModel = string.IsNullOrWhiteSpace(configuredModel) ? defaultModel : configuredModel;
        var modelRef = ModelRef.Parse(rawModel);
        if (string.IsNullOrEmpty(modelRef.CanonicalModelRef))
            modelRef = ModelRef.Parse($"{ModelRef.OllamaPrefix}{defaultModel}");

        sw.Restart();
        yield return ChatStreamEvent.PhaseEvent(
            "provider_availability",
            "running",
            detail: modelRef.CanonicalModelRef);
        if (modelRef.Kind == LlmProviderKind.Ollama)
        {
            if (!await _ollamaClient.IsAvailableAsync(cancellationToken))
            {
                LogPhase("ollama_availability", sw.ElapsedMilliseconds);
                yield return ChatStreamEvent.PhaseEvent(
                    "provider_availability",
                    "failed",
                    sw.ElapsedMilliseconds,
                    detail: modelRef.CanonicalModelRef);
                yield return ChatStreamEvent.ErrorEvent(
                    "Le service IA local (Ollama) est indisponible. Démarrez Ollama ou choisissez un modèle cloud.");
                yield break;
            }

            if (string.IsNullOrWhiteSpace(modelRef.ProviderModelId) ||
                !await _ollamaClient.IsModelInstalledAsync(modelRef.ProviderModelId, cancellationToken))
            {
                LogPhase("model_installed_check", sw.ElapsedMilliseconds);
                yield return ChatStreamEvent.PhaseEvent(
                    "provider_availability",
                    "failed",
                    sw.ElapsedMilliseconds,
                    detail: modelRef.CanonicalModelRef);
                var shortName = (modelRef.ProviderModelId ?? "").Split(':')[0];
                yield return ChatStreamEvent.ErrorEvent(
                    $"Le modèle « {modelRef.ProviderModelId} » n'est pas installé dans Ollama. Exécutez « ollama pull {shortName} » ou choisissez un autre modèle.");
                yield break;
            }
        }
        else
        {
            var apiKey = await _tenantAiProviderRepository.GetDecryptedApiKeyForOpenRouterAsync(cancellationToken);
            if (string.IsNullOrEmpty(apiKey))
            {
                LogPhase("openrouter_credentials", sw.ElapsedMilliseconds);
                yield return ChatStreamEvent.PhaseEvent(
                    "provider_availability",
                    "failed",
                    sw.ElapsedMilliseconds,
                    detail: modelRef.CanonicalModelRef);
                yield return ChatStreamEvent.ErrorEvent(
                    "Aucune clé API OpenRouter configurée pour cet espace. Configurez-la dans Paramètres > Fournisseurs IA.");
                yield break;
            }
        }

        LogPhase("provider_availability", sw.ElapsedMilliseconds);
        yield return ChatStreamEvent.PhaseEvent(
            "provider_availability",
            "completed",
            sw.ElapsedMilliseconds,
            detail: modelRef.CanonicalModelRef);

        sw.Restart();
        yield return ChatStreamEvent.PhaseEvent(
            "conversation_load_or_create",
            "running",
            detail: existing is not null ? "conversation existante" : "nouvelle conversation");
        var isNewConversation = existing is null;
        var deferUserPersist = _ollamaSettings.DeferUserMessagePersist;
        Conversation conversation;
        if (existing is not null)
        {
            conversation = existing;
            // Le scope stocké fait autorité pour les outils/prompt d'une conversation reprise : les listes UI
            // sont filtrées par scope, un écart n'est atteignable que par appel API forgé.
            var storedScope = Enum.IsDefined(typeof(AssistantAgentScope), existing.AgentScope)
                ? (AssistantAgentScope)existing.AgentScope
                : AssistantAgentScope.None;
            if (assistantMode == AssistantMode.Default && _ollamaSettings.EnableAgentScopes && storedScope != agentScope)
            {
                _logger.LogWarning(
                    "AI chat {CorrelationId} agent scope mismatch: request={RequestedScope} stored={StoredScope} — stored wins",
                    correlationId ?? "-", agentScope, storedScope);
                agentScope = storedScope;
            }
            conversation.AddMessage(MessageRole.User, command.Message);
            if (!deferUserPersist)
                await _conversationRepository.UpdateAsync(conversation, cancellationToken);
        }
        else
        {
            var title = BuildConversationTitle(command);
            conversation = Conversation.Create(userId, title, modelRef.CanonicalModelRef, (int)requestedAgentScope);
            conversation.AddMessage(MessageRole.User, command.Message);
            if (!deferUserPersist)
                await _conversationRepository.AddAsync(conversation, cancellationToken);
        }

        LogPhase("conversation_load_or_create", sw.ElapsedMilliseconds);
        yield return ChatStreamEvent.PhaseEvent(
            "conversation_load_or_create",
            "completed",
            sw.ElapsedMilliseconds,
            detail: existing is not null ? "conversation existante" : "nouvelle conversation");
        sw.Restart();

        var isScreenAnalysis = assistantMode == AssistantMode.ScreenAnalysis;

        yield return ChatStreamEvent.PhaseEvent(
            "build_system_prompt",
            "running",
            detail: assistantMode.ToString());

        string? serverEnrichment = null;
        if (isScreenAnalysis && _screenAnalysisOptions.Enabled)
        {
            serverEnrichment = await _screenAnalysisEnricher.TryEnrichAsync(
                command.UiContext,
                correlationId,
                conversation.Id,
                cancellationToken);
        }

        var systemPrompt = await systemPromptTask;
        var volatileUi = _volatileContextFormatter.Format(command.UiContext, serverEnrichment);
        if (!string.IsNullOrEmpty(volatileUi))
            systemPrompt += "\n\n" + volatileUi;

        if (isScreenAnalysis)
        {
            _logger.LogInformation(
                "Screen analysis {CorrelationId} screen={ScreenId} payload_bytes={PayloadBytes} schema={Schema}",
                correlationId ?? "-",
                screenId ?? "-",
                command.UiContext?.AnalysisSummary?.Length ?? 0,
                TryExtractSchemaVersion(command.UiContext?.AnalysisSummary));
        }

        LogPhase("build_system_prompt", sw.ElapsedMilliseconds);
        yield return ChatStreamEvent.PhaseEvent(
            "build_system_prompt",
            "completed",
            sw.ElapsedMilliseconds,
            detail: assistantMode.ToString());

        // Routage par intention : n'exposer les outils d'ÉCRITURE que si la requête — ou l'échange récent,
        // pour couvrir les confirmations type « oui » après une demande de mutation — implique une mutation.
        // Cela réduit fortement la taille du prompt (et le prefill) pour les questions de LECTURE (cas dominant),
        // et évite de tronquer le catalogue d'outils. L'exécution reste de toute façon gardée par
        // EnableMutationTools + permissions (AiToolExecutor), donc aucun risque d'action non désirée.
        var mutationIntentText = command.Message ?? string.Empty;
        if (conversation.Messages.Count > 0)
        {
            var recent = conversation.Messages
                .OrderByDescending(m => m.SortOrder)
                .Take(3)
                .Select(m => m.Content);
            mutationIntentText += " " + string.Join(" ", recent);
        }
        // Canal externe (WhatsApp…) : ForceReadOnlyTools impose la lecture seule structurelle pour
        // cette requête, indépendamment du gating dynamique (défaut false = chemin web inchangé).
        var effectiveMutationTools = command.Options?.ForceReadOnlyTools != true
            && AssistantModeResolver.ShouldEnableMutationTools(
                assistantMode, _ollamaSettings.EnableMutationTools, mutationIntentText);
        var toolIntent = _ollamaSettings.EnableToolIntentRouting
            ? AiToolIntentRouter.Resolve(command.Message, assistantMode)
            : AiToolIntentRouter.AiToolIntent.Fallback;
        // Suivi conversationnel (clic sur un prompt suggéré) en mode Default : on restreint le catalogue
        // à l'intent Synthesis pour répondre depuis l'analyse déjà présente dans l'historique, au lieu de
        // dériver vers des outils hors-contexte. N'affecte ni ScreenAnalysis/Compliance, ni les questions tapées.
        if (command.Options?.ConversationalFollowUp == true
            && assistantMode == AssistantMode.Default
            && !isScreenAnalysis)
        {
            toolIntent = AiToolIntentRouter.AiToolIntent.Synthesis;
        }
        OllamaInferenceProfile? inferenceProfile = null;
        if (modelRef.Kind == LlmProviderKind.Ollama)
            inferenceProfile = await _inferenceProfileResolver.ResolveForPlatformAsync(cancellationToken);
        var tools = BuildOllamaTools(assistantMode, effectiveMutationTools, toolIntent, inferenceProfile, agentScope);
        // Les schémas d'outils sont injectés dans le contexte du modèle : on les compte dans
        // l'estimation de taille pour dimensionner num_ctx (sinon Ollama tronque silencieusement
        // l'invite quand de nombreux outils sont exposés → réponses dégradées / hors-sujet).
        var toolsApproxChars = tools.Count > 0 ? JsonSerializer.Serialize(tools).Length : 0;
        var toolSourcesForClient = new List<SourceEntryDto>();
        var accumulatedSuggestedPrompts = new List<string>();
        string? accumulatedDashboardJson = null;
        var temperature = isScreenAnalysis ? _screenAnalysisOptions.Temperature : _ollamaSettings.Temperature;
        var maxTokens = isScreenAnalysis ? _screenAnalysisOptions.MaxTokens : _ollamaSettings.MaxTokens;
        var maxRounds = ResolveMaxToolCallRounds(
            isScreenAnalysis,
            _screenAnalysisOptions.MaxToolCallRounds,
            _ollamaSettings.MaxToolCallRounds,
            _ollamaSettings.CpuMaxToolCallRounds,
            assistantMode,
            toolIntent,
            inferenceProfile) + 1;
        var toolCallRound = 0;
        var continueLoop = true;
        var minMeaningfulTextChars = ResolveMinMeaningfulTextChars(toolIntent, _ollamaSettings);
        // Raccourci déterministe CONFORMITÉ : « vérifie la conformité de ma dernière facture » résout
        // l'intent Sales (mot « facture ») et exigerait d'enchaîner recherche + contrôle — impossible
        // pour le petit modèle en un round CPU. On détecte la demande et on pré-appelle l'outil de
        // conformité (qui résout lui-même la facture : numéro extrait ou plus récente). Gate : modes
        // Default/Compliance uniquement, flag réversible, et outil présent dans le catalogue du scope.
        var complianceShortcutAllowed = assistantMode == AssistantMode.Compliance
            || (assistantMode == AssistantMode.Default
                && (agentScope == AssistantAgentScope.None
                    || AiAgentScopeCatalog.GetToolNames(agentScope).Contains("compliance_check_invoice")));
        var complianceDetection = !isScreenAnalysis
            && assistantMode is AssistantMode.Default or AssistantMode.Compliance
            && complianceShortcutAllowed
            && _ollamaSettings.ComplianceCheckShortcutEnabled
            ? AiToolIntentRouter.TryInferComplianceCheckInvoice(command.Message)
            : null;
        // Assistant expert : les suffixes Sales/Fallback citent get_sales_revenue — on ne les applique
        // que si l'outil fait partie du catalogue du scope (sinon ils pousseraient le modèle vers un
        // outil qu'il n'a pas). Les autres suffixes (Greeting/Chart/Synthesis) restent génériques.
        // Raccourci conformité armé : le suffixe d'intent (Sales) égarerait le modèle vers le CA —
        // remplacé par la consigne de synthèse du contrôle déjà exécuté.
        var intentSuffix = complianceDetection is null && ShouldApplyIntentSuffixForScope(toolIntent, agentScope)
            ? AiToolIntentRouter.BuildIntentPromptSuffix(toolIntent)
            : string.Empty;
        if (!string.IsNullOrWhiteSpace(intentSuffix))
            systemPrompt += "\n\n" + intentSuffix;
        if (complianceDetection is not null)
            systemPrompt += "\n\n" + AiToolIntentRouter.ComplianceShortcutPromptSuffix;
        // Question de conseil (« comment gagner plus », « vos conseils pour… ») : cadrage cumulable avec
        // l'intent — recommandations ANCRÉES dans les données, jamais de méta-discours sur les outils.
        if (!isScreenAnalysis && AiToolIntentRouter.LooksLikeAdviceQuestion(command.Message))
            systemPrompt += "\n\n" + AiToolIntentRouter.AdvicePromptSuffix;
        // Suivi pour le filet de synthèse finale : le dernier tour d'appels d'outils a-t-il produit une réponse visible complète ?
        var lastToolRoundHadMeaningfulText = false;
        var forcedSynthesisTriggered = false;
        var deterministicFallbackUsed = false;
        var meaningfulResponseDelivered = false;
        // StudioBuilder : message d'erreur réel d'un outil studio_generate_* en échec, à surfacer
        // déterministiquement (le petit modèle le paraphrase sinon en excuse vague).
        string? studioBuilderToolError = null;
        var conversationalFastPath = false;
        var toolsExecutedThisRequest = 0;
        var totalContentCharsStreamed = 0;
        var contentCharsPersisted = 0;

        if (_ollamaSettings.ConversationalFastPathEnabled
            && !isScreenAnalysis
            && AiToolIntentRouter.ShouldUseConversationalFastPath(toolIntent, command.Message))
        {
            conversationalFastPath = true;
            yield return ChatStreamEvent.PhaseEvent("conversational_fast_path", "running");
            var fastBody = AssistantDeterministicFallback.LooksLikeIdentityQuestion(command.Message)
                ? AssistantDeterministicFallback.BuildIdentityResponse()
                : AssistantDeterministicFallback.BuildGreetingResponse();
            contentCharsPersisted = fastBody.Length;
            totalContentCharsStreamed = fastBody.Length;
            conversation.AddMessage(MessageRole.Assistant, fastBody);
            meaningfulResponseDelivered = true;
            yield return ChatStreamEvent.ContentReplace(fastBody);
            yield return ChatStreamEvent.PhaseEvent("conversational_fast_path", "completed");
        }

        // Raccourcis déterministes : pour une question de CA non ambiguë (« aujourd'hui » / « ce mois-ci »)
        // ou un CLASSEMENT clients (« mes 5 meilleurs clients »), on pré-appelle get_sales_revenue avec les
        // arguments déduits afin de garantir un résultat d'outil dans le contexte. Un petit modèle ne renvoie
        // alors plus une clarification au lieu du chiffre, ni ne choisit un mauvais outil pour le classement.
        // Assistant expert : le raccourci ne s'exécute que si get_sales_revenue appartient au catalogue
        // du scope (ex. Expert Stock interrogé sur les « meilleurs clients » → pas de pré-appel, le
        // modèle répond/oriente avec sa persona).
        var salesShortcutAllowedForScope = agentScope == AssistantAgentScope.None
            || AiAgentScopeCatalog.GetToolNames(agentScope).Contains("get_sales_revenue");
        // Exclusion mutuelle : une demande de conformité détectée (plus spécifique) prime sur les
        // raccourcis ventes — « la facture FAC-… est-elle conforme ce mois-ci ? » ne doit pas
        // déclencher get_sales_revenue(preset=current_month).
        var salesShortcutPreset = !conversationalFastPath
            && !isScreenAnalysis
            && complianceDetection is null
            && salesShortcutAllowedForScope
            && toolIntent == AiToolIntentRouter.AiToolIntent.Sales
            ? AiToolIntentRouter.InferSalesPreset(command.Message)
            : null;
        var clientRankingTopN = !conversationalFastPath
            && !isScreenAnalysis
            && complianceDetection is null
            && salesShortcutAllowedForScope
            && toolIntent == AiToolIntentRouter.AiToolIntent.Sales
            && _ollamaSettings.SalesClientRankingShortcutEnabled
            ? AiToolIntentRouter.TryInferClientRankingTopN(command.Message)
            : null;

        Dictionary<string, object?>? salesShortcutArgs = null;
        if (clientRankingTopN is int rankingTopN)
        {
            // Classement clients : group_by=Client + top_n ; période explicite si détectée,
            // sinon last_30_days (décision produit — le mois en cours est dégénéré en début de mois).
            salesShortcutArgs = new Dictionary<string, object?>
            {
                ["preset"] = salesShortcutPreset ?? ReportingPeriodResolver.PresetLast30Days,
                ["group_by"] = "Client",
                ["top_n"] = rankingTopN
            };
        }
        else if ((salesShortcutPreset == "today" && _ollamaSettings.SalesTodayPresetShortcutEnabled)
            || (salesShortcutPreset == "current_month" && _ollamaSettings.SalesMonthPresetShortcutEnabled))
        {
            salesShortcutArgs = new Dictionary<string, object?> { ["preset"] = salesShortcutPreset! };
        }

        if (salesShortcutArgs is not null)
        {
            var shortcutCallId = Guid.NewGuid().ToString("N")[..12];
            yield return ChatStreamEvent.ToolCallStart("get_sales_revenue", shortcutCallId);
            var shortcutSw = Stopwatch.StartNew();
            AiToolResult shortcutResult;
            try
            {
                shortcutResult = await _toolExecutor.ExecuteAsync(
                    "get_sales_revenue",
                    salesShortcutArgs,
                    new AiToolExecutionContext(correlationId, conversation.Id),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Sales shortcut failed (args={Args})",
                    JsonSerializer.Serialize(salesShortcutArgs));
                shortcutResult = AiToolResult.Error($"Erreur lors de l'exécution: {ex.Message}");
            }

            var shortcutContent = shortcutResult.Success
                ? shortcutResult.Data ?? string.Empty
                : $"Erreur: {shortcutResult.ErrorMessage}";
            conversation.AddMessage(MessageRole.Tool, shortcutContent, "get_sales_revenue", shortcutCallId);
            toolsExecutedThisRequest++;
            toolSourcesForClient.Add(new SourceEntryDto("get_sales_revenue", shortcutCallId));
            yield return ChatStreamEvent.ToolCallEnd("get_sales_revenue", shortcutCallId, shortcutSw.ElapsedMilliseconds);
        }

        // Pré-exécution du contrôle de conformité (même patron que le raccourci ventes ci-dessus) :
        // le résultat structuré est injecté dans le contexte, le modèle n'a plus qu'à synthétiser.
        if (complianceDetection is not null && !conversationalFastPath)
        {
            var complianceArgs = new Dictionary<string, object?>();
            if (complianceDetection.InvoiceNumber is not null)
                complianceArgs["invoice_number"] = complianceDetection.InvoiceNumber;
            var complianceCallId = Guid.NewGuid().ToString("N")[..12];
            yield return ChatStreamEvent.ToolCallStart("compliance_check_invoice", complianceCallId);
            var complianceSw = Stopwatch.StartNew();
            AiToolResult complianceResult;
            try
            {
                complianceResult = await _toolExecutor.ExecuteAsync(
                    "compliance_check_invoice",
                    complianceArgs,
                    new AiToolExecutionContext(correlationId, conversation.Id),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Compliance shortcut failed (args={Args})",
                    JsonSerializer.Serialize(complianceArgs));
                complianceResult = AiToolResult.Error($"Erreur lors de l'exécution: {ex.Message}");
            }

            var complianceContent = complianceResult.Success
                ? complianceResult.Data ?? string.Empty
                : $"Erreur: {complianceResult.ErrorMessage}";
            conversation.AddMessage(MessageRole.Tool, complianceContent, "compliance_check_invoice", complianceCallId);
            toolsExecutedThisRequest++;
            toolSourcesForClient.Add(new SourceEntryDto("compliance_check_invoice", complianceCallId));
            yield return ChatStreamEvent.ToolCallEnd("compliance_check_invoice", complianceCallId, complianceSw.ElapsedMilliseconds);
        }

        if (!conversationalFastPath)
        while (continueLoop && toolCallRound < maxRounds)
        {
            continueLoop = false;
            var phaseRound = toolCallRound + 1;
            sw.Restart();
            yield return ChatStreamEvent.PhaseEvent(
                "llm_stream_round",
                "running",
                round: phaseRound);

            var fullContent = new System.Text.StringBuilder();
            List<OllamaToolCall>? pendingToolCalls = null;
            var firstTokenMs = (long?)null;

            // Streaming spéculatif : les segments sûrs (bornés par AiLiveContentStreamer, assainis) sont
            // émis EN DIRECT tant que le round n'a produit aucun tool call ; un content_replace final
            // réconcilie avec le corps post-traité (identique à l'historique). StudioBuilder exclu : son
            // contenu est retravaillé APRÈS le stream (récupération d'appel-texte + redaction dédiée).
            var liveStreaming = _ollamaSettings.LiveContentStreamingEnabled
                && assistantMode != AssistantMode.StudioBuilder;
            var liveFlushedUpTo = 0;
            var liveStreamedAny = false;
            var liveResetSent = false;
            var liveFlushSw = Stopwatch.StartNew();

            int? effectiveNumCtx = null;
            int effectivePromptChars = 0;
            if (modelRef.Kind == LlmProviderKind.Ollama)
            {
                var ollamaMessages = AiConversationMessageMapper.BuildOllamaMessages(systemPrompt, conversation, _ollamaSettings.MaxContextMessages, _ollamaSettings.MaxToolResultChars);
                AttachImagesToLastUserOllamaMessage(ollamaMessages, command.Attachments, modelRef.ProviderModelId, toolCallRound);

                effectivePromptChars = toolsApproxChars;
                foreach (var m in ollamaMessages)
                    effectivePromptChars += m.Content?.Length ?? 0;

                effectiveNumCtx = ResolveChatNumCtx(effectivePromptChars, maxTokens, inferenceProfile);
                if (inferenceProfile?.Device == OllamaInferenceDevice.CpuOnly
                    && OllamaChatNumCtxResolver.MayTruncatePrompt(effectivePromptChars, maxTokens, effectiveNumCtx))
                {
                    _logger.LogWarning(
                        "AI chat {CorrelationId} cpu_ctx_may_truncate prompt_chars={PromptChars} num_ctx={NumCtx} tools_exposed={ToolCount} tool_intent={ToolIntent}",
                        correlationId ?? "-",
                        effectivePromptChars,
                        effectiveNumCtx,
                        tools.Count,
                        toolIntent);
                    yield return ChatStreamEvent.PhaseEvent(
                        "content_quality_warning",
                        "completed",
                        detail: "Le contexte est volumineux ; la réponse peut être incomplète.");
                }

                var request = new OllamaChatRequest
                {
                    Model = modelRef.ProviderModelId,
                    Messages = ollamaMessages,
                    Stream = true,
                    Tools = tools,
                    Options = BuildChatOllamaOptions(
                        temperature,
                        maxTokens,
                        effectiveNumCtx,
                        inferenceProfile)
                };

                using (var gateLease = await _ollamaGenerationGate.AcquireAsync(cancellationToken))
                {
                    if (gateLease.WaitMilliseconds >= 1000)
                    {
                        yield return ChatStreamEvent.PhaseEvent(
                            "ollama_queue_wait",
                            "completed",
                            gateLease.WaitMilliseconds);
                    }

                    await foreach (var chunk in _ollamaClient.StreamChatAsync(
                                       request,
                                       cancellationToken,
                                       streamReadTimeout: null,
                                       useGenerationGate: false))
                    {
                        if (chunk.Message?.ToolCalls is { Count: > 0 } toolCalls)
                        {
                            pendingToolCalls ??= new List<OllamaToolCall>();
                            pendingToolCalls.AddRange(toolCalls);
                            if (liveStreamedAny && !liveResetSent)
                            {
                                // Du texte a été streamé avant l'arrivée d'un tool call : préambule de
                                // tour à outils → on vide la bulle (l'indicateur de frappe reprend).
                                liveResetSent = true;
                                yield return ChatStreamEvent.ContentReplace(string.Empty);
                            }
                        }
                        if (!string.IsNullOrEmpty(chunk.Message?.Content))
                        {
                            if (firstTokenMs is null)
                                firstTokenMs = sw.ElapsedMilliseconds;
                            fullContent.Append(chunk.Message.Content);
                            if (liveStreaming && pendingToolCalls is null)
                            {
                                var pendingChars = fullContent.Length - liveFlushedUpTo;
                                if (pendingChars > 0
                                    && (liveFlushSw.ElapsedMilliseconds >= AiLiveContentStreamer.FlushIntervalMs
                                        || pendingChars >= AiLiveContentStreamer.MinFlushChars))
                                {
                                    var snapshot = fullContent.ToString();
                                    var flushTo = AiLiveContentStreamer.ComputeFlushableLength(snapshot, liveFlushedUpTo);
                                    if (flushTo > liveFlushedUpTo)
                                    {
                                        var segment = AssistantVisibleContentFormatter.SanitizeInternalToolNames(
                                            snapshot[liveFlushedUpTo..flushTo]);
                                        liveFlushedUpTo = flushTo;
                                        liveStreamedAny = true;
                                        totalContentCharsStreamed += segment.Length;
                                        yield return ChatStreamEvent.ContentChunk(segment);
                                    }
                                    liveFlushSw.Restart();
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                var baseUrl = await ResolveOpenRouterBaseUrlAsync(cancellationToken);
                var apiKey = (await _tenantAiProviderRepository.GetDecryptedApiKeyForOpenRouterAsync(cancellationToken))!;
                var openAiMessages = AiConversationMessageMapper.BuildOpenAiMessages(systemPrompt, conversation, _ollamaSettings.MaxContextMessages, _ollamaSettings.MaxToolResultChars);
                AttachImagesToLastUserOpenAiMessage(openAiMessages, command.Attachments, modelRef.ProviderModelId, toolCallRound);

                effectivePromptChars = 0;
                foreach (var m in openAiMessages)
                    effectivePromptChars += (m.Content as string)?.Length ?? 0;

                await foreach (var chunk in _openAiClient.StreamChatAsOllamaCompatibleAsync(
                    baseUrl,
                    apiKey,
                    modelRef.ProviderModelId,
                    openAiMessages,
                    tools,
                    temperature,
                    Math.Max(1, maxTokens),
                    cancellationToken,
                    _ollamaSettings.Seed))
                {
                    if (chunk.Message?.ToolCalls is { Count: > 0 } toolCalls)
                    {
                        pendingToolCalls ??= new List<OllamaToolCall>();
                        pendingToolCalls.AddRange(toolCalls);
                        if (liveStreamedAny && !liveResetSent)
                        {
                            liveResetSent = true;
                            yield return ChatStreamEvent.ContentReplace(string.Empty);
                        }
                    }
                    if (!string.IsNullOrEmpty(chunk.Message?.Content))
                    {
                        if (firstTokenMs is null)
                            firstTokenMs = sw.ElapsedMilliseconds;
                        fullContent.Append(chunk.Message.Content);
                        if (liveStreaming && pendingToolCalls is null)
                        {
                            var pendingChars = fullContent.Length - liveFlushedUpTo;
                            if (pendingChars > 0
                                && (liveFlushSw.ElapsedMilliseconds >= AiLiveContentStreamer.FlushIntervalMs
                                    || pendingChars >= AiLiveContentStreamer.MinFlushChars))
                            {
                                var snapshot = fullContent.ToString();
                                var flushTo = AiLiveContentStreamer.ComputeFlushableLength(snapshot, liveFlushedUpTo);
                                if (flushTo > liveFlushedUpTo)
                                {
                                    var segment = AssistantVisibleContentFormatter.SanitizeInternalToolNames(
                                        snapshot[liveFlushedUpTo..flushTo]);
                                    liveFlushedUpTo = flushTo;
                                    liveStreamedAny = true;
                                    totalContentCharsStreamed += segment.Length;
                                    yield return ChatStreamEvent.ContentChunk(segment);
                                }
                                liveFlushSw.Restart();
                            }
                        }
                    }
                }
            }

            // Récupération des appels d'outil émis en TEXTE par le petit modèle (qwen2.5:3b émet
            // parfois { "name": "studio_generate_system", "arguments": {…} } dans le contenu au lieu
            // d'un tool_call structuré). Strictement borné à StudioBuilder + 1er tour (anti-doublon) :
            // on réinjecte l'appel dans le pipeline d'exécution existant plutôt que de diffuser le
            // JSON brut + un faux succès.
            if (assistantMode == AssistantMode.StudioBuilder
                && toolCallRound == 0
                && pendingToolCalls is not { Count: > 0 }
                && fullContent.Length > 0
                && StudioTextToolCallRecovery.TryExtract(fullContent.ToString(), out var recoveredToolName, out var recoveredSpecJson))
            {
                pendingToolCalls = new List<OllamaToolCall>
                {
                    new()
                    {
                        Id = Guid.NewGuid().ToString("N")[..12],
                        Function = new OllamaToolCallFunction
                        {
                            Name = recoveredToolName,
                            Arguments = new Dictionary<string, object?> { ["spec_json"] = recoveredSpecJson }
                        }
                    }
                };
                fullContent.Clear(); // ne pas laisser fuir l'enveloppe brute comme contenu visible
            }

            // StudioBuilder : retirer de la prose les fuites internes (noms d'outils studio_*, mentions du
            // JSON) que le petit modèle ajoute parfois malgré le prompt. Sanitize fullContent EN PLACE →
            // couvre le contenu streamé (ContentChunk) ET le corps persisté (else-if fullContent plus bas).
            // Placé APRÈS la récupération d'appel-texte (gardé par pendingToolCalls vide) → aucune interférence.
            if (assistantMode == AssistantMode.StudioBuilder
                && pendingToolCalls is not { Count: > 0 }
                && fullContent.Length > 0)
            {
                var sanitizedStudio = AssistantVisibleContentFormatter.RedactStudioInternalLeaks(fullContent.ToString());
                if (!string.Equals(sanitizedStudio, fullContent.ToString(), StringComparison.Ordinal))
                {
                    fullContent.Clear();
                    fullContent.Append(sanitizedStudio);
                }
            }

            // N'émettre du contenu user-visible qu'au tour final (sans appels d'outils) :
            // les préambules intermédiaires (« Pour… ») ne polluent plus la bulle client.
            if (pendingToolCalls is not { Count: > 0 } && fullContent.Length > 0)
            {
                // StudioBuilder : si le contenu visible contient encore une enveloppe d'outil non
                // exécutée (récupération impossible : tour > 0 ou parse en échec), ne pas diffuser le
                // JSON brut / un faux succès — inviter à reformuler.
                if (assistantMode == AssistantMode.StudioBuilder
                    && StudioTextToolCallRecovery.TryExtract(fullContent.ToString(), out _, out _))
                {
                    yield return ChatStreamEvent.ContentReplace(
                        "Je n'ai pas pu lancer la création du système. Reformulez votre demande (ex. : « Crée un système de gestion de congés avec plusieurs tables liées »).");
                }
                else if (liveStreaming && liveStreamedAny)
                {
                    // Streaming live actif : la réconciliation finale (content_replace avec le corps
                    // post-traité complet) est émise par le bloc de persistance ci-dessous. Si le texte
                    // du round est insuffisant, on vide la bulle pour que la synthèse forcée reprenne
                    // proprement (indicateur de frappe) au lieu de laisser un fragment.
                    var visibleRound = PrepareVisibleAssistantBody(fullContent.ToString(), minMeaningfulTextChars);
                    if (!AssistantVisibleContentFormatter.HasMeaningfulAssistantText(visibleRound, minMeaningfulTextChars))
                        yield return ChatStreamEvent.ContentReplace(string.Empty);
                }
                else
                {
                    var visibleRound = PrepareVisibleAssistantBody(fullContent.ToString(), minMeaningfulTextChars);
                    if (AssistantVisibleContentFormatter.HasMeaningfulAssistantText(visibleRound, minMeaningfulTextChars))
                    {
                        totalContentCharsStreamed += visibleRound.Length;
                        yield return ChatStreamEvent.ContentChunk(visibleRound);
                    }
                }
            }

            _logger.LogInformation(
                "AI chat {CorrelationId} phase=llm_stream_round elapsed_ms={ElapsedMs} round={Round} first_token_ms={FirstTokenMs} had_tool_calls={HadToolCalls} num_ctx_effective={NumCtxEffective} prompt_chars={PromptChars} inference_device={InferenceDevice} num_gpu_effective={NumGpuEffective} prefer_adaptive_ctx={PreferAdaptiveCtx}",
                correlationId ?? "-",
                sw.ElapsedMilliseconds,
                phaseRound,
                firstTokenMs,
                pendingToolCalls is { Count: > 0 },
                effectiveNumCtx,
                effectivePromptChars,
                inferenceProfile?.Device.ToString() ?? "-",
                inferenceProfile?.NumGpu,
                inferenceProfile?.PreferAdaptiveChatNumCtx ?? false);
            yield return ChatStreamEvent.PhaseEvent(
                "llm_stream_round",
                "completed",
                sw.ElapsedMilliseconds,
                phaseRound,
                firstTokenMs,
                pendingToolCalls is { Count: > 0 });

            if (pendingToolCalls is { Count: > 0 })
            {
                continueLoop = true;
                toolCallRound++;
                lastToolRoundHadMeaningfulText = AssistantVisibleContentFormatter.HasMeaningfulAssistantText(
                    fullContent.ToString(),
                    minMeaningfulTextChars);

                EnsureToolCallIds(pendingToolCalls);
                var toolCallsJson = ToolCallsPersistenceHelper.Serialize(pendingToolCalls);
                conversation.AddMessage(MessageRole.Assistant, fullContent.ToString(), toolCallsJson: toolCallsJson);

                // Doublons stricts (même nom + mêmes args) émis dans le même round (ex. forecast_revenue ×3) :
                // exécutés UNE seule fois, le résultat est réutilisé pour chaque doublon. Les events SSE et les
                // messages Tool par call_id sont conservés (contrat historique/LLM inchangé).
                var duplicateCallMap = MapDuplicateToolCalls(pendingToolCalls);

                // ── Pass A : Pré-exécution parallèle (no-DB + read-only DB via scope DI isolé) ──
                var preExecutedResults = new Dictionary<string, AiToolResult>(StringComparer.Ordinal);
                var preExecutedDurations = new Dictionary<string, long>(StringComparer.Ordinal);
                var parallelCalls = pendingToolCalls
                    .Where(tc => tc.Function is not null
                        && !string.IsNullOrEmpty(tc.Id)
                        && !duplicateCallMap.ContainsKey(tc.Id!)
                        // Anti-boucle analyse d'écran : un dashboard déjà accumulé ne se ré-exécute pas (Pass B répond le nudge).
                        && !ShouldShortCircuitScreenAnalysisDashboardCall(isScreenAnalysis, tc.Function.Name, accumulatedDashboardJson)
                        && AiToolParallelEligibility.IsEligible(tc.Function.Name, _ollamaSettings))
                    .ToList();

                if (parallelCalls.Count > 1)
                {
                    var usesDbScope = parallelCalls.Any(tc => AiParallelDbToolPolicy.IsSafe(tc.Function!.Name));
                    var maxConcurrency = usesDbScope
                        ? Math.Clamp(_ollamaSettings.MaxParallelDbTools, 1, 8)
                        : Math.Min(parallelCalls.Count, 4);

                    using var sem = new SemaphoreSlim(Math.Min(parallelCalls.Count, maxConcurrency));
                    var execContext = new AiToolExecutionContext(correlationId, conversation.Id);
                    var tasks = parallelCalls.Select(async tc =>
                    {
                        await sem.WaitAsync(cancellationToken);
                        try
                        {
                            var sw2 = Stopwatch.StartNew();
                            AiToolResult res;
                            try
                            {
                                var toolName = tc.Function!.Name;
                                res = AiParallelDbToolPolicy.IsSafe(toolName)
                                    ? await _toolExecutorScopeFactory.ExecuteAsync(
                                        toolName,
                                        tc.Function.Arguments,
                                        execContext,
                                        cancellationToken)
                                    : await _toolExecutor.ExecuteAsync(
                                        toolName,
                                        tc.Function.Arguments,
                                        execContext,
                                        cancellationToken);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Tool execution failed for {ToolName}", tc.Function!.Name);
                                res = AiToolResult.Error($"Erreur lors de l'exécution: {ex.Message}");
                            }

                            return (tc.Id!, res, sw2.ElapsedMilliseconds);
                        }
                        finally
                        {
                            sem.Release();
                        }
                    });
                    var results = await Task.WhenAll(tasks);
                    foreach (var (id, res, ms) in results)
                    {
                        preExecutedResults[id] = res;
                        preExecutedDurations[id] = ms;
                    }
                }

                // ── Pass B : Itération ordonnée pour préserver l'ordre des events SSE et de la conversation ──
                var completedCallResults = new Dictionary<string, AiToolResult>(StringComparer.Ordinal);
                foreach (var toolCall in pendingToolCalls)
                {
                    if (toolCall.Function is null)
                        continue;

                    var callId = !string.IsNullOrEmpty(toolCall.Id)
                        ? toolCall.Id!
                        : Guid.NewGuid().ToString("N")[..12];

                    yield return ChatStreamEvent.ToolCallStart(toolCall.Function.Name, callId);

                    _logger.LogInformation("AI tool call: {ToolName} with args: {Args}",
                        toolCall.Function.Name,
                        JsonSerializer.Serialize(toolCall.Function.Arguments));

                    AiToolResult toolResult;
                    long toolElapsedMs;
                    var wasDeduplicated = false;
                    // Anti-boucle analyse d'écran : le petit modèle rappelle generate_dashboard_config au lieu
                    // de rédiger l'analyse. Une fois un dashboard accumulé, les appels suivants reçoivent un
                    // message déterministe qui le pousse à écrire — sans ré-exécution ni second événement SSE.
                    var dashboardShortCircuited = ShouldShortCircuitScreenAnalysisDashboardCall(
                        isScreenAnalysis, toolCall.Function.Name, accumulatedDashboardJson);
                    bool wasParallel = preExecutedResults.TryGetValue(callId, out var preResult) && !dashboardShortCircuited;
                    if (dashboardShortCircuited)
                    {
                        toolResult = AiToolResult.Ok(ScreenAnalysisDashboardAlreadyGeneratedMessage);
                        toolElapsedMs = 0;
                    }
                    else if (wasParallel)
                    {
                        toolResult = preResult!;
                        toolElapsedMs = preExecutedDurations.TryGetValue(callId, out var d) ? d : 0;
                    }
                    else if (duplicateCallMap.TryGetValue(callId, out var canonicalCallId)
                        && completedCallResults.TryGetValue(canonicalCallId, out var canonicalResult))
                    {
                        // Doublon strict : réutilise le résultat du 1er appel équivalent (aucune ré-exécution).
                        toolResult = canonicalResult;
                        toolElapsedMs = 0;
                        wasDeduplicated = true;
                    }
                    else
                    {
                        var toolSw = Stopwatch.StartNew();
                        try
                        {
                            toolResult = await _toolExecutor.ExecuteAsync(
                                toolCall.Function.Name,
                                toolCall.Function.Arguments,
                                new AiToolExecutionContext(correlationId, conversation.Id),
                                cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Tool execution failed for {ToolName}", toolCall.Function.Name);
                            toolResult = AiToolResult.Error($"Erreur lors de l'exécution: {ex.Message}");
                        }
                        toolElapsedMs = toolSw.ElapsedMilliseconds;
                    }

                    completedCallResults[callId] = toolResult;

                    _logger.LogInformation(
                        "AI chat {CorrelationId} phase=ai_tool_execute elapsed_ms={ElapsedMs} tool={Tool} call_id={CallId} parallel={Parallel} dedup={Dedup}",
                        correlationId ?? "-",
                        toolElapsedMs,
                        toolCall.Function.Name,
                        callId,
                        wasParallel,
                        wasDeduplicated);

                    var resultContent = toolResult.Success
                        ? toolResult.Data
                        : $"Erreur: {toolResult.ErrorMessage}";

                    conversation.AddMessage(MessageRole.Tool, resultContent, toolCall.Function.Name, callId);
                    toolsExecutedThisRequest++;
                    yield return ChatStreamEvent.ToolCallEnd(toolCall.Function.Name, callId, toolElapsedMs);
                    toolSourcesForClient.Add(new SourceEntryDto(toolCall.Function.Name, callId));
                    if (toolCall.Function.Name == "propose_client_actions" && toolResult.Success &&
                        !string.IsNullOrWhiteSpace(toolResult.Data))
                        yield return ChatStreamEvent.ClientActionsEvent(toolResult.Data);
                    if (toolCall.Function.Name == "propose_follow_up_prompts" && toolResult.Success &&
                        !string.IsNullOrWhiteSpace(toolResult.Data))
                    {
                        yield return ChatStreamEvent.SuggestedPromptsEvent(toolResult.Data);
                        try
                        {
                            using var promptsDoc = JsonDocument.Parse(toolResult.Data);
                            if (promptsDoc.RootElement.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var el in promptsDoc.RootElement.EnumerateArray())
                                {
                                    if (el.ValueKind != JsonValueKind.String)
                                        continue;
                                    var s = el.GetString();
                                    if (string.IsNullOrEmpty(s) || accumulatedSuggestedPrompts.Contains(s))
                                        continue;
                                    accumulatedSuggestedPrompts.Add(s);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Impossible d'extraire les suggestions de suivi pour la persistance.");
                        }
                    }
                    if (!dashboardShortCircuited
                        && toolCall.Function.Name == "generate_dashboard_config" && toolResult.Success &&
                        !string.IsNullOrWhiteSpace(toolResult.Data))
                    {
                        accumulatedDashboardJson = toolResult.Data;
                        yield return ChatStreamEvent.DashboardEvent(toolResult.Data);
                    }
                    if ((toolCall.Function.Name == "studio_generate_app" || toolCall.Function.Name == "studio_generate_system")
                        && toolResult.Success && !string.IsNullOrWhiteSpace(toolResult.Data))
                    {
                        foreach (var progressEv in TryBuildStudioProgressEvents(toolResult.Data))
                            yield return progressEv;
                        var openAction = TryBuildStudioClientActions(toolResult.Data);
                        if (openAction is not null)
                            yield return ChatStreamEvent.ClientActionsEvent(openAction);
                    }
                    else if ((toolCall.Function.Name == "studio_generate_app" || toolCall.Function.Name == "studio_generate_system")
                        && !toolResult.Success && assistantMode == AssistantMode.StudioBuilder)
                    {
                        // Capturer l'erreur réelle (ex. « Limite du plan atteinte : 50 tables maximum… ») pour
                        // l'afficher telle quelle après la boucle, au lieu de la paraphrase du petit modèle.
                        studioBuilderToolError = string.IsNullOrWhiteSpace(toolResult.ErrorMessage)
                            ? "La création du système a échoué."
                            : toolResult.ErrorMessage;
                    }
                }
            }
            else if (fullContent.Length > 0)
            {
                var assistantBody = BuildFinalAssistantBodyWithAppendices(
                    fullContent.ToString(),
                    minMeaningfulTextChars,
                    accumulatedDashboardJson,
                    accumulatedSuggestedPrompts);
                var leakClean = TryBuildLeakReplacementBody(
                    assistantBody,
                    conversation,
                    command.Message,
                    toolIntent,
                    toolsExecutedThisRequest > 0,
                    minMeaningfulTextChars,
                    accumulatedDashboardJson,
                    accumulatedSuggestedPrompts);
                if (leakClean is not null)
                {
                    // Une fuite de nom d'outil a été neutralisée : on remplace le contenu déjà streamé côté client.
                    assistantBody = leakClean;
                    deterministicFallbackUsed = true;
                    yield return ChatStreamEvent.ContentReplace(assistantBody);
                }
                if (AssistantVisibleContentFormatter.HasMeaningfulAssistantText(assistantBody, minMeaningfulTextChars))
                {
                    contentCharsPersisted = assistantBody.Length;
                    conversation.AddMessage(MessageRole.Assistant, assistantBody);
                    meaningfulResponseDelivered = true;
                    // Réconciliation du streaming live : le corps final post-traité (appendices,
                    // humanisation, assainissement complet) remplace les segments streamés. Sans
                    // streaming live, l'unique ContentChunk du round a déjà livré ce contenu.
                    if (liveStreaming && liveStreamedAny && leakClean is null)
                        yield return ChatStreamEvent.ContentReplace(assistantBody);
                }
            }
        }

        // StudioBuilder : un outil studio_generate_* a échoué → afficher l'erreur RÉELLE comme contenu
        // final (le ContentReplace recouvre la paraphrase éventuelle du modèle) et couper la synthèse.
        if (studioBuilderToolError is not null)
        {
            yield return ChatStreamEvent.ContentReplace(studioBuilderToolError);
            if (!meaningfulResponseDelivered)
            {
                conversation.AddMessage(MessageRole.Assistant, studioBuilderToolError);
                meaningfulResponseDelivered = true;
            }
        }

        // ── Filet de synthèse finale : aucune réponse visible complète après exécution d'outils ───────
        if (!conversationalFastPath
            && ShouldForceFinalSynthesis(
                isScreenAnalysis,
                _ollamaSettings.ForceFinalSynthesis,
                meaningfulResponseDelivered,
                toolsExecutedThisRequest > 0,
                _ollamaSettings.ForceFinalSynthesisOnlyAfterTools,
                _screenAnalysisOptions.ForceFinalSynthesisEnabled))
        {
            forcedSynthesisTriggered = true;
            sw.Restart();
            yield return ChatStreamEvent.PhaseEvent("llm_forced_synthesis", "running");
            var synthContent = new System.Text.StringBuilder();
            // Streaming live de la synthèse : aucun outil attaché à cet appel → pas de reset possible ;
            // le ContentReplace(synthBody) final réconcilie comme pour le round principal.
            var synthLive = _ollamaSettings.LiveContentStreamingEnabled
                && assistantMode != AssistantMode.StudioBuilder;
            var synthFlushedUpTo = 0;
            var synthStreamedAny = false;
            var synthFlushSw = Stopwatch.StartNew();
            // Analyse d'écran : consigne dédiée (rédiger les 7 sections, aucun outil) ; le littéral
            // historique du mode Default reste strictement inchangé.
            var synthSystemPrompt = isScreenAnalysis
                ? systemPrompt + "\n\n" + AiScreenAnalysisPromptBuilder.ForcedSynthesisSection
                : toolsExecutedThisRequest > 0
                    ? systemPrompt + "\n\nSYNTHÈSE FINALE : Des résultats d'outils sont dans l'historique. "
                      + "Synthétise-les en français avec montants TND. Ne redemande pas la période si l'utilisateur a dit « aujourd'hui »."
                    : systemPrompt;

            if (modelRef.Kind == LlmProviderKind.Ollama)
            {
                var synthMessages = AiConversationMessageMapper.BuildOllamaMessages(synthSystemPrompt, conversation, _ollamaSettings.MaxContextMessages, _ollamaSettings.MaxToolResultChars);

                var synthPromptChars = 0;
                foreach (var m in synthMessages)
                    synthPromptChars += m.Content?.Length ?? 0;
                var synthNumCtx = ResolveChatNumCtx(synthPromptChars, maxTokens, inferenceProfile);

                var synthRequest = new OllamaChatRequest
                {
                    Model = modelRef.ProviderModelId,
                    Messages = synthMessages,
                    Stream = true,
                    Tools = null,
                    Options = BuildChatOllamaOptions(
                        temperature,
                        maxTokens,
                        synthNumCtx,
                        inferenceProfile)
                };

                using (var gateLease = await _ollamaGenerationGate.AcquireAsync(cancellationToken))
                {
                    if (gateLease.WaitMilliseconds >= 1000)
                    {
                        yield return ChatStreamEvent.PhaseEvent(
                            "ollama_queue_wait",
                            "completed",
                            gateLease.WaitMilliseconds);
                    }

                    await foreach (var chunk in _ollamaClient.StreamChatAsync(
                                       synthRequest,
                                       cancellationToken,
                                       streamReadTimeout: null,
                                       useGenerationGate: false))
                    {
                        if (string.IsNullOrEmpty(chunk.Message?.Content))
                            continue;
                        synthContent.Append(chunk.Message.Content);
                        if (!synthLive)
                            continue;
                        var pendingChars = synthContent.Length - synthFlushedUpTo;
                        if (pendingChars > 0
                            && (synthFlushSw.ElapsedMilliseconds >= AiLiveContentStreamer.FlushIntervalMs
                                || pendingChars >= AiLiveContentStreamer.MinFlushChars))
                        {
                            var snapshot = synthContent.ToString();
                            var flushTo = AiLiveContentStreamer.ComputeFlushableLength(snapshot, synthFlushedUpTo);
                            if (flushTo > synthFlushedUpTo)
                            {
                                var segment = AssistantVisibleContentFormatter.SanitizeInternalToolNames(
                                    snapshot[synthFlushedUpTo..flushTo]);
                                synthFlushedUpTo = flushTo;
                                synthStreamedAny = true;
                                totalContentCharsStreamed += segment.Length;
                                yield return ChatStreamEvent.ContentChunk(segment);
                            }
                            synthFlushSw.Restart();
                        }
                    }
                }
            }
            else
            {
                var baseUrl = await ResolveOpenRouterBaseUrlAsync(cancellationToken);
                var apiKey = (await _tenantAiProviderRepository.GetDecryptedApiKeyForOpenRouterAsync(cancellationToken))!;
                var synthMessages = AiConversationMessageMapper.BuildOpenAiMessages(synthSystemPrompt, conversation, _ollamaSettings.MaxContextMessages, _ollamaSettings.MaxToolResultChars);

                await foreach (var chunk in _openAiClient.StreamChatAsOllamaCompatibleAsync(
                    baseUrl,
                    apiKey,
                    modelRef.ProviderModelId,
                    synthMessages,
                    new List<OllamaToolDefinition>(),
                    temperature,
                    Math.Max(1, maxTokens),
                    cancellationToken,
                    _ollamaSettings.Seed))
                {
                    if (string.IsNullOrEmpty(chunk.Message?.Content))
                        continue;
                    synthContent.Append(chunk.Message.Content);
                    if (!synthLive)
                        continue;
                    var pendingChars = synthContent.Length - synthFlushedUpTo;
                    if (pendingChars > 0
                        && (synthFlushSw.ElapsedMilliseconds >= AiLiveContentStreamer.FlushIntervalMs
                            || pendingChars >= AiLiveContentStreamer.MinFlushChars))
                    {
                        var snapshot = synthContent.ToString();
                        var flushTo = AiLiveContentStreamer.ComputeFlushableLength(snapshot, synthFlushedUpTo);
                        if (flushTo > synthFlushedUpTo)
                        {
                            var segment = AssistantVisibleContentFormatter.SanitizeInternalToolNames(
                                snapshot[synthFlushedUpTo..flushTo]);
                            synthFlushedUpTo = flushTo;
                            synthStreamedAny = true;
                            totalContentCharsStreamed += segment.Length;
                            yield return ChatStreamEvent.ContentChunk(segment);
                        }
                        synthFlushSw.Restart();
                    }
                }
            }

            yield return ChatStreamEvent.PhaseEvent("llm_forced_synthesis", "completed", sw.ElapsedMilliseconds);

            var synthBody = synthContent.Length > 0
                ? BuildFinalAssistantBodyWithAppendices(
                    synthContent.ToString(),
                    minMeaningfulTextChars,
                    accumulatedDashboardJson,
                    accumulatedSuggestedPrompts)
                : string.Empty;

            // Analyse d'écran : le fallback générique est écarté au profit du filet TEXTE dédié
            // (TryBuildAnalysisTextFromSnapshot, plus bas) construit depuis le snapshot de l'écran.
            if (!isScreenAnalysis
                && !AssistantVisibleContentFormatter.HasMeaningfulAssistantText(synthBody, minMeaningfulTextChars))
            {
                var fallback = AssistantDeterministicFallback.TryBuild(
                    conversation,
                    command.Message,
                    minMeaningfulTextChars,
                    _ollamaSettings.DeterministicFallbackEnabled,
                    toolsExecutedThisRequest > 0,
                    toolIntent);
                if (!string.IsNullOrWhiteSpace(fallback))
                {
                    synthBody = BuildFinalAssistantBodyWithAppendices(
                        fallback,
                        minMeaningfulTextChars,
                        accumulatedDashboardJson,
                        accumulatedSuggestedPrompts);
                    deterministicFallbackUsed = true;
                }
            }

            var synthLeakClean = TryBuildLeakReplacementBody(
                synthBody,
                conversation,
                command.Message,
                toolIntent,
                toolsExecutedThisRequest > 0,
                minMeaningfulTextChars,
                accumulatedDashboardJson,
                accumulatedSuggestedPrompts);
            if (synthLeakClean is not null)
            {
                synthBody = synthLeakClean;
                deterministicFallbackUsed = true;
            }

            // StudioBuilder : même assainissement des fuites internes sur la synthèse forcée (cas rare).
            if (assistantMode == AssistantMode.StudioBuilder && !string.IsNullOrWhiteSpace(synthBody))
                synthBody = AssistantVisibleContentFormatter.RedactStudioInternalLeaks(synthBody);

            // Analyse d'écran : une synthèse sous le seuil de prose visible ne doit PAS être persistée —
            // elle marquerait meaningfulResponseDelivered et affamerait le filet TEXTE déterministe,
            // re-déclenchant le bouchon frontend. Pour les autres modes : condition historique inchangée.
            var synthDeliverable = !string.IsNullOrWhiteSpace(synthBody)
                && (!isScreenAnalysis
                    || AssistantVisibleContentFormatter.HasMeaningfulAssistantText(synthBody, minMeaningfulTextChars));
            if (synthDeliverable)
            {
                contentCharsPersisted = synthBody.Length;
                totalContentCharsStreamed += synthBody.Length;
                conversation.AddMessage(MessageRole.Assistant, synthBody);
                meaningfulResponseDelivered = true;
                yield return ChatStreamEvent.ContentReplace(synthBody);
            }
            else if (synthStreamedAny)
            {
                // Rien d'exploitable après post-traitement : ne pas laisser des fragments streamés.
                yield return ChatStreamEvent.ContentReplace(string.Empty);
            }
        }

        if (isScreenAnalysis && _screenAnalysisOptions.PostProcessToolsEnabled)
        {
            if (accumulatedSuggestedPrompts.Count == 0)
            {
                var promptsJson = AiScreenAnalysisPostProcessor.BuildDefaultFollowUpPromptsJson(screenId);
                var followUpResult = await _toolExecutor.ExecuteAsync(
                    "propose_follow_up_prompts",
                    new Dictionary<string, object?> { ["prompts_json"] = promptsJson },
                    new AiToolExecutionContext(correlationId, conversation.Id),
                    cancellationToken);
                if (followUpResult.Success && !string.IsNullOrWhiteSpace(followUpResult.Data))
                {
                    yield return ChatStreamEvent.SuggestedPromptsEvent(followUpResult.Data);
                    TryAccumulateSuggestedPrompts(followUpResult.Data, accumulatedSuggestedPrompts);
                }
            }

            if (string.IsNullOrEmpty(accumulatedDashboardJson))
            {
                var dashboardJson = AiScreenAnalysisPostProcessor.TryBuildDashboardFromSnapshot(
                    command.UiContext?.AnalysisSummary,
                    screenId,
                    _screenAnalysisOptions);
                if (!string.IsNullOrEmpty(dashboardJson))
                {
                    accumulatedDashboardJson = dashboardJson;
                    yield return ChatStreamEvent.DashboardEvent(dashboardJson);
                }
            }

            // ── Filet TEXTE analyse d'écran : ni le modèle ni la synthèse forcée n'ont livré de prose
            // visible — sans lui, le frontend affiche le bouchon « Analyse disponible dans le tableau
            // de bord… ». Sections factuelles construites depuis le snapshot, zéro appel LLM.
            if (!meaningfulResponseDelivered)
            {
                var fallbackText = AiScreenAnalysisPostProcessor.TryBuildAnalysisTextFromSnapshot(
                    command.UiContext?.AnalysisSummary,
                    screenId,
                    _screenAnalysisOptions);
                if (!string.IsNullOrWhiteSpace(fallbackText))
                {
                    var fallbackBody = BuildFinalAssistantBodyWithAppendices(
                        fallbackText,
                        minMeaningfulTextChars,
                        accumulatedDashboardJson,
                        accumulatedSuggestedPrompts);
                    contentCharsPersisted = fallbackBody.Length;
                    totalContentCharsStreamed += fallbackBody.Length;
                    conversation.AddMessage(MessageRole.Assistant, fallbackBody);
                    meaningfulResponseDelivered = true;
                    deterministicFallbackUsed = true;
                    yield return ChatStreamEvent.ContentReplace(fallbackBody);
                }
            }
        }

        // StudioBuilder : filet anti-silence. Si, après tout, rien de visible n'a été livré (le petit
        // modèle n'a ni exécuté d'outil, ni produit de texte exploitable), garantir une réponse claire
        // au lieu d'une bulle vide.
        if (assistantMode == AssistantMode.StudioBuilder && !meaningfulResponseDelivered)
        {
            const string fallback = "Je n'ai pas réussi à créer ce système. Reformulez votre demande en décrivant les tables et leurs champs (ex. « table Employés avec nom, poste ; table Congés avec employé, dates, statut »).";
            conversation.AddMessage(MessageRole.Assistant, fallback);
            meaningfulResponseDelivered = true;
            yield return ChatStreamEvent.ContentReplace(fallback);
        }

        sw.Restart();
        yield return ChatStreamEvent.PhaseEvent("persist_conversation", "running");
        // Persistance batchée : on persiste explicitement avec CancellationToken.None pour que
        // la sauvegarde aboutisse même si le client a coupé la connexion entre-temps (Stop).
        // Cela garantit que les messages assistant/tool produits restent dans la base.
        if (isNewConversation && deferUserPersist)
            await _conversationRepository.AddAsync(conversation, CancellationToken.None);
        else
            await _conversationRepository.UpdateAsync(conversation, CancellationToken.None);
        LogPhase("persist_conversation", sw.ElapsedMilliseconds);
        yield return ChatStreamEvent.PhaseEvent(
            "persist_conversation",
            "completed",
            sw.ElapsedMilliseconds);

        _logger.LogInformation(
            "AI chat {CorrelationId} phase=total_request elapsed_ms={ElapsedMs} conversation_id={ConversationId} model={Model} tool_intent={ToolIntent} conversational_fast_path={ConversationalFastPath} tool_rounds_executed={ToolRounds} tools_executed_this_request={ToolsExecutedThisRequest} forced_synthesis_triggered={ForcedSynthesisTriggered} deterministic_fallback_used={DeterministicFallbackUsed} final_response_meaningful={FinalResponseMeaningful} content_chars_streamed={ContentCharsStreamed} content_chars_persisted={ContentCharsPersisted}",
            correlationId ?? "-",
            total.ElapsedMilliseconds,
            conversation.Id,
            modelRef.CanonicalModelRef,
            toolIntent,
            conversationalFastPath,
            toolCallRound,
            toolsExecutedThisRequest,
            forcedSynthesisTriggered,
            deterministicFallbackUsed,
            meaningfulResponseDelivered,
            totalContentCharsStreamed,
            contentCharsPersisted);
        yield return ChatStreamEvent.PhaseEvent(
            "total_request",
            "completed",
            total.ElapsedMilliseconds,
            hadToolCalls: toolCallRound > 0,
            detail: modelRef.CanonicalModelRef);

        if (toolSourcesForClient.Count > 0)
        {
            var srcJson = JsonSerializer.Serialize(toolSourcesForClient, SourcesJsonOptions);
            yield return ChatStreamEvent.SourcesEvent(srcJson);
        }

        yield return ChatStreamEvent.Done(conversation.Id);
    }

    /// <summary>
    /// Détermine si le filet de synthèse finale doit se déclencher lorsqu'aucune réponse visible complète
    /// n'a encore été livrée au client (optionnellement seulement après exécution d'outils).
    /// L'analyse d'écran est éligible uniquement si <paramref name="screenAnalysisForceFinalSynthesisEnabled"/>
    /// (ScreenAnalysis.ForceFinalSynthesisEnabled) est vrai — défaut du paramètre false : les appels 5-args
    /// existants conservent la sémantique historique à l'identique (ScreenAnalysis exclu).
    /// </summary>
    public static bool ShouldForceFinalSynthesis(
        bool isScreenAnalysis,
        bool forceEnabled,
        bool meaningfulResponseDelivered,
        bool toolsWereExecuted,
        bool onlyAfterTools,
        bool screenAnalysisForceFinalSynthesisEnabled = false)
        => (!isScreenAnalysis || screenAnalysisForceFinalSynthesisEnabled)
           && forceEnabled
           && !meaningfulResponseDelivered
           && (!onlyAfterTools || toolsWereExecuted);

    public static int ResolveMinMeaningfulTextChars(
        AiToolIntentRouter.AiToolIntent toolIntent,
        OllamaSettings settings)
    {
        var defaultMin = Math.Clamp(settings.MinAssistantTextCharsForCompleteResponse, 1, 4096);
        return toolIntent == AiToolIntentRouter.AiToolIntent.Greeting
            ? Math.Min(20, defaultMin)
            : defaultMin;
    }

    private static string BuildFinalAssistantBodyWithAppendices(
        string rawBody,
        int minMeaningfulTextChars,
        string? accumulatedDashboardJson,
        IReadOnlyList<string> accumulatedSuggestedPrompts)
    {
        var assistantBody = PrepareVisibleAssistantBody(rawBody, minMeaningfulTextChars);
        if (!string.IsNullOrEmpty(accumulatedDashboardJson) &&
            !AssistantDashboardContent.HasDashboardFence(assistantBody))
        {
            assistantBody += AssistantDashboardContent.BuildAppendix(accumulatedDashboardJson);
        }

        if (accumulatedSuggestedPrompts.Count > 0)
            assistantBody += BuildFtMetaAppendix(accumulatedSuggestedPrompts);

        return assistantBody;
    }

    private static string PrepareVisibleAssistantBody(string rawBody, int minMeaningfulChars) =>
        AssistantVisibleContentFormatter.EnhanceForDisplay(
            // Substitution systématique des identifiants d'outils internes par leur libellé métier —
            // AVANT tout le reste, y compris quand aucun outil n'a tourné dans la requête (réponse
            // depuis l'historique) : c'était le trou de la garde anti-fuite « tout ou rien ».
            AssistantVisibleContentFormatter.SanitizeInternalToolNames(rawBody),
            minMeaningfulChars);

    /// <summary>
    /// Filet de DERNIER RECOURS anti-fuite : la substitution par libellés
    /// (<see cref="AssistantVisibleContentFormatter.SanitizeInternalToolNames"/>, appliquée en amont dans
    /// <see cref="PrepareVisibleAssistantBody"/>) neutralise déjà les fuites en PRÉSERVANT la réponse ;
    /// ce filet ne se déclenche que si un nom d'outil du registre subsiste malgré tout, et remplace alors
    /// le corps par une synthèse déterministe propre. Plus de condition « outils exécutés » : une réponse
    /// bâtie depuis l'historique (0 outil ce tour) était l'angle mort qui laissait fuir les identifiants.
    /// </summary>
    private string? TryBuildLeakReplacementBody(
        string body,
        Conversation conversation,
        string? userMessage,
        AiToolIntentRouter.AiToolIntent toolIntent,
        bool toolsExecuted,
        int minMeaningfulTextChars,
        string? accumulatedDashboardJson,
        IReadOnlyList<string> accumulatedSuggestedPrompts)
    {
        if (!_ollamaSettings.RedactInternalToolNamesEnabled
            || !AssistantVisibleContentFormatter.ContainsInternalToolName(body))
        {
            return null;
        }

        var clean = AssistantDeterministicFallback.TryBuild(
            conversation,
            userMessage,
            minMeaningfulTextChars,
            _ollamaSettings.DeterministicFallbackEnabled,
            toolsExecuted,
            toolIntent);

        if (string.IsNullOrWhiteSpace(clean))
        {
            clean = "Je n'ai pas pu finaliser cette réponse à partir des données disponibles. Reformulez votre question.";
        }

        return BuildFinalAssistantBodyWithAppendices(
            clean,
            minMeaningfulTextChars,
            accumulatedDashboardJson,
            accumulatedSuggestedPrompts);
    }

    /// <summary>
    /// Builds client navigation actions from studio_generate_app / studio_generate_system results.
    /// </summary>
    private static string? TryBuildStudioClientActions(string toolData)
    {
        try
        {
            using var doc = JsonDocument.Parse(toolData);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            var actions = new List<object>();

            if (root.TryGetProperty("systemUrl", out var sysUrl) && sysUrl.ValueKind == JsonValueKind.String)
            {
                var url = sysUrl.GetString();
                if (!string.IsNullOrWhiteSpace(url) && url.StartsWith("/", StringComparison.Ordinal))
                {
                    var sysLabel = root.TryGetProperty("displayName", out var dn) && dn.ValueKind == JsonValueKind.String
                        ? $"Ouvrir le système « {dn.GetString()} »"
                        : "Ouvrir le système";
                    actions.Add(new { label = sysLabel, route = url });
                }
            }

            if (root.TryGetProperty("entities", out var entities) && entities.ValueKind == JsonValueKind.Array)
            {
                foreach (var ent in entities.EnumerateArray())
                {
                    if (!ent.TryGetProperty("openUrl", out var eu) || eu.ValueKind != JsonValueKind.String) continue;
                    var url = eu.GetString();
                    if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("/", StringComparison.Ordinal)) continue;
                    var label = ent.TryGetProperty("displayName", out var en) && en.ValueKind == JsonValueKind.String
                        ? $"Ouvrir « {en.GetString()} »"
                        : "Ouvrir la table";
                    actions.Add(new { label, route = url });
                }
            }
            else if (root.TryGetProperty("openUrl", out var urlEl) && urlEl.ValueKind == JsonValueKind.String)
            {
                var url = urlEl.GetString();
                if (!string.IsNullOrWhiteSpace(url) && url.StartsWith("/", StringComparison.Ordinal))
                {
                    var label = root.TryGetProperty("displayName", out var dn) && dn.ValueKind == JsonValueKind.String
                        ? $"Ouvrir « {dn.GetString()} »"
                        : "Ouvrir la table";
                    actions.Add(new { label, route = url });
                }
            }

            return actions.Count > 0 ? JsonSerializer.Serialize(actions, SourcesJsonOptions) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IEnumerable<ChatStreamEvent> TryBuildStudioProgressEvents(string toolData)
    {
        var events = new List<ChatStreamEvent>();
        try
        {
            using var doc = JsonDocument.Parse(toolData);
            if (!doc.RootElement.TryGetProperty("buildSteps", out var steps) || steps.ValueKind != JsonValueKind.Array)
                return events;
            foreach (var step in steps.EnumerateArray())
                events.Add(ChatStreamEvent.StudioProgressEvent(step.GetRawText()));
        }
        catch (JsonException)
        {
            // ignore malformed tool payloads
        }

        return events;
    }

    private sealed record SourceEntryDto(string ToolName, string ToolCallId);

    /// <summary>
    /// Si le modèle ciblé supporte la vision et que des pièces jointes portent des
    /// images base64, attache-les au DERNIER message utilisateur (= message courant).
    /// Ne fait rien au-delà du 1er tour de tool-call (les rounds suivants n'ont pas
    /// besoin de re-pousser les images).
    /// </summary>
    private static void AttachImagesToLastUserOllamaMessage(
        List<OllamaChatMessage> messages,
        IReadOnlyList<ChatAttachmentInput>? attachments,
        string modelRef,
        int toolCallRound)
    {
        if (toolCallRound > 0 || attachments is null || attachments.Count == 0) return;
        if (!AiModelCapabilityDetector.DetectVisionSupport(modelRef)) return;

        var images = attachments
            .Where(a => a.ImagesBase64 is { Count: > 0 })
            .SelectMany(a => a.ImagesBase64!)
            .Where(b64 => !string.IsNullOrWhiteSpace(b64))
            .Take(10) // garde-fou : 10 images max par requête
            .ToList();
        if (images.Count == 0) return;

        for (var i = messages.Count - 1; i >= 0; i--)
        {
            if (messages[i].Role == "user")
            {
                messages[i] = messages[i] with { Images = images };
                return;
            }
        }
    }

    /// <summary>
    /// Variante OpenAI / OpenRouter : transforme le content du dernier user message
    /// en tableau de parts ([{type:text}, {type:image_url}, ...]) si le modèle est vision.
    /// </summary>
    private static void AttachImagesToLastUserOpenAiMessage(
        List<OpenAiChatMessagePayload> messages,
        IReadOnlyList<ChatAttachmentInput>? attachments,
        string modelRef,
        int toolCallRound)
    {
        if (toolCallRound > 0 || attachments is null || attachments.Count == 0) return;
        if (!AiModelCapabilityDetector.DetectVisionSupport(modelRef)) return;

        var images = attachments
            .Where(a => a.ImagesBase64 is { Count: > 0 })
            .SelectMany(a => a.ImagesBase64!)
            .Where(b64 => !string.IsNullOrWhiteSpace(b64))
            .Take(10)
            .ToList();
        if (images.Count == 0) return;

        for (var i = messages.Count - 1; i >= 0; i--)
        {
            if (messages[i].Role == "user")
            {
                var existingText = messages[i].Content as string ?? string.Empty;
                var parts = new List<OpenAiContentPart>
                {
                    new() { Type = "text", Text = existingText }
                };
                foreach (var b64 in images)
                {
                    parts.Add(new OpenAiContentPart
                    {
                        Type = "image_url",
                        ImageUrl = new OpenAiImageUrl { Url = "data:image/png;base64," + b64 }
                    });
                }
                messages[i] = new OpenAiChatMessagePayload
                {
                    Role = "user",
                    Content = parts,
                    ToolCalls = messages[i].ToolCalls,
                    ToolCallId = messages[i].ToolCallId
                };
                return;
            }
        }
    }


    private static string? TryExtractSchemaVersion(string? analysisSummary)
    {
        if (string.IsNullOrWhiteSpace(analysisSummary))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(analysisSummary);
            if (doc.RootElement.TryGetProperty("schemaVersion", out var sv))
                return sv.ToString();
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private void TryAccumulateSuggestedPrompts(string json, List<string> accumulated)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return;

            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.String)
                    continue;
                var s = el.GetString();
                if (!string.IsNullOrEmpty(s) && !accumulated.Contains(s))
                    accumulated.Add(s);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible d'extraire les suggestions de suivi post-traitement.");
        }
    }

    private static string BuildConversationTitle(SendChatMessageCommand command)
    {
        if (!string.IsNullOrWhiteSpace(command.UiContext?.AnalysisSummary))
            return AiScreenTitleFormatter.BuildScreenAnalysisConversationTitle(command.UiContext.ScreenId);

        return command.Message.Length > 80
            ? command.Message[..80] + "..."
            : command.Message;
    }

    private static string BuildFtMetaAppendix(IReadOnlyList<string> prompts)
    {
        var json = JsonSerializer.Serialize(new { suggestedPrompts = prompts }, SourcesJsonOptions);
        return "\n\n```ft-meta\n" + json + "\n```\n";
    }

    /// <summary>
    /// Anti-boucle analyse d'écran : une fois un tableau de bord accumulé dans la requête, les appels
    /// generate_dashboard_config suivants sont court-circuités (réponse déterministe, pas de ré-exécution) —
    /// le petit modèle rappelait l'outil round après round au lieu de rédiger l'analyse. Gated ScreenAnalysis :
    /// aucun changement pour les autres modes.
    /// </summary>
    public static bool ShouldShortCircuitScreenAnalysisDashboardCall(
        bool isScreenAnalysis,
        string? toolName,
        string? accumulatedDashboardJson)
        => isScreenAnalysis
           && string.Equals(toolName, "generate_dashboard_config", StringComparison.Ordinal)
           && !string.IsNullOrEmpty(accumulatedDashboardJson);

    /// <summary>Message-nudge renvoyé au modèle à la place d'une ré-exécution (aucun nom d'outil dedans).</summary>
    public const string ScreenAnalysisDashboardAlreadyGeneratedMessage =
        "Le tableau de bord est déjà généré. Rédige maintenant l'analyse complète en sections markdown "
        + "(## Synthèse exécutive, ## Indicateurs clés, ## Analyse détaillée, ## Anomalies et risques, "
        + "## Opportunités, ## Actions recommandées, ## Points à vérifier).";

    /// <summary>
    /// Regroupe les tool-calls STRICTEMENT identiques (même nom + mêmes arguments sérialisés) d'un même
    /// round : chaque doublon pointe vers le PREMIER appel équivalent, qui seul est exécuté — le petit
    /// modèle émet parfois plusieurs fois le même appel (ex. forecast_revenue ×3), tripler la latence et
    /// le SQL est inutile. Des arguments différents (ex. scopes distincts) ne sont JAMAIS dédupliqués.
    /// Retourne callId → callId canonique (clé absente = appel unique ou premier de son groupe).
    /// </summary>
    public static Dictionary<string, string> MapDuplicateToolCalls(IReadOnlyList<OllamaToolCall> calls)
    {
        var canonicalByKey = new Dictionary<string, string>(StringComparer.Ordinal);
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var call in calls)
        {
            if (call.Function is null || string.IsNullOrEmpty(call.Id))
                continue;
            string argsJson;
            try
            {
                argsJson = JsonSerializer.Serialize(call.Function.Arguments);
            }
            catch (Exception)
            {
                continue; // arguments non sérialisables → jamais dédupliqué (prudence)
            }
            var key = call.Function.Name + "\n" + argsJson;
            if (canonicalByKey.TryGetValue(key, out var canonicalId))
                map[call.Id!] = canonicalId;
            else
                canonicalByKey[key] = call.Id!;
        }
        return map;
    }

    private static void EnsureToolCallIds(List<OllamaToolCall> calls)
    {
        for (var i = 0; i < calls.Count; i++)
        {
            if (string.IsNullOrEmpty(calls[i].Id))
                calls[i] = calls[i] with { Id = Guid.NewGuid().ToString("N")[..12] };
        }
    }

    private async Task<string> ResolveOpenRouterBaseUrlAsync(CancellationToken cancellationToken)
    {
        var row = await _tenantAiProviderRepository.GetByProviderKeyAsync("openrouter", cancellationToken);
        if (!string.IsNullOrWhiteSpace(row?.BaseUrl))
            return row.BaseUrl.TrimEnd('/');
        return _openRouterSettings.DefaultBaseUrl.TrimEnd('/');
    }

    /// <summary>
    /// Whitelist stricte des outils sûrs à exécuter EN PARALLÈLE pendant un même round.
    /// Critère : ne pas passer par <c>IMediator</c> / <c>DbContext</c> (qui est scoped et
    /// ne supporte pas la concurrence). Les autres tools (qui touchent la base via MediatR)
    /// restent exécutés séquentiellement pour préserver l'invariant EF Core.
    /// </summary>
    /// <summary>
    /// num_ctx du chat. Quand <c>FixedChatNumCtx &gt; 0</c> : valeur CONSTANTE identique à chaque requête
    /// (tours d'agent + synthèse finale) ⇒ Ollama ne recharge jamais le modèle. Sinon : calcul adaptatif historique.
    /// </summary>
    private int? ResolveChatNumCtx(int promptChars, int maxTokens, OllamaInferenceProfile? inferenceProfile) =>
        OllamaChatNumCtxResolver.ResolveForChat(_ollamaSettings, inferenceProfile, promptChars, maxTokens);

    /// <summary>
    /// Limite les tours agent en CPU pour les intentions simples (Sales, Stock, etc.) sans réduire
    /// les cas ambigus (Fallback, Forecasting) ni l'analyse écran.
    /// </summary>
    public static int ResolveMaxToolCallRounds(
        bool isScreenAnalysis,
        int screenAnalysisMaxRounds,
        int defaultMaxRounds,
        int cpuMaxToolCallRounds,
        AssistantMode assistantMode,
        AiToolIntentRouter.AiToolIntent toolIntent,
        OllamaInferenceProfile? inferenceProfile)
    {
        if (isScreenAnalysis)
            return Math.Clamp(screenAnalysisMaxRounds, 1, 20);

        var defaultRounds = Math.Clamp(defaultMaxRounds, 1, 20);
        if (inferenceProfile?.Device != OllamaInferenceDevice.CpuOnly)
            return defaultRounds;

        if (cpuMaxToolCallRounds <= 0)
            return defaultRounds;

        if (assistantMode is AssistantMode.Compliance or AssistantMode.ScreenAnalysis)
            return defaultRounds;

        if (toolIntent is AiToolIntentRouter.AiToolIntent.Fallback
            or AiToolIntentRouter.AiToolIntent.Forecasting)
            return defaultRounds;

        return Math.Clamp(cpuMaxToolCallRounds, 1, defaultRounds);
    }

    private OllamaOptions BuildChatOllamaOptions(
        double temperature,
        int maxTokens,
        int? numCtx,
        OllamaInferenceProfile? inferenceProfile)
    {
        var options = new OllamaOptions
        {
            Temperature = temperature,
            NumPredict = Math.Max(1, maxTokens),
            NumCtx = numCtx,
            NumThread = _ollamaSettings.NumThread,
            NumBatch = _ollamaSettings.NumBatch,
            Seed = _ollamaSettings.Seed,
            TopP = _ollamaSettings.TopP
        };

        return inferenceProfile?.ApplyTo(options) ?? options;
    }

    /// <inheritdoc cref="AssistantModeResolver.QueryLikelyMutating"/>
    public static bool QueryLikelyMutating(string? text) => AssistantModeResolver.QueryLikelyMutating(text);

    /// <summary>
    /// Vrai si le suffixe d'intent peut être appliqué pour ce scope : les suffixes Sales/Fallback citent
    /// get_sales_revenue, on ne les injecte que si l'outil appartient au catalogue du scope.
    /// </summary>
    public static bool ShouldApplyIntentSuffixForScope(
        AiToolIntentRouter.AiToolIntent toolIntent,
        AssistantAgentScope agentScope)
    {
        if (agentScope == AssistantAgentScope.None)
            return true;
        if (toolIntent is AiToolIntentRouter.AiToolIntent.Sales or AiToolIntentRouter.AiToolIntent.Fallback)
            return AiAgentScopeCatalog.GetToolNames(agentScope).Contains("get_sales_revenue");
        return true;
    }

    private static List<OllamaToolDefinition> BuildOllamaTools(
        AssistantMode mode,
        bool enableMutationTools,
        AiToolIntentRouter.AiToolIntent toolIntent,
        OllamaInferenceProfile? inferenceProfile,
        AssistantAgentScope agentScope = AssistantAgentScope.None)
    {
        var isCpuOnly = inferenceProfile?.Device == OllamaInferenceDevice.CpuOnly;
        var isScoped = mode == AssistantMode.Default && agentScope != AssistantAgentScope.None;
        // Assistant expert : le catalogue scopé est déjà restreint — le filtrage par intent mot-clé est
        // neutralisé (Fallback), sauf Greeting (aucun outil) et Synthesis (réponse depuis l'historique).
        // toolIntent lui-même n'est PAS modifié en amont : ResolveMaxToolCallRounds garde ainsi la même
        // limite CPU (1 round) que l'assistant global pour les mêmes questions.
        var effectiveIntent = isScoped
            && toolIntent is not (AiToolIntentRouter.AiToolIntent.Greeting or AiToolIntentRouter.AiToolIntent.Synthesis)
            ? AiToolIntentRouter.AiToolIntent.Fallback
            : toolIntent;
        var useCpuCoreSubset = isCpuOnly && !isScoped && effectiveIntent == AiToolIntentRouter.AiToolIntent.Fallback;
        var useCpuIntentSubset = isCpuOnly && !isScoped && effectiveIntent is AiToolIntentRouter.AiToolIntent.Sales
            or AiToolIntentRouter.AiToolIntent.Stock
            or AiToolIntentRouter.AiToolIntent.Accounting;
        var definitions = AiToolRegistry.GetDefinitionsForMode(mode, enableMutationTools, agentScope)
            .Where(tool => AiToolIntentRouter.ShouldIncludeTool(
                tool.Name,
                effectiveIntent,
                enableMutationTools,
                tool.IsMutating,
                useCpuCoreSubset,
                useCpuIntentSubset))
            .ToList();

        if (isScoped)
        {
            if (isCpuOnly && effectiveIntent == AiToolIntentRouter.AiToolIntent.Fallback)
            {
                // CPU : variante minimale du scope (≤ 8 outils), même esprit que les Cpu*ToolNames de l'intent router.
                var cpuScopeTools = AiAgentScopeCatalog.GetCpuToolNames(agentScope);
                definitions = definitions.Where(t => cpuScopeTools.Contains(t.Name)).ToList();
            }
            else if (effectiveIntent == AiToolIntentRouter.AiToolIntent.Synthesis && definitions.Count < 3)
            {
                // Synthesis ∩ scope trop étroit : repli déterministe sur la variante CPU du scope (lecture seule).
                var cpuScopeTools = AiAgentScopeCatalog.GetCpuToolNames(agentScope);
                definitions = AiToolRegistry.GetDefinitionsForMode(mode, enableMutationTools, agentScope)
                    .Where(t => cpuScopeTools.Contains(t.Name))
                    .ToList();
            }
        }

        return definitions
            .Select(tool => new OllamaToolDefinition
        {
            Type = "function",
            Function = new OllamaFunctionDefinition
            {
                Name = tool.Name,
                Description = tool.Description,
                Parameters = new OllamaFunctionParameters
                {
                    Type = "object",
                    Properties = tool.Parameters.ToDictionary(
                        p => p.Key,
                        p => new OllamaParameterProperty
                        {
                            Type = p.Value.Type,
                            Description = p.Value.Description,
                            Enum = p.Value.AllowedValues
                        }),
                    Required = tool.RequiredParameters
                }
            }
        }).ToList();
    }
}
