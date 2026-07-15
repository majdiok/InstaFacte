using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FactuTrust.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for TenantDbContext.
/// Used by EF Core tools (migrations, etc.) to create DbContext instances.
/// </summary>
public class TenantDbContextDesignTimeFactory : IDesignTimeDbContextFactory<TenantDbContext>
{
    public TenantDbContext CreateDbContext(string[] args)
    {
        // Build configuration from appsettings files
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "FactuTrust.API"))
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultTenantConnection")
            ?? configuration.GetConnectionString("MasterConnection")
            ?? throw new InvalidOperationException(
                "Chaîne de connexion 'DefaultTenantConnection' ou 'MasterConnection' introuvable dans la configuration.");

        var optionsBuilder = new DbContextOptionsBuilder<TenantDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new TenantDbContext(optionsBuilder.Options);
    }
}
