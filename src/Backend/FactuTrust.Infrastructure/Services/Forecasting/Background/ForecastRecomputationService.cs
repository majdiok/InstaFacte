using FactuTrust.Application.Common.Interfaces.Forecasting;
using FactuTrust.Application.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.Forecasting.Background;

/// <summary>
/// Hosted service that triggers a full forecasting recompute once per day at the configured Tunis-local hour
/// (defaults to 02:00). Picks up the recompute orchestrator from the request scope to honour multi-tenant
/// boundaries (the orchestrator targets the *current* resolved tenant — this service runs against a single
/// background tenant context per cycle; multi-tenant fan-out is handled by extending the orchestrator).
/// </summary>
public sealed class ForecastRecomputationService : BackgroundService
{
    private static readonly TimeZoneInfo TunisTimeZone = ResolveTunisTimeZone();

    private readonly IServiceProvider _services;
    private readonly ForecastingOptions _options;
    private readonly ILogger<ForecastRecomputationService> _logger;

    public ForecastRecomputationService(
        IServiceProvider services,
        IOptions<ForecastingOptions> options,
        ILogger<ForecastRecomputationService> logger)
    {
        _services = services;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled || !_options.BackgroundRecomputeEnabled)
        {
            _logger.LogInformation("ForecastRecomputationService is disabled (Enabled={E}, BackgroundRecomputeEnabled={B}).",
                _options.Enabled, _options.BackgroundRecomputeEnabled);
            return;
        }

        // Wait until close to startup completion so EF migrations and tenant resolution are ready.
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var nextRun = ComputeNextRun(DateTime.UtcNow);
                var delay = nextRun - DateTime.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    _logger.LogInformation("Next forecast recompute scheduled at {NextRun:O} (in {Delay}).", nextRun, delay);
                    await Task.Delay(delay, stoppingToken);
                }

                await RunAsync(stoppingToken);
            }
            catch (TaskCanceledException) { return; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ForecastRecomputationService cycle failed; retrying in 5 minutes.");
                try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); }
                catch (TaskCanceledException) { return; }
            }
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var orchestrator = scope.ServiceProvider.GetService<IForecastRecomputeOrchestrator>();
        if (orchestrator is null)
        {
            _logger.LogWarning("IForecastRecomputeOrchestrator is not registered; skipping run.");
            return;
        }

        var result = await orchestrator.RecomputeAsync("Scheduled", null, ct);
        if (result.Success)
        {
            _logger.LogInformation(
                "Scheduled recompute completed in {Ms}ms. Replenishments: {Repl}, Promotions: {Promo}, Classifications: {Class}.",
                result.DurationMs, result.ReplenishmentsGenerated, result.PromotionsGenerated, result.ClassificationsUpdated);
        }
        else
        {
            _logger.LogWarning("Scheduled recompute failed: {Error}", result.ErrorMessage);
        }
    }

    private DateTime ComputeNextRun(DateTime nowUtc)
    {
        var nowTunis = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, TunisTimeZone);
        var todayTunis = nowTunis.Date;
        var targetTunis = todayTunis.AddHours(Math.Clamp(_options.RecomputeHourTunis, 0, 23));
        if (targetTunis <= nowTunis) targetTunis = targetTunis.AddDays(1);
        return TimeZoneInfo.ConvertTimeToUtc(targetTunis, TunisTimeZone);
    }

    private static TimeZoneInfo ResolveTunisTimeZone()
    {
        foreach (var id in new[] { "Africa/Tunis", "Tunis Standard Time", "W. Central Africa Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("Africa/Tunis-Fallback", TimeSpan.FromHours(1), "Africa/Tunis", "Africa/Tunis");
    }
}
