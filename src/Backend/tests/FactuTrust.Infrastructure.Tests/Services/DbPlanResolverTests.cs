using FactuTrust.Domain.Billing;
using FactuTrust.Domain.Enums;
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
}
