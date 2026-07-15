using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.Background;

/// <summary>
/// Job Hangfire quotidien : génère les écritures des modèles récurrents échus pour TOUS les
/// tenants actifs (itération multi-tenant façon StorefrontProjectionSyncService — MasterDbContext
/// pour la liste, connexion tenant directe pour le travail). Un tenant en échec n'empêche pas
/// les autres ; l'idempotence par tenant est portée par <see cref="RecurringEntryGenerator"/>
/// (NextRunDate avancé dans le même SaveChanges que l'écriture).
/// </summary>
public sealed class RecurringEntriesJob
{
    private readonly MasterDbContext _master;
    private readonly ITenantService _tenantService;
    private readonly RecurringEntryGenerator _generator;
    private readonly AccountingSettings _settings;
    private readonly ILogger<RecurringEntriesJob> _logger;

    public RecurringEntriesJob(
        MasterDbContext master,
        ITenantService tenantService,
        RecurringEntryGenerator generator,
        IOptions<AccountingSettings> settings,
        ILogger<RecurringEntriesJob> logger)
    {
        _master = master;
        _tenantService = tenantService;
        _generator = generator;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var tenantIds = await _master.Tenants
            .AsNoTracking()
            .Where(t => t.IsActive)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        var totalGenerated = 0;
        foreach (var tenantId in tenantIds)
        {
            try
            {
                var connectionString = await _tenantService.GetConnectionStringAsync(tenantId, cancellationToken);
                if (string.IsNullOrEmpty(connectionString))
                    continue;

                var options = new DbContextOptionsBuilder<TenantDbContext>()
                    .UseSqlServer(connectionString, b => b.MigrationsAssembly(typeof(TenantDbContext).Assembly.FullName))
                    .Options;

                await using var tenantDb = new TenantDbContext(options);
                totalGenerated += await _generator.GenerateDueEntriesAsync(tenantDb, _settings, today, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Recurring entries generation failed for tenant {TenantId}", tenantId);
            }
        }

        _logger.LogInformation("Recurring entries job completed: {Count} entry(ies) generated across {Tenants} tenant(s).",
            totalGenerated, tenantIds.Count);
    }
}
