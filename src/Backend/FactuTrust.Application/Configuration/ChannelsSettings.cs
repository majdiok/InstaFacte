namespace FactuTrust.Application.Configuration;

/// <summary>
/// Réglages du canal WhatsApp (pont Node whatsapp-web.js piloté en processus enfant par l'API).
/// Tous les interrupteurs sont <c>false</c> par défaut : tant que la fonctionnalité n'est pas
/// activée, le pont n'est jamais démarré, les endpoints sont inertes et aucun message sortant n'est
/// émis (comportement historique strictement préservé).
/// </summary>
public sealed class ChannelsSettings
{
    public const string SectionName = "Channels";

    /// <summary>Interrupteur maître du module canaux. OFF = tout le chemin de code est inerte.</summary>
    public bool Enabled { get; set; }

    /// <summary>Active le canal WhatsApp (pont whatsapp-web.js). Requiert <see cref="Enabled"/>.</summary>
    public bool WhatsAppEnabled { get; set; }

    /// <summary>Longueur maximale d'un message entrant transmis au pipeline IA (troncature au-delà).</summary>
    public int MaxInboundMessageLength { get; set; } = 4000;

    /// <summary>Durée de validité (minutes) d'un code de liaison « LIER ».</summary>
    public int LinkCodeTtlMinutes { get; set; } = 10;

    /// <summary>Nombre maximal de tentatives erronées avant invalidation d'un code de liaison.</summary>
    public int LinkCodeMaxAttempts { get; set; } = 5;

    /// <summary>
    /// Taille maximale (caractères) d'un message WhatsApp sortant ; au-delà, la réponse est découpée
    /// en plusieurs messages sur des frontières de paragraphes.
    /// </summary>
    public int MaxOutboundMessageChars { get; set; } = 3500;

    /// <summary>
    /// Timeout (secondes) du traitement complet d'un message entrant (inférence IA comprise).
    /// À garder ≥ <c>Ollama.TimeoutSeconds</c> (l'inférence CPU peut durer plusieurs minutes).
    /// </summary>
    public int InboundProcessingTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Envoie aussi les rappels d'échéances fiscales par WhatsApp aux responsables dont le compte
    /// est lié. Additif : requiert <c>Accounting.FiscalEmailRemindersEnabled</c> (l'e-mail reste le
    /// canal primaire et porte l'anti-doublon).
    /// </summary>
    public bool FiscalWhatsAppRemindersEnabled { get; set; }

    // ── Pont Node (processus enfant) ──

    /// <summary>Exécutable Node à lancer (PATH ou chemin absolu). Défaut « node ».</summary>
    public string NodeExecutablePath { get; set; } = "node";

    /// <summary>
    /// Dossier du pont (contenant bridge.js + node_modules). Vide → défaut
    /// <c>Path.Combine(AppContext.BaseDirectory, "WhatsAppBridge")</c>.
    /// </summary>
    public string BridgeDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Dossier de persistance de la session WhatsApp (LocalAuth). Vide → défaut
    /// <c>Path.Combine(AppContext.BaseDirectory, "whatsapp-session")</c>.
    /// À sauvegarder / rendre persistant entre déploiements pour éviter un re-scan du QR.
    /// </summary>
    public string SessionDirectory { get; set; } = string.Empty;

    /// <summary>Démarre le pont automatiquement au boot (si la fonctionnalité est activée). Défaut true.</summary>
    public bool AutoStart { get; set; } = true;

    /// <summary>Timeout (secondes) d'un envoi sortant vers le pont (accusé « sent »).</summary>
    public int SendTimeoutSeconds { get; set; } = 30;

    /// <summary>Backoff minimal (secondes) avant redémarrage du pont après une sortie inattendue.</summary>
    public int RestartMinBackoffSeconds { get; set; } = 5;

    /// <summary>Backoff maximal (secondes) du redémarrage du pont (borne supérieure de l'exponentielle).</summary>
    public int RestartMaxBackoffSeconds { get; set; } = 300;
}
