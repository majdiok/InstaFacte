using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Treasury;
using FactuTrust.Application.Configuration;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.Background;

/// <summary>
/// Job Hangfire quotidien : recalcule la trésorerie prévisionnelle de tous les tenants actifs,
/// pour que l'écran s'ouvre sur une projection fraîche sans attendre un calcul à l'affichage.
/// </summary>
/// <remarks>
/// <para>
/// Même patron multi-tenant que <see cref="FiscalReminderJob"/> : la liste des tenants vient de la
/// base master, et l'échec d'un tenant n'empêche pas les suivants.
/// </para>
/// <para>
/// Le recalcul passe par <see cref="ICashFlowForecastService"/>, résolu dans une portée par tenant
/// avec son contexte ambiant : c'est le seul moyen d'obtenir la même projection que celle
/// déclenchée depuis l'écran, garde-fous et unité de travail compris.
/// </para>
/// </remarks>
public sealed class CashFlowRecomputationJob
{
    private readonly MasterDbContext _master;
    private readonly ITenantService _tenantService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TreasuryForecastOptions _options;
    private readonly ILogger<CashFlowRecomputationJob> _logger;

    public CashFlowRecomputationJob(
        MasterDbContext master,
        ITenantService tenantService,
        IServiceScopeFactory scopeFactory,
        IOptions<TreasuryForecastOptions> options,
        ILogger<CashFlowRecomputationJob> logger)
    {
        _master = master;
        _tenantService = tenantService;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        // Garde dans ExecuteAsync et non à l'enregistrement : le job est déclaré une fois pour
        // toutes, son activation reste une décision de configuration.
        if (!_options.Enabled || !_options.BackgroundRecomputeEnabled)
        {
            _logger.LogInformation(
                "CashFlowRecomputationJob : désactivé (TreasuryForecast:Enabled ou BackgroundRecomputeEnabled = false) — ignoré.");
            return;
        }

        var tenants = await _master.Tenants
            .AsNoTracking()
            .Where(t => t.IsActive)
            .Select(t => new { t.Id, Name = t.CompanyName })
            .ToListAsync(cancellationToken);

        var succeeded = 0;
        var failed = 0;

        foreach (var tenant in tenants)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var connectionString = await _tenantService.GetConnectionStringAsync(tenant.Id, cancellationToken);
                if (string.IsNullOrEmpty(connectionString)) continue;

                using var scope = _scopeFactory.CreateScope();

                var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
                tenantContext.SetTenant(tenant.Id, connectionString);

                var forecastService = scope.ServiceProvider.GetService<ICashFlowForecastService>();
                if (forecastService is null)
                {
                    _logger.LogInformation(
                        "CashFlowRecomputationJob : module non enregistré, arrêt sans traiter les tenants restants.");
                    return;
                }

                // userId null : c'est un recalcul automatique, il ne consomme pas le quota
                // journalier réservé aux recalculs manuels.
                var result = await forecastService.RecomputeAsync(
                    _options.DefaultHorizonMonths,
                    userId: null,
                    cancellationToken);

                if (result.IsSuccess)
                {
                    succeeded++;
                }
                else
                {
                    failed++;
                    _logger.LogWarning(
                        "CashFlowRecomputationJob : échec pour le tenant {Tenant} ({Error}).",
                        tenant.Name,
                        result.Error.Description);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                _logger.LogWarning(
                    ex,
                    "CashFlowRecomputationJob : exception sur le tenant {Tenant}, les suivants sont traités.",
                    tenant.Name);
            }
        }

        _logger.LogInformation(
            "CashFlowRecomputationJob : {Succeeded} projection(s) recalculée(s), {Failed} échec(s) sur {Total} tenant(s).",
            succeeded,
            failed,
            tenants.Count);
    }
}
