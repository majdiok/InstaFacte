using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Billing;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.SectorConfiguration;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Persistence.Seeds;
using FactuTrust.Infrastructure.Services;
using FactuTrust.Infrastructure.Services.SectorRules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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

    // ---------- ComputeAsync + real (DB-backed) plan resolver ----------

    private static MasterDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static DbPlanResolver NewDbPlanResolver(MasterDbContext db) =>
        new(db, new MemoryCache(new MemoryCacheOptions()));

    /// <summary>
    /// Plan §1.1/§1.2 — un plan restrictif (Stock/Purchases explicitement désactivés côté BD, via
    /// <see cref="DbPlanResolver"/> plutôt qu'un stub) doit faire remonter ces modules dans
    /// <see cref="ModuleSetComputationResult.DeniedByPlan"/> et les exclure du set final, alors même
    /// que le client les a explicitement demandés.
    /// </summary>
    [Fact]
    public async Task Plan_restrictif_refuse_Stock_et_Purchases_demandes_et_les_exclut_du_resultat()
    {
        await using var db = NewDb();
        var plan = Plan.Create(
            code: nameof(SubscriptionPlan.Monthly), name: "Mensuel", description: null,
            billingPeriod: BillingPeriod.Monthly, basePriceTND: 49m, isPublic: true,
            trialDays: 14, sortOrder: 1, currency: "TND");
        plan.ReplaceModules(Enum.GetValues<AppModule>().Select(m =>
            ((int)m, IsIncluded: m != AppModule.Stock && m != AppModule.Purchases)));
        db.Plans.Add(plan);
        await db.SaveChangesAsync();

        var result = await SectorModuleSetCalculator.ComputeAsync(
            coreModules: Array.Empty<AppModule>(),
            seedModuleIds: new[] { (int)AppModule.Stock, (int)AppModule.Purchases, (int)AppModule.CRM },
            plan: SubscriptionPlan.Monthly,
            dependencyEdges: CatalogDependencyEdges(),
            planResolver: NewDbPlanResolver(db),
            userId: Guid.NewGuid(),
            logger: NullLogger.Instance,
            cancellationToken: CancellationToken.None);

        Assert.Contains(AppModule.Stock, result.DeniedByPlan);
        Assert.Contains(AppModule.Purchases, result.DeniedByPlan);
        Assert.DoesNotContain(AppModule.Stock, result.EnabledModules);
        Assert.DoesNotContain(AppModule.Purchases, result.EnabledModules);
        Assert.Contains(AppModule.CRM, result.EnabledModules);
        // Products (pulled in as a Stock/Purchases dependency) is itself allowed by this plan, and
        // was never explicitly requested — it survives and must NOT show up as a denial.
        Assert.Contains(AppModule.Products, result.EnabledModules);
        Assert.DoesNotContain(AppModule.Products, result.DeniedByPlan);
        Assert.Empty(result.DroppedInvalidIds);
    }

    /// <summary>
    /// Plan §1.1, décision D1 — après le seed correctif (<see cref="PlanSeeder"/>), le plan Free doit
    /// autoriser tout module proposé par le wizard, même si une ligne <see cref="PlanModule"/> avait
    /// été explicitement désactivée (ex. restriction backoffice antérieure à D1). Vérifié ici via le
    /// vrai <see cref="DbPlanResolver"/>, pas un stub permissif.
    /// </summary>
    [Fact]
    public async Task Plan_Free_apres_correction_D1_autorise_tous_les_modules_proposes_par_le_wizard()
    {
        var dbName = Guid.NewGuid().ToString();
        await using (var arrangeDb = new MasterDbContext(new DbContextOptionsBuilder<MasterDbContext>()
                   .UseInMemoryDatabase(dbName).Options))
        {
            var free = Plan.Create(
                code: nameof(SubscriptionPlan.Free), name: "Gratuit", description: null,
                billingPeriod: BillingPeriod.Free, basePriceTND: 0m, isPublic: true,
                trialDays: 0, sortOrder: 0, currency: "TND");
            // Restriction backoffice antérieure à D1, sur des modules que le wizard offre bel et bien.
            free.ReplaceModules(Enum.GetValues<AppModule>().Select(m =>
                ((int)m, IsIncluded: m != AppModule.Stock && m != AppModule.Purchases && m != AppModule.Fiscal)));
            arrangeDb.Plans.Add(free);
            await arrangeDb.SaveChangesAsync();
        }

        await using var db = new MasterDbContext(new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(dbName).Options);
        await PlanSeeder.SeedAsync(db); // Triggers AlignFreePlanWizardModulesAsync (D1).

        var requested = PlanSeeder.WizardOfferedModuleIds.ToArray();
        var result = await SectorModuleSetCalculator.ComputeAsync(
            coreModules: Array.Empty<AppModule>(),
            seedModuleIds: requested,
            plan: SubscriptionPlan.Free,
            dependencyEdges: CatalogDependencyEdges(),
            planResolver: NewDbPlanResolver(db),
            userId: Guid.NewGuid(),
            logger: NullLogger.Instance,
            cancellationToken: CancellationToken.None);

        Assert.Empty(result.DeniedByPlan);
        Assert.Empty(result.DroppedInvalidIds);
        foreach (var moduleId in requested)
            Assert.Contains((AppModule)moduleId, result.EnabledModules);
    }
}
