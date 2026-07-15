using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities.Forecasting;

/// <summary>
/// A persisted sales-revenue forecast for a given scope (global, category, product, warehouse, client),
/// over a defined period, produced by a deterministic statistical method and optionally augmented by the
/// Tunisian commercial calendar. The LLM never invents numbers — it reads these rows.
/// </summary>
public sealed class SalesForecast : Entity
{
    public ForecastScopeType ScopeType { get; private set; }

    /// <summary>Optional ID identifying the scope (Product/Category/Warehouse/Client). Null when ScopeType=Global.</summary>
    public Guid? ScopeId { get; private set; }

    public ForecastHorizon Horizon { get; private set; }

    public DateTime GeneratedAt { get; private set; }

    /// <summary>Inclusive start of the forecasted period (UTC date, midnight).</summary>
    public DateTime PeriodStart { get; private set; }

    /// <summary>Inclusive end of the forecasted period (UTC date, midnight).</summary>
    public DateTime PeriodEnd { get; private set; }

    /// <summary>Point estimate of expected revenue for the period.</summary>
    public Money ExpectedAmount { get; private set; } = null!;

    /// <summary>Lower bound of the prediction interval (typically 95% via Z=1.96).</summary>
    public Money LowAmount { get; private set; } = null!;

    /// <summary>Upper bound of the prediction interval.</summary>
    public Money HighAmount { get; private set; } = null!;

    /// <summary>Subjective confidence percentage in [0..100]. Capped at 40 when method = CalendarHeuristic.</summary>
    public decimal ConfidencePercent { get; private set; }

    public ForecastMethod MethodUsed { get; private set; }

    /// <summary>JSON snapshot of inputs (history length, alpha/beta if Holt, seasonal factors applied, …) for auditability.</summary>
    public string? InputsJson { get; private set; }

    /// <summary>Free-text human-readable notes (e.g. "Pic Ramadan +60% appliqué", "Historique &lt;6 mois — précision limitée").</summary>
    public string? Notes { get; private set; }

    private SalesForecast() { }

    public static SalesForecast Create(
        ForecastScopeType scopeType,
        Guid? scopeId,
        ForecastHorizon horizon,
        DateTime periodStart,
        DateTime periodEnd,
        Money expected,
        Money low,
        Money high,
        decimal confidencePercent,
        ForecastMethod method,
        string? inputsJson = null,
        string? notes = null)
    {
        if (periodEnd < periodStart)
            throw new ArgumentException("PeriodEnd must be ≥ PeriodStart", nameof(periodEnd));

        if (expected.Currency != low.Currency || expected.Currency != high.Currency)
            throw new ArgumentException("Expected/Low/High must share the same currency");

        if (low.Amount > expected.Amount || expected.Amount > high.Amount)
            throw new ArgumentException("Confidence interval must satisfy Low ≤ Expected ≤ High");

        if (confidencePercent < 0 || confidencePercent > 100)
            throw new ArgumentException("ConfidencePercent must be in [0..100]", nameof(confidencePercent));

        // CalendarHeuristic is a fallback for short histories: its confidence is capped to avoid misleading the user.
        var effectiveConfidence = method == ForecastMethod.CalendarHeuristic && confidencePercent > 40m
            ? 40m
            : confidencePercent;

        return new SalesForecast
        {
            ScopeType = scopeType,
            ScopeId = scopeId,
            Horizon = horizon,
            GeneratedAt = DateTime.UtcNow,
            PeriodStart = periodStart.Date,
            PeriodEnd = periodEnd.Date,
            ExpectedAmount = expected,
            LowAmount = low,
            HighAmount = high,
            ConfidencePercent = effectiveConfidence,
            MethodUsed = method,
            InputsJson = inputsJson,
            Notes = notes
        };
    }
}
