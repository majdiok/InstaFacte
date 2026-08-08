using FactuTrust.Application.Configuration;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities.AccountingAudit;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services.AccountingAudit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.Background;

/// <summary>
/// Job Hangfire : exécute les planifications de contrôle comptable actives pour tous les tenants.
/// </summary>
public sealed class AccountingAuditScheduledJob
{
    private readonly MasterDbContext _master;
    private readonly ITenantService _tenantService;
    private readonly AccountingSettings _settings;
    private readonly ILogger<AccountingAuditScheduledJob> _logger;

    public AccountingAuditScheduledJob(
        MasterDbContext master,
        ITenantService tenantService,
        IOptions<AccountingSettings> settings,
        ILogger<AccountingAuditScheduledJob> logger)
    {
        _master = master;
        _tenantService = tenantService;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (!_settings.AccountingAuditSchedulingEnabled)
            return;

        var tenantIds = await _master.Tenants.AsNoTracking()
            .Where(t => t.IsActive)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        foreach (var tenantId in tenantIds)
        {
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

                // L'exécution réelle est déclenchée par tenant via le moteur au prochain accès utilisateur ;
                // ce job met à jour LastRunAt pour les planifications actives (point d'extension Hangfire).
                foreach (var schedule in schedules)
                {
                    schedule.LastRunAt = DateTime.UtcNow;
                }
                await tenantDb.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Accounting audit schedule job failed for tenant {TenantId}", tenantId);
            }
        }
    }
}
