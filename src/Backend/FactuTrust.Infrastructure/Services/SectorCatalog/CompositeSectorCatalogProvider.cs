using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.SectorConfiguration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.SectorCatalog;

/// <summary>
/// Registered <see cref="ISectorCatalogProvider"/> (plan §WP-B2, D2/D10). Flag off ⇒ static, no DB
/// touch. Flag on ⇒ tries the DB provider; any exception or an empty active-segment set logs a
/// warning and falls back to static. <b>Never throws</b> — a rule-table outage can never 500 the
/// registration or public catalog endpoints.
/// </summary>
public sealed class CompositeSectorCatalogProvider : ISectorCatalogProvider
{
    private readonly StaticSectorCatalogProvider _staticProvider;
    private readonly DbSectorCatalogProvider _dbProvider;
    private readonly RegistrationSectorOptions _options;
    private readonly ILogger<CompositeSectorCatalogProvider> _logger;

    public CompositeSectorCatalogProvider(
        StaticSectorCatalogProvider staticProvider,
        DbSectorCatalogProvider dbProvider,
        IOptions<RegistrationSectorOptions> options,
        ILogger<CompositeSectorCatalogProvider> logger)
    {
        _staticProvider = staticProvider;
        _dbProvider = dbProvider;
        _options = options.Value;
        _logger = logger;
    }

    public SectorRuleSnapshot GetSnapshot()
    {
        if (!_options.UseDbRules)
            return _staticProvider.GetSnapshot();

        try
        {
            var dbSnapshot = _dbProvider.GetSnapshot();
            if (dbSnapshot.Segments.Count == 0)
            {
                _logger.LogWarning("Sector DB rules unavailable or empty — falling back to static catalog");
                return _staticProvider.GetSnapshot();
            }

            return dbSnapshot;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sector DB rules unavailable or empty — falling back to static catalog");
            return _staticProvider.GetSnapshot();
        }
    }
}
