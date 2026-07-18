using FactuTrust.API.Authorization;
using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Scripts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.API.Controllers;

/// <summary>Platform-only endpoints for tenant database migrations.</summary>
[ApiController]
[Route("api/platform/migrations")]
[Authorize(Policy = PlatformPolicies.PlatformAdmin)]
public sealed class PlatformMigrationsController : ControllerBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PlatformMigrationsController> _logger;

    public PlatformMigrationsController(IServiceProvider serviceProvider, ILogger<PlatformMigrationsController> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    [HttpPost("tenants/apply-migrations")]
    [ProducesResponseType(typeof(ApiResponse<MigrationResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ApplyMigrationsToAllTenants(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Platform admin requested migration of all tenant databases");

            var successCount = await TenantMigrationHelper.ApplyMigrationsToAllTenantsAsync(
                _serviceProvider,
                cancellationToken);

            await using var scope = _serviceProvider.CreateAsyncScope();
            var masterContext = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
            var totalTenants = await masterContext.Tenants.CountAsync(t => t.IsActive, cancellationToken);

            var result = new MigrationResultDto
            {
                TotalTenants = totalTenants,
                SuccessCount = successCount,
                FailureCount = totalTenants - successCount
            };

            _logger.LogInformation(
                "Migration completed. Total: {Total}, Success: {Success}, Failures: {Failures}",
                result.TotalTenants, result.SuccessCount, result.FailureCount);

            return Ok(ApiResponse<MigrationResultDto>.Ok(result,
                $"Migrations appliquées avec succès pour {successCount} tenant(s) sur {totalTenants}."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during tenant migration");
            return StatusCode(500, ApiResponse<MigrationResultDto>.Fail(
                $"Erreur lors de l'application des migrations : {ex.Message}"));
        }
    }

    [HttpPost("tenants/{tenantId:guid}/apply-migrations")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ApplyMigrationsToTenant(Guid tenantId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Platform admin requested migration for tenant {TenantId}", tenantId);

            await using var scope = _serviceProvider.CreateAsyncScope();
            var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();
            var masterContext = scope.ServiceProvider.GetRequiredService<MasterDbContext>();

            var tenant = await masterContext.Tenants
                .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

            if (tenant == null)
            {
                return NotFound(ApiResponse<object>.Fail($"Tenant {tenantId} introuvable."));
            }

            await tenantService.ApplyMigrationsAsync(tenantId, cancellationToken);

            _logger.LogInformation("Migrations applied successfully for tenant {TenantId}", tenantId);

            return Ok(ApiResponse<object>.Ok(null!,
                $"Migrations appliquées avec succès pour le tenant {tenant.CompanyName}."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying migrations for tenant {TenantId}", tenantId);
            return StatusCode(500, ApiResponse<object>.Fail(
                $"Erreur lors de l'application des migrations : {ex.Message}"));
        }
    }

    [HttpGet("tenants/{tenantId:guid}/migrations-status")]
    [ProducesResponseType(typeof(ApiResponse<MigrationStatusResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTenantMigrationStatus(Guid tenantId, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var masterContext = scope.ServiceProvider.GetRequiredService<MasterDbContext>();

            var row = await (
                    from t in masterContext.Tenants.AsNoTracking()
                    where t.Id == tenantId
                    join s in masterContext.Subscriptions.AsNoTracking() on t.Id equals s.TenantId into sj
                    from sub in sj.DefaultIfEmpty()
                    select new { Tenant = t, Sub = sub })
                .FirstOrDefaultAsync(cancellationToken);

            if (row is null)
            {
                return NotFound(ApiResponse<MigrationStatusResultDto>.Fail($"Tenant {tenantId} introuvable."));
            }

            var hasMigrations = await TenantMigrationHelper.HasMigrationsAppliedAsync(
                _serviceProvider,
                tenantId,
                cancellationToken);

            var subscription = row.Sub;
            var result = new MigrationStatusResultDto
            {
                TenantId = tenantId,
                TenantName = row.Tenant.CompanyName,
                HasMigrationsApplied = hasMigrations,
                SubscriptionPlan = subscription?.Plan,
                SubscriptionPlanDisplay = subscription is null ? null : subscription.Plan.ToDisplayString(),
                SubscriptionStatus = subscription?.Status,
                SubscriptionStatusDisplay = subscription is null ? null : subscription.Status.ToDisplayString(),
                IsPayingSubscriber = PlatformSubscriptionSegmentHelper.IsPayingSubscriber(subscription?.Plan, subscription?.Status)
            };

            return Ok(ApiResponse<MigrationStatusResultDto>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking migration status for tenant {TenantId}", tenantId);
            return StatusCode(500, ApiResponse<MigrationStatusResultDto>.Fail(
                $"Erreur lors de la vérification : {ex.Message}"));
        }
    }

    /// <summary>
    /// KPIs agrégés pour la page Migrations (Lot A3).
    /// Les échecs 24h sont retournés à 0 tant que le tracking persistant n'est pas en place (Lot D4).
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApiResponse<MigrationStatsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var masterContext = scope.ServiceProvider.GetRequiredService<MasterDbContext>();

            var activeTenantIds = await masterContext.Tenants
                .Where(t => t.IsActive)
                .Select(t => t.Id)
                .ToListAsync(cancellationToken);

            var upToDate = 0;
            foreach (var tenantId in activeTenantIds)
            {
                try
                {
                    var hasMigrations = await TenantMigrationHelper.HasMigrationsAppliedAsync(
                        _serviceProvider, tenantId, cancellationToken);
                    if (hasMigrations) upToDate++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not read migration status for tenant {TenantId}", tenantId);
                    // tenant compte comme "pending" (non up-to-date)
                }
            }

            var stats = new MigrationStatsDto
            {
                TotalTenants = activeTenantIds.Count,
                UpToDate = upToDate,
                Pending = activeTenantIds.Count - upToDate,
                Failures24h = 0 // Lot D4 : à remplir depuis MigrationRunItems
            };

            return Ok(ApiResponse<MigrationStatsDto>.Ok(stats));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing migration stats");
            return StatusCode(500, ApiResponse<MigrationStatsDto>.Fail(
                $"Erreur lors du calcul des statistiques : {ex.Message}"));
        }
    }

    /// <summary>
    /// Balayage d'unicité des numéros de facture de vente (Invoices.Number) sur tous
    /// les tenants actifs. Préalable OBLIGATOIRE avant de livrer la migration ajoutant
    /// l'index UNIQUE fiscal : tant que <c>IsUniqueIndexSafe</c> est faux (doublons ou
    /// tenant injoignable — fail-closed), la migration ne doit PAS être déployée, sinon
    /// elle échouerait au boot du tenant et le bloquerait via TenantMigrationGuard.
    /// </summary>
    [HttpGet("tenants/invoice-number-integrity")]
    [ProducesResponseType(typeof(ApiResponse<InvoiceNumberIntegrityReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ScanInvoiceNumberIntegrity(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Platform admin requested invoice number integrity scan for all tenants");

            var report = await InvoiceNumberIntegrityHelper.ScanAllTenantsAsync(_serviceProvider, cancellationToken);

            var message = report.IsUniqueIndexSafe
                ? $"Aucun doublon détecté sur {report.CleanTenants} tenant(s) : l'index unique peut être déployé."
                : $"Index unique NON déployable : {report.TenantsWithDuplicates} tenant(s) avec doublons, " +
                  $"{report.UnreachableTenants} injoignable(s) sur {report.TotalTenants}.";

            return Ok(ApiResponse<InvoiceNumberIntegrityReportDto>.Ok(report, message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during invoice number integrity scan");
            return StatusCode(500, ApiResponse<InvoiceNumberIntegrityReportDto>.Fail(
                $"Erreur lors du balayage d'unicité : {ex.Message}"));
        }
    }

    [HttpGet("tenants/migrations-status")]
    [ProducesResponseType(typeof(ApiResponse<List<MigrationStatusResultDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllTenantsMigrationStatus(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var masterContext = scope.ServiceProvider.GetRequiredService<MasterDbContext>();

            var rows = await (
                    from t in masterContext.Tenants.AsNoTracking()
                    where t.IsActive
                    join s in masterContext.Subscriptions.AsNoTracking() on t.Id equals s.TenantId into sj
                    from sub in sj.DefaultIfEmpty()
                    select new { Tenant = t, Sub = sub })
                .ToListAsync(cancellationToken);

            var results = new List<MigrationStatusResultDto>();

            foreach (var row in rows)
            {
                var hasMigrations = await TenantMigrationHelper.HasMigrationsAppliedAsync(
                    _serviceProvider,
                    row.Tenant.Id,
                    cancellationToken);

                var sub = row.Sub;
                results.Add(new MigrationStatusResultDto
                {
                    TenantId = row.Tenant.Id,
                    TenantName = row.Tenant.CompanyName,
                    HasMigrationsApplied = hasMigrations,
                    SubscriptionPlan = sub?.Plan,
                    SubscriptionPlanDisplay = sub is null ? null : sub.Plan.ToDisplayString(),
                    SubscriptionStatus = sub?.Status,
                    SubscriptionStatusDisplay = sub is null ? null : sub.Status.ToDisplayString(),
                    IsPayingSubscriber = PlatformSubscriptionSegmentHelper.IsPayingSubscriber(sub?.Plan, sub?.Status)
                });
            }

            return Ok(ApiResponse<List<MigrationStatusResultDto>>.Ok(results));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking migration status for all tenants");
            return StatusCode(500, ApiResponse<List<MigrationStatusResultDto>>.Fail(
                $"Erreur lors de la vérification : {ex.Message}"));
        }
    }
}
