using FactuTrust.Application.Features.Forecasting.Dtos;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces.Forecasting;

/// <summary>
/// Top-level deterministic forecasting service. Always source-of-truth for revenue/demand forecasts.
/// Read-only — never mutates business data. Cached per tenant with TTL Forecasting:CacheTtlMinutes.
/// </summary>
public interface IForecastingService
{
    Task<SalesForecastDto> ForecastRevenueAsync(
        ForecastScopeType scopeType,
        Guid? scopeId,
        ForecastHorizon horizon,
        DateTime? customPeriodStart,
        DateTime? customPeriodEnd,
        CancellationToken ct = default);

    Task<ProductDemandForecastDto> ForecastProductDemandAsync(
        Guid productId,
        ForecastHorizon horizon,
        CancellationToken ct = default);

    Task<SeasonalImpactDto> AnalyzeSeasonalImpactAsync(
        Guid? productId,
        Guid? categoryId,
        DateTime periodStart,
        DateTime periodEnd,
        CancellationToken ct = default);

    Task<PromotionSimulationResultDto> SimulatePromotionImpactAsync(
        Guid productId,
        decimal discountPercent,
        int durationDays,
        CancellationToken ct = default);
}
