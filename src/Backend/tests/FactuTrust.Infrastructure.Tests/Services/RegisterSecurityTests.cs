using FactuTrust.Domain.Billing;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.SectorConfiguration;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Sécurité / anti-escalade de l'inscription sectorielle (plan §6.2, ligne "Sécurité / anti-escalade").
/// Contrairement à <see cref="RegistrationSectorServiceTests"/> (qui vérifie surtout des flags par
/// module), chaque test ici affirme le SET FINAL de modules réellement accordés à l'utilisateur —
/// c'est-à-dire ce que <see cref="EffectivePermissionService.ResolveEnabledModules"/> renvoie une
/// fois les lignes <c>UserModuleGrant</c> écrites par <see cref="RegistrationSectorService.ApplyModuleSelectionAsync"/>
/// relues, exactement le chemin qu'un token JWT recalculé à l'émission empruntera en production.
/// </summary>
public sealed class RegisterSecurityTests
{
    private static MasterDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static RegistrationSectorService NewService(
        MasterDbContext db,
        IPlanResolver planResolver,
        bool enabled = true,
        bool enforceSegmentDomainLinks = true)
    {
        return new RegistrationSectorService(
            db,
            planResolver,
            new FactuTrust.Infrastructure.Services.SectorCatalog.StaticSectorCatalogProvider(),
            Options.Create(new RegistrationSectorOptions { Enabled = enabled, EnforceSegmentDomainLinks = enforceSegmentDomainLinks }),
            NullLogger<RegistrationSectorService>.Instance);
    }

    /// <summary>Plan resolver stub that allows every module (mirrors DbPlanResolver's permissive fallback).</summary>
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

    /// <summary>Plan resolver stub that denies a specific set of modules — simulates a plan ceiling.</summary>
    private sealed class DenyingPlanResolver : IPlanResolver
    {
        private readonly HashSet<AppModule> _denied;
        public DenyingPlanResolver(IEnumerable<AppModule> denied) => _denied = new HashSet<AppModule>(denied);

        public Task<Plan?> GetPlanByCodeAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult<Plan?>(null);
        public Task<bool> HasFeatureAsync(SubscriptionPlan plan, string featureKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
        public Task<int> GetIntLimitAsync(SubscriptionPlan plan, string limitKey, int fallback, CancellationToken cancellationToken = default) =>
            Task.FromResult(fallback);
        public Task<bool> IsModuleAllowedAsync(SubscriptionPlan plan, int module, CancellationToken cancellationToken = default) =>
            Task.FromResult(!_denied.Contains((AppModule)module));
    }

    /// <summary>
    /// Résout le set final de modules réellement accordés pour <paramref name="userId"/>, en
    /// simulant un acteur dont le rôle porte la permission universelle (le filtre rôle∩module est
    /// donc un no-op) — isole ainsi purement la logique de plafonnement d'<c>ApplyModuleSelectionAsync</c>.
    /// </summary>
    private static async Task<HashSet<AppModule>> FinalGrantSetAsync(MasterDbContext db, Guid userId)
    {
        var grants = await db.UserModuleGrants.Where(g => g.UserId == userId).ToListAsync();
        var fullPermissionUniverse = AppModuleExtensions.GetAllModulesPermissionUniverse().ToHashSet();
        return EffectivePermissionService.ResolveEnabledModules(grants, fullPermissionUniverse).ToHashSet();
    }

    [Fact]
    public async Task EnabledModules_hors_plan_sont_ecartes_par_le_plafond_plan()
    {
        await using var db = NewDb();
        // Le plan ne couvre ni Stock ni CRM : même si le client les réclame explicitement, le set
        // final ne peut jamais les contenir — le plafond plan est appliqué côté serveur, jamais
        // accepté depuis le payload client.
        var service = NewService(db, new DenyingPlanResolver(new[] { AppModule.Stock, AppModule.CRM }));
        var userId = Guid.NewGuid();

        await service.ApplyModuleSelectionAsync(
            userId, null, new[] { (int)AppModule.Stock, (int)AppModule.CRM, (int)AppModule.Sales },
            SubscriptionPlan.Free, CancellationToken.None);
        await db.SaveChangesAsync();

        var finalSet = await FinalGrantSetAsync(db, userId);

        Assert.DoesNotContain(AppModule.Stock, finalSet);
        Assert.DoesNotContain(AppModule.CRM, finalSet);
        // Le set final reste borné aux modules cœur (toujours forcés) + ce qui est à la fois
        // demandé ET autorisé par le plan (Sales n'est pas refusé ci-dessus).
        var expected = SectorConfigurationCatalog.CoreModules.Concat(new[] { AppModule.Sales }).ToHashSet();
        Assert.Equal(expected, finalSet);
    }

    [Fact]
    public async Task EnabledModules_Honoraires_toujours_rejete()
    {
        await using var db = NewDb();
        // Honoraires est explicitement exclu du provisioning sectoriel (plan §3, "rien ne peut
        // tirer Honoraires") : même sous un plan qui l'autoriserait sans restriction, le module ne
        // doit jamais apparaître dans le set final.
        var service = NewService(db, new AllowAllPlanResolver());
        var userId = Guid.NewGuid();

        await service.ApplyModuleSelectionAsync(
            userId, null, new[] { (int)AppModule.Honoraires, (int)AppModule.Stock },
            SubscriptionPlan.Free, CancellationToken.None);
        await db.SaveChangesAsync();

        var finalSet = await FinalGrantSetAsync(db, userId);

        Assert.DoesNotContain(AppModule.Honoraires, finalSet);
        var expected = SectorConfigurationCatalog.CoreModules.Concat(new[] { AppModule.Stock }).ToHashSet();
        Assert.Equal(expected, finalSet);
    }

    [Fact]
    public async Task EnabledModules_ids_invalides_droppes()
    {
        await using var db = NewDb();
        // Des ids hors de l'énumération AppModule (négatifs, hors bornes, valeur arbitraire non
        // mappée) doivent être silencieusement écartés — jamais propagés jusqu'au set final, et ne
        // doivent jamais faire échouer l'appel.
        var service = NewService(db, new AllowAllPlanResolver());
        var userId = Guid.NewGuid();

        await service.ApplyModuleSelectionAsync(
            userId, null, new[] { -1, 9999, int.MaxValue, (int)AppModule.CRM },
            SubscriptionPlan.Free, CancellationToken.None);
        await db.SaveChangesAsync();

        var finalSet = await FinalGrantSetAsync(db, userId);

        var expected = SectorConfigurationCatalog.CoreModules.Concat(new[] { AppModule.CRM }).ToHashSet();
        Assert.Equal(expected, finalSet);
    }

    /// <summary>
    /// Test combiné "kitchen sink" : cumule dans une même requête un id invalide, Honoraires, et un
    /// module refusé par le plan, en plus de modules valides et autorisés — prouve que les trois
    /// mécanismes de restriction (filtre enum, rejet Honoraires, plafond plan) composent correctement
    /// et que le set final ne contient RIEN d'autre que : modules cœur + modules valides autorisés
    /// par le plan et effectivement demandés.
    /// </summary>
    [Fact]
    public async Task ApplyModuleSelection_final_grant_set_combine_toutes_les_restrictions_sans_escalade()
    {
        await using var db = NewDb();
        var service = NewService(db, new DenyingPlanResolver(new[] { AppModule.Payroll }));
        var userId = Guid.NewGuid();

        var requested = new[]
        {
            -1,                          // id invalide (négatif)
            9999,                        // id invalide (hors énumération)
            (int)AppModule.Honoraires,    // toujours rejeté
            (int)AppModule.Payroll,       // refusé par le plan
            (int)AppModule.Stock,         // valide et autorisé
            (int)AppModule.CRM,           // valide et autorisé
        };

        await service.ApplyModuleSelectionAsync(
            userId, null, requested, SubscriptionPlan.Free, CancellationToken.None);
        await db.SaveChangesAsync();

        var finalSet = await FinalGrantSetAsync(db, userId);

        var expected = SectorConfigurationCatalog.CoreModules
            .Concat(new[] { AppModule.Stock, AppModule.CRM })
            .ToHashSet();
        Assert.Equal(expected, finalSet);
        Assert.DoesNotContain(AppModule.Honoraires, finalSet);
        Assert.DoesNotContain(AppModule.Payroll, finalSet);
    }
}
