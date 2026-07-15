using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.Forecasting;

/// <summary>
/// Throttle / audit row recording each manual or scheduled recompute trigger for the forecasting engine.
/// Used to enforce the "MaxRecomputeRunsPerDay" rate limit per tenant and to surface a runbook of past runs.
/// </summary>
public sealed class ForecastRecomputeAudit : Entity
{
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    /// <summary>"Manual" (user clicked) or "Scheduled" (background service).</summary>
    public string TriggerType { get; private set; } = "Scheduled";

    /// <summary>UserId that triggered a manual recompute. Null for scheduled.</summary>
    public string? TriggeredBy { get; private set; }

    public int ForecastsGenerated { get; private set; }
    public int ReplenishmentsGenerated { get; private set; }
    public int PromotionsGenerated { get; private set; }
    public int ClassificationsUpdated { get; private set; }

    public bool Success { get; private set; }
    public string? ErrorMessage { get; private set; }

    public long DurationMs { get; private set; }

    private ForecastRecomputeAudit() { }

    public static ForecastRecomputeAudit StartManual(string triggeredBy)
    {
        if (string.IsNullOrWhiteSpace(triggeredBy))
            throw new ArgumentException("TriggeredBy is required", nameof(triggeredBy));

        return new ForecastRecomputeAudit
        {
            StartedAt = DateTime.UtcNow,
            TriggerType = "Manual",
            TriggeredBy = triggeredBy
        };
    }

    public static ForecastRecomputeAudit StartScheduled() => new()
    {
        StartedAt = DateTime.UtcNow,
        TriggerType = "Scheduled"
    };

    public void Complete(int forecasts, int replenishments, int promotions, int classifications)
    {
        CompletedAt = DateTime.UtcNow;
        ForecastsGenerated = Math.Max(0, forecasts);
        ReplenishmentsGenerated = Math.Max(0, replenishments);
        PromotionsGenerated = Math.Max(0, promotions);
        ClassificationsUpdated = Math.Max(0, classifications);
        DurationMs = (long)(CompletedAt.Value - StartedAt).TotalMilliseconds;
        Success = true;
    }

    public void Fail(string errorMessage)
    {
        CompletedAt = DateTime.UtcNow;
        DurationMs = (long)(CompletedAt.Value - StartedAt).TotalMilliseconds;
        Success = false;
        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? "Unknown error" : errorMessage.Trim();
    }
}
