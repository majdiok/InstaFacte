using System.Collections.Concurrent;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Common;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.MultiTenancy;

/// <summary>
/// Guard that ensures tenant migrations are applied before running tenant-scoped operations.
/// Uses a two-phase approach: first check if migrations are needed (cheap), then apply if necessary.
/// In Development, cache duration is shortened so new migrations are picked up quickly after deploy.
/// </summary>
public sealed class TenantMigrationGuard : ITenantMigrationGuard
{
    private const string CacheKeyPrefix = "TenantMigrationGuard.Applied.";
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> TenantLocks = new();
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
    private static readonly TimeSpan CacheDurationDevelopment = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan FailureCacheDuration = TimeSpan.FromSeconds(30);

    private readonly IMemoryCache _cache;
    private readonly ITenantService _tenantService;
    private readonly ILogger<TenantMigrationGuard> _logger;
    private readonly IHostEnvironment _environment;

    public TenantMigrationGuard(
        IMemoryCache cache,
        ITenantService tenantService,
        ILogger<TenantMigrationGuard> logger,
        IHostEnvironment environment)
    {
        _cache = cache;
        _tenantService = tenantService;
        _logger = logger;
        _environment = environment;
    }

    public async Task<Result> EnsureMigrationsAppliedAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return Result.Failure(Error.Validation("TenantId", "Identifiant d'entreprise invalide."));

        var cacheKey = $"{CacheKeyPrefix}{tenantId}";
        if (_cache.TryGetValue(cacheKey, out bool isMigrated))
        {
            if (isMigrated)
            {
                return Result.Success();
            }
            
            // If we have a cached failure, return false immediately to prevent DB hammering
            return Result.Failure(Error.Validation(
                "Migration",
                "Impossible d'accéder à la base de données. Veuillez vérifier la connexion ou contacter l'administrateur."));
        }

        var semaphore = TenantLocks.GetOrAdd(tenantId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            if (_cache.TryGetValue(cacheKey, out isMigrated))
            {
                if (isMigrated) return Result.Success();
                return Result.Failure(Error.Validation("Migration", "Base de données inaccessible temporairement."));
            }

            var connectionString = await _tenantService.GetConnectionStringAsync(tenantId, cancellationToken);
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("No connection string found for tenant {TenantId}", tenantId);
                return Result.Failure(Error.Validation(
                    "Tenant",
                    "Base de données introuvable pour cette entreprise. Veuillez contacter l'administrateur."));
            }

            var pendingMigrations = await GetPendingMigrationsAsync(connectionString, cancellationToken);

            if (pendingMigrations.Count > 0)
            {
                _logger.LogInformation(
                    "Applying {Count} pending migration(s) for tenant {TenantId}: {Migrations}",
                    pendingMigrations.Count,
                    tenantId,
                    string.Join(", ", pendingMigrations));
                await _tenantService.ApplyMigrationsAsync(tenantId, cancellationToken);
                _logger.LogInformation("Migrations applied successfully for tenant {TenantId}", tenantId);
            }

            await EnsureWithholdingTaxCatalogAsync(connectionString, cancellationToken);
            await EnsureWithholdingChartAccountsAsync(connectionString, cancellationToken);

            var cacheDuration = _environment.IsDevelopment() ? CacheDurationDevelopment : CacheDuration;
            _cache.Set(cacheKey, true, cacheDuration);
            return Result.Success();
        }
        catch (Exception ex)
        {
            int? sqlErrorNumber = ex switch
            {
                SqlException sqlEx => sqlEx.Number,
                _ when ex.InnerException is SqlException innerSqlEx => innerSqlEx.Number,
                _ => null
            };

            _logger.LogError(
                ex,
                "Failed to ensure migrations for tenant {TenantId}: {Message}. SqlErrorNumber={SqlErrorNumber}",
                tenantId,
                ex.Message,
                sqlErrorNumber);

            // Cache failure for a short duration to prevent retry storms when DB is down
            _cache.Set(cacheKey, false, FailureCacheDuration);

            return Result.Failure(Error.Validation(
                "Migration",
                "Impossible d'accéder à la base de données. Veuillez vérifier la connexion ou contacter l'administrateur."));
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static async Task<IReadOnlyList<string>> GetPendingMigrationsAsync(
        string connectionString,
        CancellationToken cancellationToken)
    {
        try
        {
            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseSqlServer(connectionString, b => b.MigrationsAssembly(typeof(TenantDbContext).Assembly.FullName))
                .Options;

            await using var context = new TenantDbContext(options);

            if (!await context.Database.CanConnectAsync(cancellationToken))
                return new List<string> { "(database unreachable)" };

            var pending = await context.Database.GetPendingMigrationsAsync(cancellationToken);
            return pending.ToList();
        }
        catch
        {
            return new List<string> { "(migration check failed)" };
        }
    }

    private static async Task EnsureWithholdingTaxCatalogAsync(string connectionString, CancellationToken cancellationToken)
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlServer(connectionString, b => b.MigrationsAssembly(typeof(TenantDbContext).Assembly.FullName))
            .Options;

        await using var context = new TenantDbContext(options);
        await WithholdingTaxCatalogInitializer.EnsureSystemTypesSeededAsync(context, cancellationToken);
        await WithholdingFiscalYearParameterInitializer.EnsureDefaultsSeededAsync(context, cancellationToken);
        await IncomeTaxYearParameterInitializer.EnsureDefaultsSeededAsync(context, cancellationToken);
    }

    private static async Task EnsureWithholdingChartAccountsAsync(string connectionString, CancellationToken cancellationToken)
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlServer(connectionString, b => b.MigrationsAssembly(typeof(TenantDbContext).Assembly.FullName))
            .Options;

        await using var context = new TenantDbContext(options);
        await WithholdingChartAccountsInitializer.EnsureAccountsAsync(context, cancellationToken);
    }
}
