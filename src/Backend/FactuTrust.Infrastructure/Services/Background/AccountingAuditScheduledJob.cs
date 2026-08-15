using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Audit;
using FactuTrust.Domain.Entities.AccountingAudit;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.Background;

/// <summary>
/// Job Hangfire : exécute les planifications de contrôle comptable actives pour tous les tenants.
/// </summary>
/// <remarks>
/// <para>
/// Calqué sur <see cref="FiscalReminderJob"/> : drapeau d'abord avec sortie anticipée, énumération
/// master, isolation des échecs par tenant, log de synthèse final.
/// </para>
/// <para>
/// <b>Hors tenant ambiant.</b> Le job n'a pas de contexte de requête : il passe donc par
/// <see cref="IAccountingAuditEngine.RunForConnectionAsync"/> avec la chaîne de connexion du
/// dossier, et non par <c>RunAsync</c> qui s'appuierait sur un <c>ITenantContext</c> vide.
/// </para>
/// <para>
/// <b>Échéance.</b> Une planification n'est exécutée que si son expression cron a franchi un
/// créneau depuis <c>LastRunAt</c>. <c>LastRunAt</c> n'est avancé qu'après une exécution
/// <i>réussie</i> : un échec transitoire est ainsi retenté au tour suivant au lieu d'être perdu.
/// </para>
/// </remarks>
public sealed class AccountingAuditScheduledJob
{
    private readonly MasterDbContext _master;
    private readonly ITenantService _tenantService;
    private readonly IAccountingAuditEngine _engine;
    private readonly AccountingSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AccountingAuditScheduledJob> _logger;

    public AccountingAuditScheduledJob(
        MasterDbContext master,
        ITenantService tenantService,
        IAccountingAuditEngine engine,
        IOptions<AccountingSettings> settings,
        TimeProvider timeProvider,
        ILogger<AccountingAuditScheduledJob> logger)
    {
        _master = master;
        _tenantService = tenantService;
        _engine = engine;
        _settings = settings.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (!_settings.AccountingAuditSchedulingEnabled)
        {
            _logger.LogInformation(
                "AccountingAuditScheduledJob : désactivé (AccountingAuditSchedulingEnabled) — aucun contrôle lancé.");
            return;
        }

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        var tenantIds = await _master.Tenants.AsNoTracking()
            .Where(t => t.IsActive)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        var tenantsProcessed = 0;
        var runsLaunched = 0;
        var runsFailed = 0;

        foreach (var tenantId in tenantIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var connectionString = await _tenantService.GetConnectionStringAsync(tenantId, cancellationToken);
                if (string.IsNullOrEmpty(connectionString)) continue;

                var options = new DbContextOptionsBuilder<TenantDbContext>()
                    .UseSqlServer(connectionString, b => b.MigrationsAssembly(typeof(TenantDbContext).Assembly.FullName))
                    .Options;

                await using var tenantDb = new TenantDbContext(options);
                var schedules = await tenantDb.Set<AccountingControlSchedule>()
                    .Where(s => s.IsActive)
                    .ToListAsync(cancellationToken);
                if (schedules.Count == 0) continue;

                tenantsProcessed++;

                foreach (var schedule in schedules)
                {
                    if (!IsDue(schedule, nowUtc)) continue;

                    var request = BuildRequest(schedule, nowUtc);
                    var result = await _engine.RunForConnectionAsync(
                        connectionString, request, userId: null, userName: schedule.Name, cancellationToken);

                    if (result.IsFailure)
                    {
                        runsFailed++;
                        _logger.LogWarning(
                            "Contrôle planifié « {Schedule} » en échec pour le tenant {TenantId} : {Error}. " +
                            "LastRunAt inchangé, nouvelle tentative au prochain passage.",
                            schedule.Name, tenantId, result.Error.Description);
                        continue;
                    }

                    runsLaunched++;
                    schedule.LastRunAt = nowUtc;
                    _logger.LogInformation(
                        "Contrôle planifié « {Schedule} » exécuté pour le tenant {TenantId} : " +
                        "{Total} anomalie(s), conformité {Rate}%.",
                        schedule.Name, tenantId, result.Value.TotalAnomalies, result.Value.ComplianceRate);
                }

                await tenantDb.SaveChangesAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // L'échec d'un tenant ne doit jamais priver les autres de leur contrôle.
                runsFailed++;
                _logger.LogError(ex, "Accounting audit schedule job failed for tenant {TenantId}", tenantId);
            }
        }

        _logger.LogInformation(
            "AccountingAuditScheduledJob terminé : {Tenants} tenant(s) planifié(s), {Launched} contrôle(s) exécuté(s), {Failed} échec(s).",
            tenantsProcessed, runsLaunched, runsFailed);
    }

    /// <summary>
    /// Vrai si une occurrence cron a été franchie depuis <c>LastRunAt</c>. Une planification jamais
    /// exécutée est due immédiatement. Une expression illisible est journalisée et ignorée — elle ne
    /// fait tomber ni le tenant ni les autres planifications.
    /// </summary>
    private bool IsDue(AccountingControlSchedule schedule, DateTime nowUtc)
    {
        if (!CronOccurrenceEvaluator.IsValid(schedule.CronExpression))
        {
            _logger.LogWarning(
                "Expression cron illisible sur la planification « {Schedule} » : {Cron}. Planification ignorée.",
                schedule.Name, schedule.CronExpression);
            return false;
        }

        return CronOccurrenceEvaluator.HasOccurrenceBetween(schedule.CronExpression, schedule.LastRunAt, nowUtc);
    }

    /// <summary>
    /// Exercice ciblé = année courante décalée de <c>FiscalYearOffset</c> (0 ou absent = exercice
    /// en cours, -1 = exercice précédent, cas d'usage de la révision post-clôture).
    /// </summary>
    private static AccountingAuditRunRequestDto BuildRequest(AccountingControlSchedule schedule, DateTime nowUtc)
    {
        var fiscalYear = nowUtc.Year + (schedule.FiscalYearOffset ?? 0);

        var moduleCodes = string.IsNullOrWhiteSpace(schedule.ModuleCodesFilter)
            ? null
            : schedule.ModuleCodesFilter
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

        return new AccountingAuditRunRequestDto
        {
            FiscalYear = fiscalYear,
            ModuleCodes = moduleCodes
        };
    }
}
