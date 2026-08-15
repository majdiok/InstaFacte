using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Treasury;

/// <summary>
/// Élément d'analyse attaché à une projection : alerte datée, facteur d'influence ou
/// recommandation. Alimente les panneaux « Alertes &amp; recommandations » et « Facteurs
/// d'influence » de l'écran.
/// </summary>
/// <remarks>
/// <see cref="Origin"/> distingue ce qu'a produit une règle déterministe de ce qu'a rédigé le
/// modèle. L'écran doit rester capable d'afficher les alertes de règle même quand le modèle est
/// indisponible : c'est ce qui garantit qu'une rupture de trésorerie est signalée en toutes
/// circonstances.
/// </remarks>
public sealed class CashFlowForecastInsight : Entity
{
    public Guid ForecastRunId { get; private set; }

    public CashFlowInsightKind Kind { get; private set; }

    public CashFlowInsightSeverity Severity { get; private set; }

    public CashFlowInsightOrigin Origin { get; private set; }

    /// <summary>Intensité de l'effet, renseignée pour les facteurs d'influence.</summary>
    public CashFlowImpactLevel? Impact { get; private set; }

    /// <summary>Sens de l'effet sur le solde : Inflow pousse à la hausse, Outflow à la baisse.</summary>
    public CashFlowDirection? ImpactDirection { get; private set; }

    public string Title { get; private set; } = null!;

    public string? Detail { get; private set; }

    /// <summary>Mois concerné, quand l'analyse porte sur une période précise.</summary>
    public DateTime? PeriodStart { get; private set; }

    /// <summary>Solde estimé au moment signalé, pour les alertes de seuil.</summary>
    public decimal? EstimatedBalance { get; private set; }

    /// <summary>Ordre d'affichage : les alertes les plus graves d'abord.</summary>
    public int SortOrder { get; private set; }

    private CashFlowForecastInsight() { }

    public static CashFlowForecastInsight Alert(
        CashFlowInsightSeverity severity,
        string title,
        string? detail,
        DateTime? periodStart,
        decimal? estimatedBalance,
        CashFlowInsightOrigin origin,
        int sortOrder)
        => Create(
            CashFlowInsightKind.Alert,
            severity,
            origin,
            title,
            detail,
            sortOrder,
            periodStart: periodStart,
            estimatedBalance: estimatedBalance);

    public static CashFlowForecastInsight Driver(
        string title,
        string? detail,
        CashFlowImpactLevel impact,
        CashFlowDirection impactDirection,
        CashFlowInsightOrigin origin,
        int sortOrder)
        => Create(
            CashFlowInsightKind.Driver,
            CashFlowInsightSeverity.Info,
            origin,
            title,
            detail,
            sortOrder,
            impact: impact,
            impactDirection: impactDirection);

    public static CashFlowForecastInsight Recommendation(
        string title,
        string? detail,
        CashFlowInsightOrigin origin,
        int sortOrder)
        => Create(
            CashFlowInsightKind.Recommendation,
            CashFlowInsightSeverity.Info,
            origin,
            title,
            detail,
            sortOrder);

    private static CashFlowForecastInsight Create(
        CashFlowInsightKind kind,
        CashFlowInsightSeverity severity,
        CashFlowInsightOrigin origin,
        string title,
        string? detail,
        int sortOrder,
        CashFlowImpactLevel? impact = null,
        CashFlowDirection? impactDirection = null,
        DateTime? periodStart = null,
        decimal? estimatedBalance = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Le titre de l'analyse est obligatoire.", nameof(title));

        return new CashFlowForecastInsight
        {
            Kind = kind,
            Severity = severity,
            Origin = origin,
            Impact = impact,
            ImpactDirection = impactDirection,
            Title = Truncate(title.Trim(), 200)!,
            Detail = Truncate(detail, 1000),
            PeriodStart = periodStart?.Date,
            EstimatedBalance = estimatedBalance,
            SortOrder = sortOrder
        };
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
