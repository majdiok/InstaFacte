using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FactuTrust.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for MasterDbContext.
/// Used by EF Core tools (migrations, etc.) to create DbContext instances.
/// </summary>
public class MasterDbContextFactory : IDesignTimeDbContextFactory<MasterDbContext>
{
    public MasterDbContext CreateDbContext(string[] args)
    {
        // Build configuration from appsettings files
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "FactuTrust.API"))
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
            .Build();

        var connectionString = configuration.GetConnectionString("MasterConnection")
            ?? throw new InvalidOperationException(
                "Chaîne de connexion 'MasterConnection' introuvable dans la configuration.");

        var optionsBuilder = new DbContextOptionsBuilder<MasterDbContext>();
        optionsBuilder.UseSqlServer(
            connectionString,
            b => b.MigrationsAssembly(typeof(MasterDbContext).Assembly.FullName));

        return new MasterDbContext(optionsBuilder.Options);
    }
}
