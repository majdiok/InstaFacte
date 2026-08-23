using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services.RecurringContracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.Background;

/// <summary>
/// Job Hangfire quotidien : génère les brouillons de facture pour les contrats récurrents échus.
/// </summary>
public sealed class RecurringContractBillingJob
{
    private readonly MasterDbContext _master;
    private readonly ITenantService _tenantService;
    private readonly RecurringContractBillingService _billing;
    private readonly RecurringContractsOptions _options;
    private readonly ILogger<RecurringContractBillingJob> _logger;

    public RecurringContractBillingJob(
        MasterDbContext master,
        ITenantService tenantService,
        RecurringContractBillingService billing,
        IOptions<RecurringContractsOptions> options,
        ILogger<RecurringContractBillingJob> logger)
    {
        _master = master;
        _tenantService = tenantService;
        _billing = billing;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || !_options.BillingJobEnabled)
        {
            _logger.LogInformation("RecurringContractBillingJob skipped (feature disabled)");
            return;
        }

        var tenantIds = await _master.Tenants
            .AsNoTracking()
            .Where(t => t.IsActive)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        var totalCreated = 0;
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
                var result = await _billing.ScanAndCreateDraftsAsync(
                    tenantDb, null, DateTime.UtcNow, cancellationToken);
                if (result.IsSuccess)
                    totalCreated += result.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Recurring contract billing failed for tenant {TenantId}", tenantId);
            }
        }

        _logger.LogInformation(
            "RecurringContractBillingJob completed: {Count} draft(s) created across {Tenants} tenant(s).",
            totalCreated, tenantIds.Count);
    }
}
