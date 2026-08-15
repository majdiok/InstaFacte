namespace FactuTrust.Application.Configuration;

/// <summary>
/// Configuration du module « Trésorerie prévisionnelle par IA » (projection du solde de
/// trésorerie, scénarios probabilisés, alertes de tension, engagements récurrents).
/// Liée à la section "TreasuryForecast" de appsettings.json.
/// </summary>
/// <remarks>
/// Même contrat de sûreté que <see cref="ForecastingOptions"/> : quand <see cref="Enabled"/> vaut
/// false, aucun service n'est enregistré, aucun outil IA n'est exposé, aucune tâche de fond ne
/// tourne et l'API répond 503. Défaut à false pour garantir un impact nul sur les tenants
/// existants tant que l'activation n'est pas explicite.
/// </remarks>
public sealed class TreasuryForecastOptions
{
    public const string SectionName = "TreasuryForecast";

    /// <summary>Interrupteur maître du module.</summary>
    public bool Enabled { get; set; }

    /// <summary>Horizon retenu quand l'appelant n'en précise aucun.</summary>
    public int DefaultHorizonMonths { get; set; } = 6;

    /// <summary>Borne haute acceptée par l'API (400 au-delà).</summary>
    public int MaxHorizonMonths { get; set; } = 12;

    /// <summary>Score Z de l'intervalle de prévision (1.96 → 95 %, 1.65 → 90 %).</summary>
    public double ConfidenceZ { get; set; } = 1.96;

    /// <summary>
    /// Durée de validité d'un run avant qu'une lecture ne déclenche un recalcul implicite.
    /// </summary>
    public int CacheTtlMinutes { get; set; } = 30;

    /// <summary>
    /// Profondeur d'historique (en mois) utilisée pour mesurer la volatilité du flux net de
    /// trésorerie et le retard de paiement des clients.
    /// </summary>
    public int HistoryMonthsForVolatility { get; set; } = 24;

    /// <summary>Garde-fou : nombre maximal de recalculs manuels par tenant et par 24 h.</summary>
    public int MaxRecomputeRunsPerDay { get; set; } = 6;

    /// <summary>
    /// Jour du mois retenu pour projeter le paiement des cycles de paie futurs, quand aucun
    /// historique de date de règlement n'est exploitable.
    /// </summary>
    public int PayrollPaymentDayOfMonth { get; set; } = 28;

    /// <summary>
    /// Probabilité affectée aux effets de commerce clients en portefeuille (traites).
    /// Contractuellement datés, mais un impayé reste possible.
    /// </summary>
    public decimal ClientEffetProbabilityPercent { get; set; } = 95m;

    /// <summary>Probabilité affectée aux cycles de paie projetés (non encore calculés).</summary>
    public decimal ProjectedPayrollProbabilityPercent { get; set; } = 90m;

    /// <summary>
    /// Probabilité d'encaissement d'une créance client selon son ancienneté, par tranche.
    /// </summary>
    public ReceivableProbabilityCurve ReceivableProbability { get; set; } = new();

    /// <summary>
    /// Plafond, en jours, du décalage appliqué à une échéance client d'après le retard observé.
    /// Empêche qu'un litige ancien repousse un encaissement hors de tout horizon raisonnable.
    /// </summary>
    public int MaxPaymentDelayShiftDays { get; set; } = 180;

    /// <summary>
    /// Nombre minimal de factures soldées nécessaires pour retenir le retard médian d'un client
    /// plutôt que la médiane de la société. En deçà, un seul règlement tardif fausserait tout.
    /// </summary>
    public int MinInvoicesForClientDelayMedian { get; set; } = 3;

    /// <summary>
    /// Carnet de commandes clients (commandes confirmées non facturées) comme encaissement futur.
    /// Désactivé par défaut : risque de double comptage avec les factures déjà émises.
    /// </summary>
    public bool IncludeSalesOrderBacklog { get; set; }

    /// <summary>
    /// Bons de commande fournisseurs confirmés non facturés comme décaissement futur.
    /// Désactivé par défaut : même risque de double comptage.
    /// </summary>
    public bool IncludePurchaseOrderCommitments { get; set; }

    /// <summary>
    /// Modèles d'écritures récurrentes (<c>JournalEntryTemplate.NextRunDate</c>) comme flux futurs.
    /// Désactivé par défaut : toutes les écritures récurrentes ne sont pas des flux de trésorerie.
    /// </summary>
    public bool IncludeRecurringJournalTemplates { get; set; }

    /// <summary>
    /// Quand false, la tâche de fond de recalcul nocturne ne s'exécute pas, même si
    /// <see cref="Enabled"/> vaut true.
    /// </summary>
    public bool BackgroundRecomputeEnabled { get; set; }

    /// <summary>Couche IA — pondération des scénarios et rédaction des analyses.</summary>
    public AiCashForecastOptions Ai { get; set; } = new();
}

/// <summary>
/// Probabilité d'encaissement d'une créance client selon son ancienneté à la date de calcul.
/// </summary>
/// <remarks>
/// Courbe décroissante volontairement paramétrable plutôt que calibrée en dur : le taux de
/// recouvrement dépend du secteur et du portefeuille client. Les valeurs par défaut reflètent un
/// usage commercial ordinaire ; une société qui dispose d'un historique fiable les affine.
/// </remarks>
public sealed class ReceivableProbabilityCurve
{
    /// <summary>Facture non encore échue.</summary>
    public decimal NotDuePercent { get; set; } = 95m;

    /// <summary>Échue depuis 30 jours au plus.</summary>
    public decimal OverdueUpTo30Percent { get; set; } = 85m;

    /// <summary>Échue depuis 31 à 60 jours.</summary>
    public decimal Overdue31To60Percent { get; set; } = 70m;

    /// <summary>Échue depuis 61 à 90 jours.</summary>
    public decimal Overdue61To90Percent { get; set; } = 55m;

    /// <summary>Échue depuis plus de 90 jours.</summary>
    public decimal OverdueOver90Percent { get; set; } = 35m;

    /// <summary>Probabilité applicable à une créance échue depuis <paramref name="daysOverdue"/> jours.</summary>
    public decimal Resolve(int daysOverdue) => daysOverdue switch
    {
        <= 0 => NotDuePercent,
        <= 30 => OverdueUpTo30Percent,
        <= 60 => Overdue31To60Percent,
        <= 90 => Overdue61To90Percent,
        _ => OverdueOver90Percent
    };
}

/// <summary>
/// Paramètres de la couche IA du prévisionnel de trésorerie.
/// </summary>
/// <remarks>
/// Le LLM ne produit jamais un montant ni une date : il ajuste les probabilités des trois
/// scénarios, dans une borne étroite autour de la valeur déterministe, et rédige les alertes,
/// facteurs d'influence et recommandations. Toute défaillance retombe silencieusement sur le
/// résultat déterministe — un recalcul ne doit jamais échouer à cause du modèle.
/// </remarks>
public sealed class AiCashForecastOptions
{
    /// <summary>Active la pondération et la rédaction par le modèle.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Écart maximal, en points de pourcentage, entre la probabilité déterministe d'un scénario
    /// et celle proposée par le modèle. Au-delà, la valeur est ramenée à la borne.
    /// </summary>
    public int MaxProbabilityShiftPoints { get; set; } = 15;

    /// <summary>Délai au-delà duquel l'appel au modèle est abandonné (repli déterministe).</summary>
    public int TimeoutSeconds { get; set; } = 45;

    /// <summary>Nombre maximal d'alertes, facteurs et recommandations conservés par run.</summary>
    public int MaxInsights { get; set; } = 8;

    /// <summary>
    /// Nombre de flux les plus importants transmis au modèle pour contextualiser son analyse.
    /// </summary>
    public int TopFlowsInPrompt { get; set; } = 20;

    /// <summary>
    /// Référence de modèle forçant le choix du LLM (ex. <c>openrouter:…</c>). Quand null, le
    /// modèle par défaut de la plateforme est utilisé.
    /// </summary>
    public string? ModelOverride { get; set; }
}
