using FactuTrust.Domain.Billing;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Persistence.Seeds;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Lot C1 — Tests de régression du <see cref="PlanSeeder"/>.
/// Garantit l'idempotence, le backfill des Subscriptions historiques
/// et l'ajout automatique des modules manquants après extension d'<see cref="AppModule"/>.
/// </summary>
public sealed class PlanSeederTests
{
    private static MasterDbContext NewDb(string? dbName = null) =>
        new(new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task SeedAsync_IsIdempotent_AndBackfillsSubscriptionsAndModules()
    {
        // Partage la même in-memory DB entre les deux appels pour vérifier l'idempotence
        // (chaque DbContextOptions doit pointer sur le même nom).
        var dbName = Guid.NewGuid().ToString();

        // Préparation : une Subscription historique sans PlanId, attachée à l'enum Monthly.
        var tenantId = Guid.NewGuid();
        await using (var arrangeDb = NewDb(dbName))
        {
            var sub = Subscription.CreateTrial(tenantId);
            arrangeDb.Subscriptions.Add(sub);
            await arrangeDb.SaveChangesAsync();
        }

        // 1er passage du seed
        await using (var db1 = NewDb(dbName))
        {
            await PlanSeeder.SeedAsync(db1);
        }

        // 2e passage : ne doit pas dupliquer les plans
        await using (var db2 = NewDb(dbName))
        {
            await PlanSeeder.SeedAsync(db2);
        }

        // Vérification finale
        await using var assertDb = NewDb(dbName);
        var allPlans = await assertDb.Plans.Include(p => p.Modules).ToListAsync();

        // (a) Idempotence : exactement 3 plans Free/Monthly/Annual
        Assert.Equal(3, allPlans.Count);
        Assert.Contains(allPlans, p => p.Code == nameof(SubscriptionPlan.Free));
        Assert.Contains(allPlans, p => p.Code == nameof(SubscriptionPlan.Monthly));
        Assert.Contains(allPlans, p => p.Code == nameof(SubscriptionPlan.Annual));

        // (b) Backfill modules : tous les plans doivent contenir les 13 AppModule.
        var allModuleValues = Enum.GetValues<AppModule>().Select(m => (int)m).ToHashSet();
        foreach (var plan in allPlans)
        {
            var present = plan.Modules.Select(m => m.Module).ToHashSet();
            Assert.True(allModuleValues.SetEquals(present),
                $"Plan {plan.Code} : modules manquants {string.Join(",", allModuleValues.Except(present))} " +
                $"ou en trop {string.Join(",", present.Except(allModuleValues))}.");
        }

        // (c) Backfill Subscription.PlanId : la sub trial du tenant doit avoir reçu le PlanId du plan Monthly.
        var monthlyPlanId = allPlans.Single(p => p.Code == nameof(SubscriptionPlan.Monthly)).Id;
        var backfilledSub = await assertDb.Subscriptions
            .AsNoTracking()
            .SingleAsync(s => s.TenantId == tenantId);
        Assert.Equal(monthlyPlanId, backfilledSub.PlanId);
    }

    [Fact]
    public async Task SeedAsync_BackfillsStalePlanLimits_ToCurrentConstants()
    {
        var dbName = Guid.NewGuid().ToString();

        // Plan Free préexistant avec une limite OBSOLÈTE (MaxCustomEntities = 10) — le seed insert-only
        // ne la mettrait jamais à jour ; le backfill des limites doit la réaligner sur la constante (50).
        await using (var arrangeDb = NewDb(dbName))
        {
            var free = Plan.Create(
                code: nameof(SubscriptionPlan.Free), name: "Gratuit", description: "ancien",
                billingPeriod: BillingPeriod.Free, basePriceTND: 0m, isPublic: true,
                trialDays: 0, sortOrder: 0, currency: "TND");
            free.ReplaceLimits(new[] { ("MaxCustomEntities", "10") });
            arrangeDb.Plans.Add(free);
            await arrangeDb.SaveChangesAsync();
        }

        await using (var db = NewDb(dbName))
        {
            await PlanSeeder.SeedAsync(db);
        }

        await using var assertDb = NewDb(dbName);
        var freePlan = await assertDb.Plans.Include(p => p.Limits)
            .SingleAsync(p => p.Code == nameof(SubscriptionPlan.Free));
        var maxEntities = freePlan.Limits.Single(l => l.Key == "MaxCustomEntities").Value;
        Assert.Equal(SubscriptionLimits.Free.MaxCustomEntities.ToString(), maxEntities);
        Assert.Equal("50", maxEntities);
    }
}
