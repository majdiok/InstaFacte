using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services.Background;

/// <summary>
/// Lot B5 — Job Hangfire de démonstration : purge les <c>FailedLoginAttempts</c> de plus de 90 jours.
///
/// Cette table grossit linéairement avec le trafic ; au-delà de 90 jours, l'utilité forensique
/// est faible (logs Serilog/Seq prennent le relais). Programmé via <c>RecurringJob.AddOrUpdate</c>
/// dans <c>Program.cs</c> (exécution quotidienne à 03h UTC).
///
/// <b>Conservatif</b> : limite la suppression à 50 000 lignes par exécution pour éviter de bloquer
/// la table en cas d'accumulation massive (le job retournera supprimer le reste à la prochaine
/// exécution).
/// </summary>
public sealed class CleanupOldFailedLoginAttemptsJob
{
    private const int RetentionDays = 90;
    private const int MaxRowsPerRun = 50_000;

    private readonly MasterDbContext _db;
    private readonly ILogger<CleanupOldFailedLoginAttemptsJob> _logger;

    public CleanupOldFailedLoginAttemptsJob(
        MasterDbContext db,
        ILogger<CleanupOldFailedLoginAttemptsJob> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);

        // ExecuteDeleteAsync (EF Core 7+) avec limite via Take()
        var idsToDelete = await _db.FailedLoginAttempts
            .Where(a => a.AttemptAt < cutoff)
            .OrderBy(a => a.AttemptAt)
            .Take(MaxRowsPerRun)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        if (idsToDelete.Count == 0)
        {
            _logger.LogInformation("FailedLoginAttempts cleanup: nothing to delete (cutoff={Cutoff})", cutoff);
            return;
        }

        var deleted = await _db.FailedLoginAttempts
            .Where(a => idsToDelete.Contains(a.Id))
            .ExecuteDeleteAsync(cancellationToken);

        _logger.LogInformation(
            "FailedLoginAttempts cleanup: removed {Count} rows older than {Cutoff} (max per run = {Max})",
            deleted, cutoff, MaxRowsPerRun);
    }
}
