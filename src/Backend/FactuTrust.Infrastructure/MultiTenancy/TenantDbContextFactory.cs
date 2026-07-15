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
    private readonly ILogger<TenantDbContext> _logger;

    public TenantDbContextFactory(ITenantContext tenantContext, IMediator mediator, ILogger<TenantDbContext> logger)
    {
        _tenantContext = tenantContext;
        _mediator = mediator;
        _logger = logger;
    }

    public TenantDbContext CreateContext()
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
