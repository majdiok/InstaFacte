using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Scripts;

/// <summary>
/// Helper class for applying migrations to all existing tenant databases.
/// This should be used during deployment or maintenance windows.
/// </summary>
public static class TenantMigrationHelper
{
    /// <summary>
    /// Applies migrations to all existing tenant databases.
    /// </summary>
    /// <param name="serviceProvider">Service provider with registered services</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of tenants processed successfully</returns>
    public static async Task<int> ApplyMigrationsToAllTenantsAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger(typeof(TenantMigrationHelper).FullName ?? nameof(TenantMigrationHelper));
        var tenantService = serviceProvider.GetRequiredService<ITenantService>();
        
        await using var scope = serviceProvider.CreateAsyncScope();
        var masterContext = scope.ServiceProvider.GetRequiredService<MasterDbContext>();

        logger.LogInformation("Starting migration process for all tenants...");

        var tenants = await masterContext.Tenants
            .Where(t => t.IsActive)
            .ToListAsync(cancellationToken);

        if (tenants.Count == 0)
        {
            logger.LogWarning("No active tenants found.");
            return 0;
        }

        logger.LogInformation("Found {Count} active tenant(s) to process", tenants.Count);

        int successCount = 0;
        int failureCount = 0;

        foreach (var tenant in tenants)
        {
            try
            {
                logger.LogInformation("Applying migrations for tenant {TenantId} ({CompanyName})...", 
                    tenant.Id, tenant.CompanyName);

                await tenantService.ApplyMigrationsAsync(tenant.Id, cancellationToken);

                logger.LogInformation("✓ Migrations applied successfully for tenant {TenantId}", tenant.Id);
                successCount++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "✗ Failed to apply migrations for tenant {TenantId} ({CompanyName})", 
                    tenant.Id, tenant.CompanyName);
                failureCount++;
            }
        }

        logger.LogInformation(
            "Migration process completed. Success: {SuccessCount}, Failures: {FailureCount}",
            successCount, failureCount);

        return successCount;
    }

    /// <summary>
    /// Checks if a tenant database has migrations applied.
    /// </summary>
    /// <param name="serviceProvider">Service provider</param>
    /// <param name="tenantId">Tenant ID to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if migrations are applied, false otherwise</returns>
    public static async Task<bool> HasMigrationsAppliedAsync(
        IServiceProvider serviceProvider,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenantService = serviceProvider.GetRequiredService<ITenantService>();
        var connectionString = await tenantService.GetConnectionStringAsync(tenantId, cancellationToken);

        if (string.IsNullOrEmpty(connectionString))
            return false;

        try
        {
            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseSqlServer(connectionString, b => b.MigrationsAssembly(typeof(TenantDbContext).Assembly.FullName))
                .Options;

            await using var context = new TenantDbContext(options);
            
            // Check if __EFMigrationsHistory table exists and has entries
            var canConnect = await context.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
                return false;

            // Try to query the migrations history table
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync(cancellationToken);
            return !pendingMigrations.Any();
        }
        catch
        {
            return false;
        }
    }
}
