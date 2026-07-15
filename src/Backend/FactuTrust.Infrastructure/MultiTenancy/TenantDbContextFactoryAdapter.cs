using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.MultiTenancy;

/// <summary>
/// Adapter that implements IDbContextFactory for TenantDbContext
/// and resolves the tenant connection string dynamically at runtime.
/// </summary>
public sealed class TenantDbContextFactoryAdapter : IDbContextFactory<TenantDbContext>
{
    private readonly ITenantContext _tenantContext;
    private readonly IMediator _mediator;
    private readonly ILogger<TenantDbContext> _logger;

    public TenantDbContextFactoryAdapter(ITenantContext tenantContext, IMediator mediator, ILogger<TenantDbContext> logger)
    {
        _tenantContext = tenantContext;
        _mediator = mediator;
        _logger = logger;
    }

    public TenantDbContext CreateDbContext()
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
