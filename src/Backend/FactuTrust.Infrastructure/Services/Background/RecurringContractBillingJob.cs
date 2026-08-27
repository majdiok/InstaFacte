using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services.RecurringContracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RecurringContractsOptions _options;
    private readonly ILogger<RecurringContractBillingJob> _logger;

    public RecurringContractBillingJob(
        MasterDbContext master,
        ITenantService tenantService,
        IServiceScopeFactory scopeFactory,
        IOptions<RecurringContractsOptions> options,
        ILogger<RecurringContractBillingJob> logger)
    {
        _master = master;
        _tenantService = tenantService;
        _scopeFactory = scopeFactory;
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

                using var scope = _scopeFactory.CreateScope();
                var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
                tenantContext.SetTenant(tenantId, connectionString);

                var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
                await using var tenantDb = factory.CreateContext();
                var billing = scope.ServiceProvider.GetRequiredService<RecurringContractBillingService>();
                var result = await billing.ScanAndCreateDraftsAsync(
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
