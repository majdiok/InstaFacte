namespace FactuTrust.Domain.Enums;

/// <summary>
/// Sens d'un flux de trésorerie attendu.
/// </summary>
public enum CashFlowDirection
{
    /// <summary>Encaissement — augmente le solde.</summary>
    Inflow = 1,

    /// <summary>Décaissement — diminue le solde.</summary>
    Outflow = 2
}

/// <summary>
/// Origine métier d'un flux prévisionnel. Chaque valeur correspond à un collecteur dédié, ce qui
/// permet de tracer d'où vient chaque ligne et de désactiver une source sans toucher aux autres.
/// </summary>
public enum CashFlowSourceType
{
    /// <summary>Facture client non soldée, projetée à son échéance ajustée du retard observé.</summary>
    ClientInvoice = 1,

    /// <summary>Effet de commerce client en portefeuille (traite) — date contractuelle.</summary>
    ClientEffet = 2,

    /// <summary>Commande client confirmée non encore facturée. Source optionnelle.</summary>
    SalesOrderBacklog = 3,

    /// <summary>Facture fournisseur non soldée, nette de retenue à la source.</summary>
    SupplierInvoice = 4,

    /// <summary>Effet de commerce fournisseur à payer.</summary>
    SupplierEffet = 5,

    /// <summary>Bon de commande fournisseur confirmé non facturé. Source optionnelle.</summary>
    PurchaseOrderCommitment = 6,

    /// <summary>Net à payer d'un cycle de paie (dette 421 constatée ou cycle projeté).</summary>
    Payroll = 7,

    /// <summary>Charges sociales et retenues à reverser (CNSS 453, État 432).</summary>
    PayrollContribution = 8,

    /// <summary>Obligation de l'échéancier fiscal (TVA, RS, acomptes…).</summary>
    FiscalObligation = 9,

    /// <summary>Échéance d'emprunt (capital + intérêts).</summary>
    LoanInstallment = 10,

    /// <summary>Engagement récurrent saisi manuellement (loyer, abonnement, traite…).</summary>
    RecurringCommitment = 11,

    /// <summary>Modèle d'écriture récurrente comptable. Source optionnelle.</summary>
    RecurringJournalTemplate = 12,

    /// <summary>Flux ajouté à la main dans une simulation.</summary>
    Manual = 99
}

/// <summary>
/// Les trois scénarios présentés à l'écran.
/// </summary>
public enum CashFlowScenarioKind
{
    /// <summary>Encaissements à la date contractuelle, défaut minimal.</summary>
    Optimistic = 1,

    /// <summary>Somme pondérée par les probabilités observées — le scénario de référence.</summary>
    Realistic = 2,

    /// <summary>Encaissements décalés du retard P90, décaissements à l'heure.</summary>
    Pessimistic = 3
}

/// <summary>
/// Nature d'un élément d'analyse attaché à une projection.
/// </summary>
public enum CashFlowInsightKind
{
    /// <summary>Signal daté appelant une décision (tension, rupture, encaissement majeur).</summary>
    Alert = 1,

    /// <summary>Facteur d'influence détecté (saisonnalité, concentration d'échéances…).</summary>
    Driver = 2,

    /// <summary>Action suggérée (relancer, décaler, négocier un délai).</summary>
    Recommendation = 3
}

/// <summary>
/// Gravité d'une alerte de trésorerie.
/// </summary>
public enum CashFlowInsightSeverity
{
    /// <summary>Information — aucune action requise.</summary>
    Info = 1,

    /// <summary>Vigilance — le solde passe sous le seuil d'alerte.</summary>
    Warning = 2,

    /// <summary>Critique — rupture prévue ou franchissement du seuil critique.</summary>
    Critical = 3
}

/// <summary>
/// Qui a produit un élément d'analyse — déterminant pour l'auditabilité vis-à-vis d'un comptable.
/// </summary>
public enum CashFlowInsightOrigin
{
    /// <summary>Règle déterministe du moteur (seuils, concentration, solde négatif).</summary>
    Rule = 1,

    /// <summary>Rédigé par le modèle de langage à partir du résultat déterministe.</summary>
    Ai = 2
}

/// <summary>
/// Intensité de l'effet d'un facteur d'influence sur la projection.
/// </summary>
public enum CashFlowImpactLevel
{
    Low = 1,
    Medium = 2,
    High = 3
}

/// <summary>
/// Provenance de la probabilité affichée sur un scénario.
/// </summary>
/// <remarks>
/// Le module conserve toujours la valeur déterministe à côté de la valeur affichée : un
/// expert-comptable doit pouvoir constater ce que le moteur seul aurait produit.
/// </remarks>
public enum CashFlowProbabilitySource
{
    /// <summary>Probabilité issue du moteur statistique seul.</summary>
    Deterministic = 1,

    /// <summary>Probabilité déterministe ajustée par le modèle, dans la borne configurée.</summary>
    AiAdjusted = 2
}

/// <summary>
/// Périodicité d'un engagement de trésorerie récurrent.
/// </summary>
public enum CashCommitmentFrequency
{
    Weekly = 1,
    Monthly = 2,
    Quarterly = 3,
    SemiAnnual = 4,
    Annual = 5
}

/// <summary>
/// État d'exécution d'un calcul de projection.
/// </summary>
public enum CashFlowForecastRunStatus
{
    /// <summary>Calcul en cours.</summary>
    Running = 1,

    /// <summary>Calcul abouti — le run est exploitable.</summary>
    Computed = 2,

    /// <summary>Calcul interrompu par une erreur ; conservé pour diagnostic.</summary>
    Failed = 3
}
