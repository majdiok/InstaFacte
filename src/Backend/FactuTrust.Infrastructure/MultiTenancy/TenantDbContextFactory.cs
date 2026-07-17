using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.MultiTenancy;

/// <summary>
/// Factory for creating tenant-specific DbContext instances.
/// </summary>
public sealed class TenantDbContextFactory : ITenantDbContextFactory
{
    private readonly ITenantContext _tenantContext;
    private readonly IMediator _mediator;
    private readonly TenantAmbientTransaction _ambient;
    private readonly ILogger<TenantDbContext> _logger;

    public TenantDbContextFactory(
        ITenantContext tenantContext,
        IMediator mediator,
        TenantAmbientTransaction ambient,
        ILogger<TenantDbContext> logger)
    {
        _tenantContext = tenantContext;
        _mediator = mediator;
        _ambient = ambient;
        _logger = logger;
    }

    public TenantDbContext CreateContext()
    {
        // Transaction ambiante active (TenantUnitOfWork) : le contexte est construit sur la
        // MÊME connexion et enrôlé dans la MÊME transaction. Sans retry : interdit par EF
        // avec une transaction utilisateur (le retry est porté par le contexte racine du UoW).
        // Le contexte ne possède pas la connexion : son DisposeAsync ne la ferme pas.
        if (_ambient.IsActive)
        {
            var enlistedOptions = new DbContextOptionsBuilder<TenantDbContext>()
                .UseSqlServer(_ambient.Connection!)
                .Options;

            var enlisted = new TenantDbContext(enlistedOptions);
            enlisted.Database.UseTransaction(_ambient.Transaction);
            enlisted.SetMediator(_mediator);
            enlisted.SetLogger(_logger);
            return enlisted;
        }

        return CreateIsolatedContext();
    }

    public TenantDbContext CreateIsolatedContext()
    {
        var connectionString = _tenantContext.ConnectionString
            ?? throw new InvalidOperationException("Aucun contexte d'entreprise disponible. Assurez-vous que TenantMiddleware a été exécuté.");

        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlServer(
                connectionString,
                sqlServerOptions => sqlServerOptions
                    .EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: null)
                    .MigrationsAssembly(typeof(TenantDbContext).Assembly.FullName))
            .Options;

        var context = new TenantDbContext(options);
        context.SetMediator(_mediator);
        context.SetLogger(_logger);
        return context;
    }
}
