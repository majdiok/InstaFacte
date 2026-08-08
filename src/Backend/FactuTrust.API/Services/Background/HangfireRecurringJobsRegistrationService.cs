using FactuTrust.Infrastructure.Services.Background;
using Hangfire;

namespace FactuTrust.API.Services.Background;

/// <summary>
/// Enregistre les jobs récurrents Hangfire APRÈS le démarrage de l'application, avec un retry
/// exponentiel sur les erreurs transitoires (SQL Server / LocalDB indisponible, schéma Hangfire
/// non encore prêt, etc.). Anciennement effectué inline avant <c>app.Run()</c> : la moindre
/// erreur crashait la totalité du processus, alors qu'aucun endpoint HTTP ne dépend de Hangfire
/// pour répondre. Cette version isole l'erreur dans le hosted service et retente jusqu'à succès,
/// sans bloquer le démarrage HTTP. L'idempotence de <see cref="RecurringJob.AddOrUpdate{T}(string, System.Linq.Expressions.Expression{System.Action{T}}, string, RecurringJobOptions)"/>
/// garantit l'absence de doublons malgré les retries.
/// </summary>
internal sealed class HangfireRecurringJobsRegistrationService : BackgroundService
{
    private static readonly RecurringJobOptions UtcOptions = new() { TimeZone = TimeZoneInfo.Utc };

    /// <summary>
    /// Définitions immuables des jobs récurrents. Pour ajouter / modifier un job : éditer cette liste.
    /// Les actions ci-dessous restent strictement équivalentes au bloc inline original
    /// (mêmes jobId, mêmes crons, même TimeZone UTC, même méthode appelée).
    /// </summary>
    private static readonly IReadOnlyList<JobDescriptor> Jobs = new JobDescriptor[]
    {
        new(
            "cleanup-old-failed-logins",
            () => RecurringJob.AddOrUpdate<CleanupOldFailedLoginAttemptsJob>(
                "cleanup-old-failed-logins",
                job => job.ExecuteAsync(CancellationToken.None),
                Cron.Daily(3),
                UtcOptions)),
        new(
            "expire-module-overrides",
            () => RecurringJob.AddOrUpdate<ExpireModuleOverridesJob>(
                "expire-module-overrides",
                job => job.ExecuteAsync(CancellationToken.None),
                Cron.Daily(2),
                UtcOptions)),
        new(
            "renewal-scan",
            () => RecurringJob.AddOrUpdate<RenewalScanJob>(
                "renewal-scan",
                job => job.ExecuteAsync(CancellationToken.None),
                Cron.Daily(4),
                UtcOptions)),
        new(
            "dunning-executor",
            () => RecurringJob.AddOrUpdate<DunningExecutorJob>(
                "dunning-executor",
                job => job.ExecuteAsync(CancellationToken.None),
                Cron.Daily(5),
                UtcOptions)),
        new(
            "ai-export-cleanup",
            () => RecurringJob.AddOrUpdate<AiExportCleanupJob>(
                "ai-export-cleanup",
                job => job.ExecuteAsync(CancellationToken.None),
                Cron.Hourly(),
                UtcOptions)),
        new(
            "recurring-journal-entries",
            () => RecurringJob.AddOrUpdate<RecurringEntriesJob>(
                "recurring-journal-entries",
                job => job.ExecuteAsync(CancellationToken.None),
                Cron.Daily(1),
                UtcOptions)),
        new(
            "fiscal-reminders",
            () => RecurringJob.AddOrUpdate<FiscalReminderJob>(
                "fiscal-reminders",
                job => job.ExecuteAsync(CancellationToken.None),
                Cron.Daily(6),
                UtcOptions)),
        new(
            "tenant-template-maintenance",
            () => RecurringJob.AddOrUpdate<TenantTemplateMaintenanceJob>(
                "tenant-template-maintenance",
                job => job.ExecuteAsync(CancellationToken.None),
                Cron.Weekly(DayOfWeek.Sunday, 2),
                UtcOptions)),
        new(
            "accounting-audit-schedules",
            () => RecurringJob.AddOrUpdate<FactuTrust.Infrastructure.Services.Background.AccountingAuditScheduledJob>(
                "accounting-audit-schedules",
                job => job.ExecuteAsync(CancellationToken.None),
                Cron.Daily(7),
                UtcOptions)),
    };

    private readonly ILogger<HangfireRecurringJobsRegistrationService> _logger;

    public HangfireRecurringJobsRegistrationService(
        ILogger<HangfireRecurringJobsRegistrationService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var remaining = Jobs.ToList();
        var attempt = 0;

        while (remaining.Count > 0 && !stoppingToken.IsCancellationRequested)
        {
            var stillPending = new List<JobDescriptor>(capacity: remaining.Count);
            foreach (var job in remaining)
            {
                try
                {
                    job.Register();
                    _logger.LogInformation(
                        "Hangfire recurring job '{JobId}' registered.",
                        job.JobId);
                }
                catch (Exception ex)
                {
                    // Toute exception (SqlException incluse) est traitée comme transitoire : on
                    // retentera ce job au tour suivant. On NE FAIT JAMAIS REMONTER vers l'hôte.
                    _logger.LogWarning(
                        ex,
                        "Hangfire recurring job '{JobId}' registration failed (attempt {Attempt}); will retry.",
                        job.JobId,
                        attempt + 1);
                    stillPending.Add(job);
                }
            }

            remaining = stillPending;
            if (remaining.Count == 0)
            {
                _logger.LogInformation("All Hangfire recurring jobs registered.");
                break;
            }

            var delay = ComputeBackoff(attempt);
            _logger.LogInformation(
                "{Pending} Hangfire recurring job(s) still pending; retrying in {DelaySeconds}s.",
                remaining.Count,
                (int)delay.TotalSeconds);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return; // arrêt de l'hôte
            }

            attempt++;
        }
    }

    /// <summary>Back-off exponentiel borné : 5 s → 10 s → 30 s → 60 s → 5 min (puis 5 min).</summary>
    private static TimeSpan ComputeBackoff(int attempt) => attempt switch
    {
        0 => TimeSpan.FromSeconds(5),
        1 => TimeSpan.FromSeconds(10),
        2 => TimeSpan.FromSeconds(30),
        3 => TimeSpan.FromSeconds(60),
        _ => TimeSpan.FromMinutes(5),
    };

    private readonly record struct JobDescriptor(string JobId, Action Register);
}
