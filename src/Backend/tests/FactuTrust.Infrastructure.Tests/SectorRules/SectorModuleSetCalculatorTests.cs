using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Billing;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.SectorConfiguration;
using FactuTrust.Infrastructure.Services.SectorRules;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.SectorRules;

/// <summary>
/// Plan §4.2/§4.6 — <see cref="SectorModuleSetCalculator"/> dependency transitive-closure,
/// exercised directly (no DB, no <c>RegistrationSectorService</c> wrapper). <c>coreModules</c> is
/// deliberately passed as empty here (unlike the real registration flow, where
/// <c>SectorConfigurationCatalog.CoreModules</c> already contains Products/Sales/Treasury) so the
/// dependency pull is actually observable in the resulting set.
/// </summary>
public sealed class SectorModuleSetCalculatorTests
{
    private sealed class AllowAllPlanResolver : IPlanResolver
    {
        public Task<Plan?> GetPlanByCodeAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult<Plan?>(null);
        public Task<bool> HasFeatureAsync(SubscriptionPlan plan, string featureKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
        public Task<int> GetIntLimitAsync(SubscriptionPlan plan, string limitKey, int fallback, CancellationToken cancellationToken = default) =>
            Task.FromResult(fallback);
        public Task<bool> IsModuleAllowedAsync(SubscriptionPlan plan, int module, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private static IReadOnlyList<ModuleDependencySnapshot> CatalogDependencyEdges() =>
        SectorConfigurationCatalog.ModuleDependencies
            .Select(e => new ModuleDependencySnapshot { ModuleId = (int)e.Module, RequiredModuleId = (int)e.RequiredModule })
            .ToList();

    [Fact]
    public async Task Activer_Stock_tire_Products_par_fermeture_de_dependances()
    {
        var result = await SectorModuleSetCalculator.ComputeEnabledSetAsync(
            coreModules: Array.Empty<AppModule>(),
            seedModuleIds: new[] { (int)AppModule.Stock },
            plan: SubscriptionPlan.Monthly,
            dependencyEdges: CatalogDependencyEdges(),
            planResolver: new AllowAllPlanResolver(),
            userId: Guid.NewGuid(),
            logger: NullLogger.Instance,
            cancellationToken: CancellationToken.None);

        Assert.Contains(AppModule.Stock, result);
        Assert.Contains(AppModule.Products, result); // plan §4.2 approved edge: Stock -> Products.
    }

    [Fact]
    public async Task Activer_Purchases_tire_Products_et_activer_RecurringContracts_tire_Sales()
    {
        var result = await SectorModuleSetCalculator.ComputeEnabledSetAsync(
            coreModules: Array.Empty<AppModule>(),
            seedModuleIds: new[] { (int)AppModule.Purchases, (int)AppModule.RecurringContracts },
            plan: SubscriptionPlan.Monthly,
            dependencyEdges: CatalogDependencyEdges(),
            planResolver: new AllowAllPlanResolver(),
            userId: Guid.NewGuid(),
            logger: NullLogger.Instance,
            cancellationToken: CancellationToken.None);

        Assert.Contains(AppModule.Purchases, result);
        Assert.Contains(AppModule.Products, result); // Purchases -> Products.
        Assert.Contains(AppModule.RecurringContracts, result);
        Assert.Contains(AppModule.Sales, result); // RecurringContracts -> Sales.
    }

    [Fact]
    public async Task Activer_Forecasting_tire_Treasury()
    {
        var result = await SectorModuleSetCalculator.ComputeEnabledSetAsync(
            coreModules: Array.Empty<AppModule>(),
            seedModuleIds: new[] { (int)AppModule.Forecasting },
            plan: SubscriptionPlan.Monthly,
            dependencyEdges: CatalogDependencyEdges(),
            planResolver: new AllowAllPlanResolver(),
            userId: Guid.NewGuid(),
            logger: NullLogger.Instance,
            cancellationToken: CancellationToken.None);

        Assert.Contains(AppModule.Forecasting, result);
        Assert.Contains(AppModule.Treasury, result); // Forecasting -> Treasury.
    }

    [Fact]
    public async Task Aucune_dependance_tiree_quand_aucun_module_seed_ne_declenche_une_arete()
    {
        var result = await SectorModuleSetCalculator.ComputeEnabledSetAsync(
            coreModules: Array.Empty<AppModule>(),
            seedModuleIds: new[] { (int)AppModule.CRM },
            plan: SubscriptionPlan.Monthly,
            dependencyEdges: CatalogDependencyEdges(),
            planResolver: new AllowAllPlanResolver(),
            userId: Guid.NewGuid(),
            logger: NullLogger.Instance,
            cancellationToken: CancellationToken.None);

        Assert.Contains(AppModule.CRM, result);
        Assert.DoesNotContain(AppModule.Products, result);
        Assert.DoesNotContain(AppModule.Treasury, result);
        Assert.DoesNotContain(AppModule.Sales, result);
    }
}
