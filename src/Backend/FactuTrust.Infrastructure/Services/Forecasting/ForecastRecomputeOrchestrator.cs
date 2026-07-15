using FactuTrust.Application.Common.Interfaces.Forecasting;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Forecasting.Dtos;
using FactuTrust.Domain.Entities.Forecasting;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.Forecasting;

/// <summary>
/// Coordinates a full nightly recompute: ABC/XYZ → replenishments → promotions.
/// Throttled to <see cref="ForecastingOptions.MaxRecomputeRunsPerDay"/> when triggered manually.
/// Records every run in ForecastRecomputeAudits for observability.
/// </summary>
public sealed class ForecastRecomputeOrchestrator : IForecastRecomputeOrchestrator
{
    private readonly ITenantDbContextFactory _contextFactory;
    private readonly IAbcXyzClassifier _classifier;
    private readonly IReplenishmentService _replenishment;
    private readonly IPromotionRecommendationService _promotions;
    private readonly ForecastingOptions _options;
    private readonly ILogger<ForecastRecomputeOrchestrator> _logger;

    public ForecastRecomputeOrchestrator(
        ITenantDbContextFactory contextFactory,
        IAbcXyzClassifier classifier,
        IReplenishmentService replenishment,
        IPromotionRecommendationService promotions,
        IOptions<ForecastingOptions> options,
        ILogger<ForecastRecomputeOrchestrator> logger)
    {
        _contextFactory = contextFactory;
        _classifier = classifier;
        _replenishment = replenishment;
        _promotions = promotions;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<RecomputeResultDto> RecomputeAsync(string triggerType, string? triggeredBy, CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return new RecomputeResultDto(
                DateTime.UtcNow, DateTime.UtcNow, 0, 0, 0, 0, 0, false,
                "Module désactivé (Features:Forecasting:Enabled=false).");
        }

        // Throttle manual triggers (hosted-service triggers always run on schedule).
        if (string.Equals(triggerType, "Manual", StringComparison.OrdinalIgnoreCase) && _options.MaxRecomputeRunsPerDay > 0)
        {
            await using var checkCtx = _contextFactory.CreateContext();
            var since = DateTime.UtcNow.Date;
            var runsToday = await checkCtx.ForecastRecomputeAudits
                .CountAsync(a => a.TriggerType == "Manual" && a.StartedAt >= since, ct);
            if (runsToday >= _options.MaxRecomputeRunsPerDay)
            {
                throw new InvalidOperationException(
                    $"Le recalcul manuel est limité à {_options.MaxRecomputeRunsPerDay} fois par jour. Réessayez demain ou attendez le job nocturne.");
            }
        }

        var audit = string.Equals(triggerType, "Manual", StringComparison.OrdinalIgnoreCase)
            ? ForecastRecomputeAudit.StartManual(triggeredBy ?? "system")
            : ForecastRecomputeAudit.StartScheduled();
        await using (var ctxAudit = _contextFactory.CreateContext())
        {
            ctxAudit.ForecastRecomputeAudits.Add(audit);
            await ctxAudit.SaveChangesAsync(ct);
        }

        try
        {
            var classifications = await _classifier.ClassifyAsync(ct);
            // Daily orchestrator regenerates the whole tenant (warehouseId=null, productId=null).
            // The optional productId parameter is only used by the product-demand modal — fix F-C3.
            var replenishments = await _replenishment.GenerateRecommendationsAsync(
                warehouseId: null, productId: null, ct);
            var promotions = await _promotions.GenerateAsync(_options.DefaultHorizonDays, ct);

            audit.Complete(forecasts: 0, replenishments: replenishments, promotions: promotions, classifications: classifications);
            await using var ctxFinal = _contextFactory.CreateContext();
            ctxFinal.ForecastRecomputeAudits.Update(audit);
            await ctxFinal.SaveChangesAsync(ct);

            _logger.LogInformation("Forecast recompute completed in {Ms}ms. {Class} classifications, {Repl} replenishments, {Promo} promotions.",
                audit.DurationMs, classifications, replenishments, promotions);

            return new RecomputeResultDto(
                audit.StartedAt, audit.CompletedAt!.Value, audit.DurationMs,
                0, replenishments, promotions, classifications, true, null);
        }
        catch (Exception ex)
        {
            audit.Fail(ex.Message);
            await using var ctxErr = _contextFactory.CreateContext();
            ctxErr.ForecastRecomputeAudits.Update(audit);
            await ctxErr.SaveChangesAsync(ct);
            _logger.LogError(ex, "Forecast recompute failed.");
            return new RecomputeResultDto(
                audit.StartedAt, audit.CompletedAt!.Value, audit.DurationMs,
                0, 0, 0, 0, false, ex.Message);
        }
    }

    public async Task<RecomputeAuditDto?> GetLastAuditAsync(CancellationToken ct = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        var last = await ctx.ForecastRecomputeAudits
            .OrderByDescending(a => a.StartedAt)
            .FirstOrDefaultAsync(ct);

        if (last is null) return null;

        return new RecomputeAuditDto(
            last.Id,
            last.StartedAt,
            last.CompletedAt,
            last.DurationMs,
            last.TriggerType,
            last.TriggeredBy,
            last.ForecastsGenerated,
            last.ReplenishmentsGenerated,
            last.PromotionsGenerated,
            last.ClassificationsUpdated,
            last.Success,
            last.ErrorMessage);
    }
}
