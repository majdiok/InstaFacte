using FactuTrust.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services.Background;

/// <summary>
/// Refreshes the pre-migrated tenant template database and its .bak backup
/// used for fast provisioning during firm/tenant registration.
/// </summary>
public sealed class TenantTemplateMaintenanceJob
{
    private readonly ITenantService _tenantService;
    private readonly ILogger<TenantTemplateMaintenanceJob> _logger;

    public TenantTemplateMaintenanceJob(
        ITenantService tenantService,
        ILogger<TenantTemplateMaintenanceJob> logger)
    {
        _tenantService = tenantService;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _tenantService.EnsureTenantTemplateAsync(cancellationToken);
            _logger.LogInformation("Tenant template maintenance completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tenant template maintenance failed.");
            throw;
        }
    }
}
