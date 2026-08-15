using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Treasury;

/// <summary>
/// Une projection de trésorerie calculée : solde d'ouverture ancré en comptabilité, flux attendus
/// datés, agrégats mensuels, scénarios probabilisés et analyses.
/// </summary>
/// <remarks>
/// <para>
/// Tous les montants sont des <see cref="decimal"/> signés et non des <c>Money</c> : un solde
/// prévisionnel peut être négatif, et <c>Money.Subtract</c> lève une exception dans ce cas.
/// </para>
/// <para>
/// Le modèle de langage ne produit jamais un montant ni une date sur ce run. Il n'ajuste que les
/// probabilités des <see cref="CashFlowScenario"/>, dans une borne configurée, et rédige les
/// <see cref="CashFlowForecastInsight"/>. <see cref="AiAdjustmentApplied"/> et
/// <see cref="AiModelRef"/> tracent son intervention.
/// </para>
/// </remarks>
public sealed class CashFlowForecastRun : Entity
{
    private readonly List<CashFlowForecastLine> _lines = new();
    private readonly List<CashFlowForecastBucket> _buckets = new();
    private readonly List<CashFlowScenario> _scenarios = new();
    private readonly List<CashFlowForecastInsight> _insights = new();

    /// <summary>Début inclus de la période projetée (date, minuit).</summary>
    public DateTime PeriodStart { get; private set; }

    /// <summary>Fin incluse de la période projetée (date, minuit).</summary>
    public DateTime PeriodEnd { get; private set; }

    /// <summary>Nombre de mois couverts, tel que demandé par l'appelant.</summary>
    public int HorizonMonths { get; private set; }

    /// <summary>Solde de trésorerie réel (classe 5) à <see cref="PeriodStart"/>.</summary>
    public decimal OpeningBalance { get; private set; }

    /// <summary>Somme des encaissements attendus, pondérée par leur probabilité.</summary>
    public decimal TotalInflows { get; private set; }

    /// <summary>Somme des décaissements attendus, pondérée par leur probabilité.</summary>
    public decimal TotalOutflows { get; private set; }

    /// <summary><see cref="TotalInflows"/> − <see cref="TotalOutflows"/>. Peut être négatif.</summary>
    public decimal NetFlow { get; private set; }

    /// <summary>Solde projeté à <see cref="PeriodEnd"/>. Peut être négatif.</summary>
    public decimal ClosingBalance { get; private set; }

    public string Currency { get; private set; } = "TND";

    /// <summary>Indice de confiance global dans [0..100].</summary>
    public decimal ConfidencePercent { get; private set; }

    /// <summary>Méthode statistique employée pour la volatilité et l'intervalle de prévision.</summary>
    public ForecastMethod MethodUsed { get; private set; }

    public CashFlowForecastRunStatus Status { get; private set; }

    public DateTime ComputedAt { get; private set; }

    /// <summary>Auteur du recalcul. Null quand le run provient de la tâche de fond.</summary>
    public Guid? ComputedByUserId { get; private set; }

    public int DurationMs { get; private set; }

    /// <summary>
    /// Empreinte JSON des paramètres et du volume collecté par source, pour rejouer et auditer
    /// un run a posteriori.
    /// </summary>
    public string? InputsJson { get; private set; }

    /// <summary>Message d'erreur quand <see cref="Status"/> vaut Failed.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>Vrai si les probabilités de scénario ont été ajustées par le modèle.</summary>
    public bool AiAdjustmentApplied { get; private set; }

    /// <summary>Référence canonique du modèle ayant produit l'ajustement (ex. <c>ollama:…</c>).</summary>
    public string? AiModelRef { get; private set; }

    public DateTime? AiAnalyzedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public IReadOnlyCollection<CashFlowForecastLine> Lines => _lines.AsReadOnly();
    public IReadOnlyCollection<CashFlowForecastBucket> Buckets => _buckets.AsReadOnly();
    public IReadOnlyCollection<CashFlowScenario> Scenarios => _scenarios.AsReadOnly();
    public IReadOnlyCollection<CashFlowForecastInsight> Insights => _insights.AsReadOnly();

    private CashFlowForecastRun() { }

    public static CashFlowForecastRun Start(
        DateTime periodStart,
        DateTime periodEnd,
        int horizonMonths,
        decimal openingBalance,
        string currency,
        Guid? computedByUserId)
    {
        if (periodEnd < periodStart)
            throw new ArgumentException("PeriodEnd doit être ≥ PeriodStart.", nameof(periodEnd));

        if (horizonMonths < 1)
            throw new ArgumentException("HorizonMonths doit être ≥ 1.", nameof(horizonMonths));

        return new CashFlowForecastRun
        {
            PeriodStart = periodStart.Date,
            PeriodEnd = periodEnd.Date,
            HorizonMonths = horizonMonths,
            OpeningBalance = MillimeRounding.Round(openingBalance),
            Currency = string.IsNullOrWhiteSpace(currency) ? "TND" : currency.Trim().ToUpperInvariant(),
            Status = CashFlowForecastRunStatus.Running,
            ComputedAt = DateTime.UtcNow,
            ComputedByUserId = computedByUserId,
            MethodUsed = ForecastMethod.Sma
        };
    }

    public void AddLine(CashFlowForecastLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        _lines.Add(line);
    }

    public void AddBucket(CashFlowForecastBucket bucket)
    {
        ArgumentNullException.ThrowIfNull(bucket);
        _buckets.Add(bucket);
    }

    public void AddScenario(CashFlowScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        _scenarios.Add(scenario);
    }

    public void AddInsight(CashFlowForecastInsight insight)
    {
        ArgumentNullException.ThrowIfNull(insight);
        _insights.Add(insight);
    }

    /// <summary>
    /// Clôt le calcul en figeant les agrégats. Le solde final est recalculé à partir du solde
    /// d'ouverture et du flux net, jamais recopié d'un DTO — c'est le seul point d'écriture.
    /// </summary>
    public void Complete(
        decimal totalInflows,
        decimal totalOutflows,
        decimal confidencePercent,
        ForecastMethod methodUsed,
        int durationMs,
        string? inputsJson)
    {
        if (confidencePercent < 0m || confidencePercent > 100m)
            throw new ArgumentException("ConfidencePercent doit être dans [0..100].", nameof(confidencePercent));

        TotalInflows = MillimeRounding.Round(totalInflows);
        TotalOutflows = MillimeRounding.Round(totalOutflows);
        NetFlow = MillimeRounding.Round(TotalInflows - TotalOutflows);
        ClosingBalance = MillimeRounding.Round(OpeningBalance + NetFlow);
        ConfidencePercent = confidencePercent;
        MethodUsed = methodUsed;
        DurationMs = durationMs < 0 ? 0 : durationMs;
        InputsJson = inputsJson;
        Status = CashFlowForecastRunStatus.Computed;
    }

    public void MarkFailed(string errorMessage, int durationMs)
    {
        Status = CashFlowForecastRunStatus.Failed;
        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? "Erreur inconnue." : errorMessage.Trim();
        DurationMs = durationMs < 0 ? 0 : durationMs;
    }

    /// <summary>
    /// Enregistre le fait que le modèle a produit une analyse. Appelé uniquement après que les
    /// scénarios ont accepté un ajustement borné : si le modèle échoue, ce marqueur reste faux et
    /// l'écran affiche les probabilités déterministes sans mention d'IA.
    /// </summary>
    public void MarkAiAnalyzed(string modelRef, bool probabilitiesAdjusted)
    {
        AiModelRef = string.IsNullOrWhiteSpace(modelRef) ? null : modelRef.Trim();
        AiAnalyzedAt = DateTime.UtcNow;
        AiAdjustmentApplied = probabilitiesAdjusted;
    }
}
