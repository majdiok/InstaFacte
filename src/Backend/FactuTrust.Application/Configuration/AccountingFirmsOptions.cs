namespace FactuTrust.Application.Configuration;

public sealed class AccountingFirmsOptions
{
    public const string SectionName = "Features:AccountingFirms";

    public bool Enabled { get; set; }

    /// <summary>
    /// When true, firm users in delegated client context receive <c>ai:chat</c> and
    /// <see cref="Enums.AppModule.AI"/> for accounting assistant and document import.
    /// </summary>
    public bool AiAccountingEnabled { get; set; } = true;

    /// <summary>
    /// Active l'agent « Chef de mission » : permission <c>firm:ai:chat</c> dans le JWT cabinet natif
    /// et surface HTTP <c>/api/firm/ai</c>. OFF par défaut — l'activation est une décision explicite.
    /// </summary>
    public bool FirmAgentEnabled { get; set; }

    /// <summary>
    /// Autorise l'outil de relance d'échéance de l'agent (permission <c>firm:ai:remind</c>,
    /// responsable de cabinet uniquement). Sans effet si <see cref="FirmAgentEnabled"/> est faux.
    /// Rappel : l'outil étant mutant, il reste aussi soumis à <c>Ollama.EnableMutationTools</c>.
    /// </summary>
    public bool FirmAgentReminderToolEnabled { get; set; }

    /// <summary>
    /// Quand vrai (défaut), l'outil de relance de l'agent ne fait plus qu'une PREVIEW : il valide
    /// tous les gardes-fous puis retourne une action en attente qu'un endpoint de confirmation
    /// dédié doit consommer pour déclencher l'envoi réel. À faux, comportement historique : l'outil
    /// envoie directement (réversibilité explicite).
    /// </summary>
    public bool FirmAgentReminderRequiresConfirmation { get; set; } = true;

    /// <summary>Active le brief quotidien poussé au responsable de cabinet (job Hangfire).</summary>
    public bool FirmAgentDailyBriefingEnabled { get; set; }

    /// <summary>
    /// Degré de parallélisme du fan-out sur les bases dossiers. Borné à [1, 16] à la lecture :
    /// chaque unité est une connexion SQL simultanée supplémentaire.
    /// </summary>
    public int FirmAgentMaxParallelDossiers { get; set; } = 4;

    /// <summary>
    /// Active le réviseur de portefeuille : permissions <c>firm:revision:*</c> dans le JWT cabinet
    /// natif et surface HTTP <c>/api/firm/revision</c>. OFF par défaut — l'activation est une
    /// décision explicite, et les utilisateurs doivent se reconnecter pour recevoir les permissions.
    /// </summary>
    public bool FirmRevisionEnabled { get; set; }

    /// <summary>
    /// Active le balayage nocturne du portefeuille (job Hangfire). Sans effet si
    /// <see cref="FirmRevisionEnabled"/> est faux. Un balayage ouvre une connexion par dossier :
    /// à n'activer qu'après avoir mesuré la charge sur un cabinet représentatif.
    /// </summary>
    public bool FirmRevisionSweepEnabled { get; set; }

    /// <summary>
    /// Autorise la rédaction du dossier de révision par le modèle de langage. OFF = notes purement
    /// déterministes, qui restent complètes et exploitables.
    /// </summary>
    public bool FirmRevisionAiEnabled { get; set; }

    /// <summary>
    /// Délai maximal accordé à la rédaction, en secondes. Dépassement ⇒ repli déterministe
    /// silencieux : une note n'échoue jamais à cause du modèle.
    /// </summary>
    public int FirmRevisionAiTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Nombre d'anomalies transmises au modèle pour rédaction. Au-delà, les anomalies restantes
    /// gardent leur description déterministe : mieux vaut une note complète dont une partie est
    /// générée qu'un prompt tronqué au milieu d'une liste.
    /// </summary>
    public int FirmRevisionMaxAnomaliesInPrompt { get; set; } = 40;
}
