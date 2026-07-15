using FactuTrust.Application.Features.Forecasting.Dtos;

namespace FactuTrust.Application.Common.Interfaces.Forecasting;

/// <summary>
/// Orchestrates a full nightly recompute: classifications → forecasts → replenishments → promotions.
/// Throttled to MaxRecomputeRunsPerDay per tenant. Records every run in ForecastRecomputeAudits.
/// </summary>
public interface IForecastRecomputeOrchestrator
{
    Task<RecomputeResultDto> RecomputeAsync(string triggerType, string? triggeredBy, CancellationToken ct = default);
    Task<RecomputeAuditDto?> GetLastAuditAsync(CancellationToken ct = default);
}
