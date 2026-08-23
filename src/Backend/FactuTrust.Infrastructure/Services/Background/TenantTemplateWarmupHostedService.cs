using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.Background;

/// <summary>
/// Builds or refreshes the pre-migrated tenant template in the background after the API starts listening.
/// Does not block HTTP startup; a cold first registration still falls back to full migrations if needed.
/// </summary>
public sealed class TenantTemplateWarmupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<TenantProvisioningOptions> _options;
    private readonly ILogger<TenantTemplateWarmupHostedService> _logger;

    public TenantTemplateWarmupHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<TenantProvisioningOptions> options,
        ILogger<TenantTemplateWarmupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.Value.Strategy != TenantProvisioningStrategy.TemplateClone)
        {
            _logger.LogInformation(
                "Skipping tenant template warmup because TenantProvisioning:Strategy is {Strategy}.",
                _options.Value.Strategy);
            return;
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();
            await tenantService.EnsureTenantTemplateAsync(stoppingToken);
            _logger.LogInformation("Tenant template warmup completed.");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host is shutting down.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Tenant template warmup failed; the first registration may fall back to full MigrateAsync.");
        }
    }
}
