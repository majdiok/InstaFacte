using FactuTrust.Application.Common.Interfaces.Forecasting;
using FactuTrust.Application.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.Forecasting.Background;

/// <summary>
/// Weekly background service that scans the Tunisian commercial calendar for the next 30 days and
/// generates promotion recommendations for upcoming events (Ramadan, Aïd, Soldes, Rentrée…). Runs
/// once a week at the configured day-of-week and Tunis-local hour. Idempotent: existing Pending
/// recommendations for the same event/category are skipped by the underlying service.
/// </summary>
public sealed class PromotionWindowDetectorService : BackgroundService
{
    private static readonly TimeZoneInfo TunisTimeZone = ResolveTunisTimeZone();

    private readonly IServiceProvider _services;
    private readonly ForecastingOptions _options;
    private readonly ILogger<PromotionWindowDetectorService> _logger;

    public PromotionWindowDetectorService(
        IServiceProvider services,
        IOptions<ForecastingOptions> options,
        ILogger<PromotionWindowDetectorService> logger)
    {
        _services = services;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled || !_options.PromotionDetectorEnabled)
        {
            _logger.LogInformation("PromotionWindowDetectorService is disabled.");
            return;
        }

        try { await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken); }
        catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var nextRun = ComputeNextRun(DateTime.UtcNow);
                var delay = nextRun - DateTime.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    _logger.LogInformation("Next promotion-window scan at {NextRun:O} (in {Delay}).", nextRun, delay);
                    await Task.Delay(delay, stoppingToken);
                }

                using var scope = _services.CreateScope();
                var promoSvc = scope.ServiceProvider.GetService<IPromotionRecommendationService>();
                if (promoSvc is null)
                {
                    _logger.LogWarning("IPromotionRecommendationService not registered; skipping scan.");
                }
                else
                {
                    var generated = await promoSvc.GenerateAsync(_options.DefaultHorizonDays, stoppingToken);
                    _logger.LogInformation("Promotion-window scan completed: {Count} new recommendation(s).", generated);
                }
            }
            catch (TaskCanceledException) { return; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PromotionWindowDetectorService cycle failed; retrying in 1 hour.");
                try { await Task.Delay(TimeSpan.FromHours(1), stoppingToken); }
                catch (TaskCanceledException) { return; }
            }
        }
    }

    private DateTime ComputeNextRun(DateTime nowUtc)
    {
        var targetDay = ParseDayOfWeek(_options.PromotionScanDayOfWeek);
        var nowTunis = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, TunisTimeZone);
        var todayTunis = nowTunis.Date;
        var hour = Math.Clamp(_options.PromotionScanHourTunis, 0, 23);

        var daysUntilTarget = ((int)targetDay - (int)todayTunis.DayOfWeek + 7) % 7;
        var targetDate = todayTunis.AddDays(daysUntilTarget).AddHours(hour);
        if (targetDate <= nowTunis) targetDate = targetDate.AddDays(7);

        return TimeZoneInfo.ConvertTimeToUtc(targetDate, TunisTimeZone);
    }

    private static DayOfWeek ParseDayOfWeek(string s) =>
        Enum.TryParse<DayOfWeek>(s, true, out var d) ? d : DayOfWeek.Monday;

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
