using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.AuditChainRepair;

/// <summary>
/// One-off tool: re-seals PreviousHash and Hash for all AuditLogs in canonical order (CreatedAt, Id).
/// Usage: FactuTrust.AuditChainRepair &lt;tenantSqlConnectionString&gt;
/// Or set environment variable TENANT_CONNECTION_STRING.
/// Take a database backup before running.
/// </summary>
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var connectionString = args.Length >= 1
            ? args[0]
            : Environment.GetEnvironmentVariable("TENANT_CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            await Console.Error.WriteLineAsync("Usage: FactuTrust.AuditChainRepair <tenantSqlConnectionString>");
            await Console.Error.WriteLineAsync("Or set environment variable TENANT_CONNECTION_STRING.");
            return 1;
        }

        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlServer(
                connectionString,
                sql => sql.MigrationsAssembly(typeof(TenantDbContext).Assembly.FullName))
            .Options;

        ITenantDbContextFactory factory = new StandaloneTenantDbContextFactory(options);
        var repair = new AuditChainRepairService(factory);
        var result = await repair.ResealChainAsync(CancellationToken.None);

        if (result.IsFailure)
        {
            await Console.Error.WriteLineAsync(result.Error.Description);
            return 2;
        }

        await Console.Out.WriteLineAsync(
            $"Resealed {result.Value} audit log row(s). Document this administrative operation for compliance.");
        return 0;
    }

    private sealed class StandaloneTenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly DbContextOptions<TenantDbContext> _options;

        public StandaloneTenantDbContextFactory(DbContextOptions<TenantDbContext> options)
        {
            _options = options;
        }

        public TenantDbContext CreateContext() => new TenantDbContext(_options);
    }
}
