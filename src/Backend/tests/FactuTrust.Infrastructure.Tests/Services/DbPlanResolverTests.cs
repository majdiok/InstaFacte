using FactuTrust.Domain.Billing;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.SectorConfiguration;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Lot C1 — Tests de régression du <see cref="DbPlanResolver"/> ciblant le bug B2
/// (faille de sécurité : fallback <c>return true</c> quand un PlanModule était absent).
/// </summary>
public sealed class DbPlanResolverTests
{
    private static MasterDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static DbPlanResolver NewResolver(MasterDbContext db)
        => new(db, new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public async Task IsModuleAllowed_ReturnsFalse_WhenModuleAbsent_ButOthersConfigured()
    {
        // Arrange : plan "Free" avec UNIQUEMENT le module Clients explicitement inclus.
        // Tout autre module doit être REFUSÉ (le bug originel retournait true silencieusement).
        await using var db = NewDb();
        var plan = Plan.Create(
            code: nameof(SubscriptionPlan.Free),
            name: "Gratuit",
            description: null,
            billingPeriod: BillingPeriod.Free,
            basePriceTND: 0m);
        plan.ReplaceModules(new[] { ((int)AppModule.Clients, true) });
        db.Plans.Add(plan);
        await db.SaveChangesAsync();

        var resolver = NewResolver(db);

        // Act
        var aiAllowed = await resolver.IsModuleAllowedAsync(SubscriptionPlan.Free, (int)AppModule.AI);
        var clientsAllowed = await resolver.IsModuleAllowedAsync(SubscriptionPlan.Free, (int)AppModule.Clients);

        // Assert
        Assert.False(aiAllowed); // ← le fix : refus explicite, plus de faille
        Assert.True(clientsAllowed);
    }

    [Fact]
    public async Task IsModuleAllowed_ReturnsTrue_WhenDbPlanModulesEmpty()
    {
        // Arrange : plan "plat" pré-Lot C1 sans aucune ligne PlanModule.
        // Rétro-compat : tous les modules doivent rester accessibles tant que l'admin
        // n'a pas configuré la liste.
        await using var db = NewDb();
        var plan = Plan.Create(
            code: nameof(SubscriptionPlan.Free),
            name: "Gratuit",
            description: null,
            billingPeriod: BillingPeriod.Free,
            basePriceTND: 0m);
        // Pas de ReplaceModules → Modules.Count == 0
        db.Plans.Add(plan);
        await db.SaveChangesAsync();

        var resolver = NewResolver(db);

        // Act & Assert : tous les modules de l'enum doivent passer
        foreach (var mod in Enum.GetValues<AppModule>())
        {
            var allowed = await resolver.IsModuleAllowedAsync(SubscriptionPlan.Free, (int)mod);
            Assert.True(allowed, $"Module {mod} devrait être autorisé sur un plan plat.");
        }
    }

    /// <summary>
    /// Régression du bug « espace créé sans clients, sans produits, sans facturation » : un plan dont
    /// les lignes de modules CŒUR sont explicitement décochées produisait un tenant inutilisable, avec
    /// un bandeau annonçant à tort que ces modules n'étaient « pas inclus dans votre offre ».
    /// Les modules cœur ne sont plus soumis au plafond du plan ; les autres le restent strictement.
    /// </summary>
    [Fact]
    public async Task IsModuleAllowed_ReturnsTrue_ForCoreModules_EvenWhenPlanExplicitlyExcludesThem()
    {
        await using var db = NewDb();
        var plan = Plan.Create(
            code: nameof(SubscriptionPlan.Free),
            name: "Gratuit",
            description: null,
            billingPeriod: BillingPeriod.Free,
            basePriceTND: 0m);

        // Plan volontairement dégradé : TOUS les modules cœur décochés, plus un non-cœur décoché.
        plan.ReplaceModules(
            SectorConfigurationCatalog.CoreModules
                .Select(m => ((int)m, false))
                .Append(((int)AppModule.Stock, false))
                .ToList());
        db.Plans.Add(plan);
        await db.SaveChangesAsync();

        var resolver = NewResolver(db);

        foreach (var core in SectorConfigurationCatalog.CoreModules)
        {
            Assert.True(
                await resolver.IsModuleAllowedAsync(SubscriptionPlan.Free, (int)core),
                $"Le module cœur {core} ne doit jamais être refusé par un plan.");
        }

        // La garde reste stricte hors du cœur : un module non cœur décoché est toujours refusé,
        // sinon le correctif ouvrirait une faille au lieu d'en fermer une.
        Assert.False(await resolver.IsModuleAllowedAsync(SubscriptionPlan.Free, (int)AppModule.Stock));
    }

    /// <summary>
    /// Le cœur est immunisé même lorsque sa ligne est totalement ABSENTE du plan (cas distinct du
    /// précédent : `entry?.IsIncluded ?? false` refusait alors par défaut).
    /// </summary>
    [Fact]
    public async Task IsModuleAllowed_ReturnsTrue_ForCoreModules_WhenPlanRowIsMissingEntirely()
    {
        await using var db = NewDb();
        var plan = Plan.Create(
            code: nameof(SubscriptionPlan.Free),
            name: "Gratuit",
            description: null,
            billingPeriod: BillingPeriod.Free,
            basePriceTND: 0m);

        // Une seule ligne, non cœur : le plan est « configuré », donc la BD fait foi pour tout le reste.
        plan.ReplaceModules(new[] { ((int)AppModule.Stock, true) });
        db.Plans.Add(plan);
        await db.SaveChangesAsync();

        var resolver = NewResolver(db);

        foreach (var core in SectorConfigurationCatalog.CoreModules)
        {
            Assert.True(
                await resolver.IsModuleAllowedAsync(SubscriptionPlan.Free, (int)core),
                $"Le module cœur {core} doit être autorisé même sans ligne de plan.");
        }

        Assert.False(await resolver.IsModuleAllowedAsync(SubscriptionPlan.Free, (int)AppModule.AI));
    }
}
