namespace FactuTrust.Application.Configuration;

/// <summary>Feature flags and tuning for « Analyser avec l'assistant IA » screen analysis.</summary>
public sealed class ScreenAnalysisOptions
{
    public const string SectionName = "ScreenAnalysis";

    public bool Enabled { get; set; } = true;

    /// <summary>When true, uses enhanced per-screen prompts and structured output template.</summary>
    public bool EnhancedPromptsEnabled { get; set; } = true;

    /// <summary>When true, expects schemaVersion 2 payloads and deduplicates JSON from persisted user messages.</summary>
    public bool EnhancedPayloadsEnabled { get; set; } = true;

    /// <summary>When true, JSON snapshot is only in uiContext (system prompt), not duplicated in user message.</summary>
    public bool DeduplicateContextInMessage { get; set; } = true;

    /// <summary>When true, server may call complementary tools before the first LLM round.</summary>
    public bool ServerEnrichmentEnabled { get; set; } = true;

    /// <summary>When true, forces follow-up prompts and dashboard for numeric screens after LLM response.</summary>
    public bool PostProcessToolsEnabled { get; set; } = true;

    /// <summary>
    /// Quand true (défaut), exécute UNE passe de synthèse finale quand la boucle agent se termine
    /// sans prose visible suffisante — analyse d'écran uniquement (le mode Default a sa propre garde).
    /// Ne se déclenche que là où l'utilisateur obtiendrait sinon le bouchon « Analyse disponible… ».
    /// </summary>
    public bool ForceFinalSynthesisEnabled { get; set; } = true;

    public double Temperature { get; set; } = 0.15;

    public int MaxTokens { get; set; } = 6144;

    public int MaxToolCallRounds { get; set; } = 2;

    public int MaxAnalysisSummaryChars { get; set; } = 16384;

    /// <summary>Screen ids that should receive a dashboard KPI block when post-processing is enabled.</summary>
    public List<string> ForceDashboardForScreens { get; set; } =
    [
        "accounting-income-statement",
        "accounting-balance",
        "accounting-balance-sheet",
        "dashboard",
        "cash-desk"
    ];

    /// <summary>Per-screen rollout overrides (enhancedPrompt, enhancedPayload).</summary>
    public Dictionary<string, ScreenAnalysisScreenOverride> PerScreenOverrides { get; set; } = new();
}

public sealed class ScreenAnalysisScreenOverride
{
    public bool? EnhancedPrompt { get; set; }

    public bool? EnhancedPayload { get; set; }
}
