using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
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
    private readonly ILoggerFactory _loggerFactory;
    private readonly bool _enableSensitiveDataLogging;

    /// <summary>
    /// DI injecte <see cref="ILoggerFactory"/> et <see cref="IConfiguration"/> pour que le SQL
    /// du contexte tenant apparaisse enfin dans Serilog (le contexte isolé était jusqu'ici
    /// construit sans logger factory, donc muet). Le niveau effectif reste contrôlé par la
    /// section Serilog des <c>appsettings.json</c> (Warning en prod, Information en développement).
    /// </summary>
    public TenantDbContextFactory(
        ITenantContext tenantContext,
        IMediator mediator,
        TenantAmbientTransaction ambient,
        ILogger<TenantDbContext> logger,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        _tenantContext = tenantContext;
        _mediator = mediator;
        _ambient = ambient;
        _logger = logger;
        _loggerFactory = loggerFactory;
        // Valeurs de paramètres SQL (PII possible) : opt-in explicite ET développement uniquement.
        _enableSensitiveDataLogging = hostEnvironment.IsDevelopment()
            && configuration.GetValue<bool>("Diagnostics:TenantSqlSensitiveLogging");
    }

    public TenantDbContext CreateContext()
    {
        // Transaction ambiante active (TenantUnitOfWork) : le contexte est construit sur la
        // MÊME connexion et enrôlé dans la MÊME transaction. Sans retry : interdit par EF
        // avec une transaction utilisateur (le retry est porté par le contexte racine du UoW).
        // Le contexte ne possède pas la connexion : son DisposeAsync ne la ferme pas.
        if (_ambient.IsActive)
        {
            var enlistedOptionsBuilder = new DbContextOptionsBuilder<TenantDbContext>()
                .UseSqlServer(_ambient.Connection!);
            ApplyDiagnostics(enlistedOptionsBuilder);

            var enlisted = new TenantDbContext(enlistedOptionsBuilder.Options);
            enlisted.Database.UseTransaction(_ambient.Transaction);
            enlisted.SetMediator(_mediator);
            enlisted.SetLogger(_logger);
            return enlisted;
        }

        return CreateIsolatedContext();
    }

    public TenantDbContext CreateIsolatedContext() =>
        CreateIsolatedContext(
            _tenantContext.ConnectionString
            ?? throw new InvalidOperationException("Aucun contexte d'entreprise disponible. Assurez-vous que TenantMiddleware a été exécuté."));

    public TenantDbContext CreateIsolatedContext(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string is required.", nameof(connectionString));

        var optionsBuilder = new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlServer(
                connectionString,
                sqlServerOptions => sqlServerOptions
                    .EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: null)
                    .MigrationsAssembly(typeof(TenantDbContext).Assembly.FullName));
        ApplyDiagnostics(optionsBuilder);

        var context = new TenantDbContext(optionsBuilder.Options);
        context.SetMediator(_mediator);
        context.SetLogger(_logger);
        return context;
    }

    /// <summary>
    /// Branche ILoggerFactory et EnableDetailedErrors sur le builder d'options.
    /// EnableSensitiveDataLogging n'est activé qu'en développement, sur opt-in explicite
    /// (<c>Diagnostics:TenantSqlSensitiveLogging</c>), car il logue les valeurs des paramètres.
    /// </summary>
    private void ApplyDiagnostics(DbContextOptionsBuilder<TenantDbContext> builder)
    {
        builder
            .UseLoggerFactory(_loggerFactory)
            .EnableDetailedErrors();

        if (_enableSensitiveDataLogging)
        {
            builder.EnableSensitiveDataLogging();
        }
    }
}
