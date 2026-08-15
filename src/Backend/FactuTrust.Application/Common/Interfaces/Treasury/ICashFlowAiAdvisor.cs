using FactuTrust.Domain.Entities.Treasury;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces.Treasury;

/// <summary>Analyse rédigée par le modèle, avant application des garde-fous.</summary>
/// <remarks>
/// Contrat volontairement dépourvu de tout type de fournisseur LLM : la logique de bornage et de
/// renormalisation se teste ainsi sans jamais appeler un modèle.
/// </remarks>
public sealed record CashFlowAiAnalysis
{
    /// <summary>Probabilités proposées par scénario, en points de pourcentage.</summary>
    public IReadOnlyDictionary<CashFlowScenarioKind, decimal> ScenarioProbabilities { get; init; } =
        new Dictionary<CashFlowScenarioKind, decimal>();

    /// <summary>Justification par scénario.</summary>
    public IReadOnlyDictionary<CashFlowScenarioKind, string?> ScenarioRationales { get; init; } =
        new Dictionary<CashFlowScenarioKind, string?>();

    public IReadOnlyList<CashFlowAiInsight> Alerts { get; init; } = Array.Empty<CashFlowAiInsight>();
    public IReadOnlyList<CashFlowAiInsight> Drivers { get; init; } = Array.Empty<CashFlowAiInsight>();
    public IReadOnlyList<CashFlowAiInsight> Recommendations { get; init; } = Array.Empty<CashFlowAiInsight>();
}

/// <summary>Élément d'analyse proposé par le modèle.</summary>
public sealed record CashFlowAiInsight
{
    public required string Title { get; init; }
    public string? Detail { get; init; }
    public CashFlowInsightSeverity Severity { get; init; } = CashFlowInsightSeverity.Info;
    public CashFlowImpactLevel? Impact { get; init; }
    public CashFlowDirection? ImpactDirection { get; init; }
    public DateTime? PeriodStart { get; init; }
}

/// <summary>
/// Couche IA du prévisionnel : pondère les scénarios et rédige alertes, facteurs d'influence et
/// recommandations à partir du résultat déterministe.
/// </summary>
/// <remarks>
/// Le modèle ne reçoit ni ne renvoie de montant à modifier. Toute défaillance — indisponibilité,
/// délai dépassé, JSON invalide — se traduit par un retour <c>null</c> : le recalcul aboutit alors
/// avec les seules valeurs déterministes, sans jamais échouer.
/// </remarks>
public interface ICashFlowAiAdvisor
{
    Task<CashFlowAiAnalysis?> AnalyzeAsync(
        CashFlowForecastRun run,
        CancellationToken cancellationToken = default);
}
