namespace FactuTrust.Application.Features.AI.DTOs;

public enum AssistantMode
{
    Default = 0,
    Compliance = 1,
    ScreenAnalysis = 2,

    /// <summary>
    /// Focused Studio "AI builder" surface: only the low-code generation/report/extraction tools are
    /// exposed (isolated from the general assistant catalogue for small-model reliability).
    /// </summary>
    StudioBuilder = 3
}

/// <summary>
/// Expert « métier » optionnel de l'assistant (assistants par module). Orthogonal à <see cref="AssistantMode"/> :
/// ne s'applique qu'en mode Default (ignoré en ScreenAnalysis/Compliance/StudioBuilder qui ont déjà leur
/// catalogue focalisé). None = assistant global, comportement historique inchangé.
/// </summary>
public enum AssistantAgentScope
{
    None = 0,
    Sales = 1,
    Purchases = 2,
    Stock = 3,
    Accounting = 4,
    Treasury = 5,
    Crm = 6,

    /// <summary>
    /// Agent « Chef de mission » : seul scope au périmètre du CABINET et non d'un dossier.
    /// Ses outils lisent le portefeuille de dossiers via la base master et un fan-out borné, sans
    /// jamais passer par <c>ITenantContext</c>. Réservé au cabinet en mode natif (jamais délégué,
    /// cf. <c>FirmDelegatedAiScopePolicy</c>).
    /// </summary>
    FirmMission = 7
}

public sealed record ChatUiContextEntityDto
{
    public string Type { get; init; } = "";
    public string Id { get; init; } = "";
}

/// <summary>Optional snapshot of the Angular screen when the user sends a message (not persisted).</summary>
public sealed record ChatUiContextDto
{
    public string? Route { get; init; }
    public Dictionary<string, string>? PathParams { get; init; }
    public Dictionary<string, string>? QueryParams { get; init; }
    public string? ScreenId { get; init; }
    public ChatUiContextEntityDto? Entity { get; init; }
    /// <summary>Non-contractual JSON snapshot of on-screen data for analysis (volatile, truncated when formatted).</summary>
    public string? AnalysisSummary { get; init; }
}

public sealed record ChatRequestOptionsDto
{
    public AssistantMode AssistantMode { get; init; } = AssistantMode.Default;

    /// <summary>
    /// Vrai quand le message est un suivi conversationnel (clic sur un prompt suggéré) : le backend
    /// route alors vers l'intent Synthesis (catalogue restreint) pour répondre à partir de l'analyse
    /// déjà présente dans l'historique, au lieu de dériver vers des outils hors-contexte.
    /// Rétro-compatible : défaut false.
    /// </summary>
    public bool ConversationalFollowUp { get; init; }

    /// <summary>
    /// Expert de module demandé (assistants par module). Rétro-compatible : défaut None = assistant global.
    /// </summary>
    public AssistantAgentScope AgentScope { get; init; } = AssistantAgentScope.None;

    /// <summary>
    /// Force la lecture seule structurelle pour cette requête : les outils mutants (IsMutating)
    /// sont exclus du catalogue offert au modèle, quel que soit le gating dynamique
    /// (<c>Ollama.EnableMutationTools</c> + détection d'intention). Utilisé par les canaux externes
    /// (WhatsApp…) où aucune mutation n'est autorisée. Rétro-compatible : défaut false = chemin
    /// web historique inchangé.
    /// </summary>
    public bool ForceReadOnlyTools { get; init; }
}

/// <summary>HTTP body for POST /api/ai/chat (all new fields optional for backward compatibility).</summary>
public sealed record AiChatHttpRequestDto
{
    public Guid? ConversationId { get; init; }
    public required string Message { get; init; }
    public string? Model { get; init; }
    public ChatUiContextDto? UiContext { get; init; }
    public ChatRequestOptionsDto? Options { get; init; }
    /// <summary>Pièces jointes in-flight (images base64 pour modèles vision). Non persistées.</summary>
    public IReadOnlyList<ChatAttachmentInput>? Attachments { get; init; }
}

/// <summary>
/// Corps de POST /api/firm/ai/reminders/confirm. Le nonce n'est jamais transporté ailleurs que
/// dans l'événement SSE <c>client_actions</c> (côté serveur → client) et ce corps de requête
/// (côté client → serveur) — jamais en URL ni en queryParam.
/// </summary>
public sealed record ConfirmFirmReminderRequestDto
{
    public required string Nonce { get; init; }
}

public sealed record AiDocumentExtractResponseDto
{
    public string Text { get; init; } = "";
    public bool Truncated { get; init; }
    public string FileName { get; init; } = "";

    // ── Champs étendus (ajoutés, rétro-compatibles : les anciens clients les ignorent) ──
    /// <summary>Format détecté : "pdf" | "image" | "docx" | "xlsx" | "csv" | "txt".</summary>
    public string Format { get; init; } = "";
    /// <summary>Vrai si au moins une page a été extraite via OCR.</summary>
    public bool OcrApplied { get; init; }
    /// <summary>Nombre de pages effectivement traitées.</summary>
    public int PageCount { get; init; }
    /// <summary>Taille du fichier source en octets.</summary>
    public long SizeBytes { get; init; }
    /// <summary>Pages individuelles avec leur texte et leur rendu image (si demandé).</summary>
    public IReadOnlyList<AiDocumentExtractPageDto> Pages { get; init; } = Array.Empty<AiDocumentExtractPageDto>();
    /// <summary>Avertissements informatifs (PDF protégé partiellement, pages OCR, troncature…).</summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

public sealed record AiDocumentExtractPageDto
{
    public int PageIndex { get; init; }
    public string Text { get; init; } = "";
    /// <summary>PNG encodé en base64 sans préfixe data:. Présent uniquement si renderImages=true a été demandé.</summary>
    public string? ImageBase64 { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public bool OcrApplied { get; init; }
}
