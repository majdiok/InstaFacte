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

    /// <summary>
    /// Plan §1.1, décision D1 — un plan Free préexistant dont un opérateur a explicitement
    /// désactivé un module proposé par le wizard d'inscription (ex. Stock/Purchases) doit être
    /// réaligné (IsIncluded=true) par le seed, contrairement à <see cref="BackfillPlanModulesAsync"/>
    /// qui n'ajoute que les lignes manquantes. Monthly/Annual ne sont volontairement PAS touchés —
    /// une restriction sur ces plans reste possible et intentionnelle.
    /// </summary>
    [Fact]
    public async Task SeedAsync_AlignsFreePlanWizardModules_ButLeavesMonthlyAndAnnualRestrictionsIntact()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var arrangeDb = NewDb(dbName))
        {
            var free = Plan.Create(
                code: nameof(SubscriptionPlan.Free), name: "Gratuit", description: "ancien",
                billingPeriod: BillingPeriod.Free, basePriceTND: 0m, isPublic: true,
                trialDays: 0, sortOrder: 0, currency: "TND");
            // Un opérateur a restreint le plan Free via le backoffice : Stock et Purchases (offerts
            // par le wizard) sont explicitement désactivés — c'est exactement le cas non couvert par
            // BackfillPlanModulesAsync (qui n'ajoute que les modules ABSENTS).
            free.ReplaceModules(Enum.GetValues<AppModule>().Select(m =>
                ((int)m, IsIncluded: m != AppModule.Stock && m != AppModule.Purchases)));
            arrangeDb.Plans.Add(free);

            var monthly = Plan.Create(
                code: nameof(SubscriptionPlan.Monthly), name: "Mensuel", description: "ancien",
                billingPeriod: BillingPeriod.Monthly, basePriceTND: 49m, isPublic: true,
                trialDays: 14, sortOrder: 1, currency: "TND");
            // Restriction intentionnelle sur un plan payant : ne doit JAMAIS être touchée par D1
            // (qui ne s'applique qu'au plan Free).
            monthly.ReplaceModules(Enum.GetValues<AppModule>().Select(m =>
                ((int)m, IsIncluded: m != AppModule.Stock)));
            arrangeDb.Plans.Add(monthly);

            await arrangeDb.SaveChangesAsync();
        }

        await using (var db1 = NewDb(dbName))
        {
            await PlanSeeder.SeedAsync(db1);
        }

        await using (var assertDb1 = NewDb(dbName))
        {
            var free = await assertDb1.Plans.Include(p => p.Modules)
                .SingleAsync(p => p.Code == nameof(SubscriptionPlan.Free));
            Assert.True(free.Modules.Single(m => m.Module == (int)AppModule.Stock).IsIncluded);
            Assert.True(free.Modules.Single(m => m.Module == (int)AppModule.Purchases).IsIncluded);

            var monthly = await assertDb1.Plans.Include(p => p.Modules)
                .SingleAsync(p => p.Code == nameof(SubscriptionPlan.Monthly));
            Assert.False(monthly.Modules.Single(m => m.Module == (int)AppModule.Stock).IsIncluded,
                "Monthly's intentional restriction must not be touched by the Free-only D1 fix.");
        }

        // Idempotence : un second passage ne doit rien changer (ni lever d'exception).
        await using (var db2 = NewDb(dbName))
        {
            await PlanSeeder.SeedAsync(db2);
        }

        await using var assertDb2 = NewDb(dbName);
        var freeAfterSecondRun = await assertDb2.Plans.Include(p => p.Modules)
            .SingleAsync(p => p.Code == nameof(SubscriptionPlan.Free));
        Assert.True(freeAfterSecondRun.Modules.Single(m => m.Module == (int)AppModule.Stock).IsIncluded);
        Assert.True(freeAfterSecondRun.Modules.Single(m => m.Module == (int)AppModule.Purchases).IsIncluded);

        var monthlyAfterSecondRun = await assertDb2.Plans.Include(p => p.Modules)
            .SingleAsync(p => p.Code == nameof(SubscriptionPlan.Monthly));
        Assert.False(monthlyAfterSecondRun.Modules.Single(m => m.Module == (int)AppModule.Stock).IsIncluded);
    }

    /// <summary>
    /// Plan §1.1, décision D1 (RÉSOLU — Free = cœur + standard, pas de premium) : un seed frais du
    /// plan Free doit semer les modules premium (AI/Forecasting/Studio/Payroll) à
    /// <c>IsIncluded=false</c> et tout le reste (cœur + standard + Honoraires) à <c>true</c>.
    /// Monthly/Annual restent <see cref="PlanSeeder"/>.<c>AllModulesIncluded</c> (tout inclus, y
    /// compris premium — le plafond premium ne s'applique qu'au plan Free).
    /// </summary>
    [Fact]
    public async Task SeedAsync_FreePlan_excludes_premium_modules_but_includes_core_and_standard()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var db = NewDb(dbName))
        {
            await PlanSeeder.SeedAsync(db);
        }

        await using var assertDb = NewDb(dbName);
        var plans = await assertDb.Plans.Include(p => p.Modules).ToListAsync();
        var free = plans.Single(p => p.Code == nameof(SubscriptionPlan.Free));
        var monthly = plans.Single(p => p.Code == nameof(SubscriptionPlan.Monthly));
        var annual = plans.Single(p => p.Code == nameof(SubscriptionPlan.Annual));

        var premium = AppModuleExtensions.PaidPlanModuleIds.Select(m => (int)m).ToHashSet();

        // Free : premium OFF, tout le reste ON (y compris Honoraires, inchangé — natif cabinet).
        Assert.All(free.Modules.Where(m => premium.Contains(m.Module)),
            m => Assert.False(m.IsIncluded, $"Free must NOT include premium module {(AppModule)m.Module}."));
        Assert.All(free.Modules.Where(m => !premium.Contains(m.Module)),
            m => Assert.True(m.IsIncluded, $"Free must include non-premium module {(AppModule)m.Module}."));
        // Honoraires reste inclus sur Free (hors périmètre du plafond premium, inchangé).
        Assert.True(free.Modules.Single(m => m.Module == (int)AppModule.Honoraires).IsIncluded);

        // Monthly/Annual : tout inclus (aucun plafond premium).
        foreach (var plan in new[] { monthly, annual })
        {
            Assert.All(plan.Modules,
                m => Assert.True(m.IsIncluded, $"{plan.Code} must include every module, including premium {(AppModule)m.Module}."));
        }
    }

    /// <summary>
    /// Plan §1.1, décision D1 — un plan Free préexistant (legacy) dont les modules premium ont été
    /// laissés à <c>IsIncluded=true</c> (le seed précédent semait <c>AllModulesIncluded</c>) doit être
    /// corrigé par la passe premium → false de <c>AlignFreePlanWizardModulesAsync</c>, et ce de façon
    /// idempotente (un second passage ne change rien et ne lève pas d'exception). Symétrique au test
    /// wizard false → true ci-dessus.
    /// </summary>
    [Fact]
    public async Task SeedAsync_AlignsFreePlanPremiumModulesFalse_Idempotently()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var arrangeDb = NewDb(dbName))
        {
            var free = Plan.Create(
                code: nameof(SubscriptionPlan.Free), name: "Gratuit", description: "ancien",
                billingPeriod: BillingPeriod.Free, basePriceTND: 0m, isPublic: true,
                trialDays: 0, sortOrder: 0, currency: "TND");
            // Legacy : tout inclus (premium compris) — exactement l'état laissé par l'ancien seed.
            free.ReplaceModules(Enum.GetValues<AppModule>().Select(m => ((int)m, true)));
            arrangeDb.Plans.Add(free);
            await arrangeDb.SaveChangesAsync();
        }

        var premium = AppModuleExtensions.PaidPlanModuleIds.Select(m => (int)m).ToHashSet();

        await using (var db1 = NewDb(dbName))
        {
            await PlanSeeder.SeedAsync(db1);
        }

        await using var assertDb1 = NewDb(dbName);
        var free1 = await assertDb1.Plans.Include(p => p.Modules)
            .SingleAsync(p => p.Code == nameof(SubscriptionPlan.Free));
        foreach (var id in premium)
            Assert.False(free1.Modules.Single(m => m.Module == id).IsIncluded,
                $"Premium module {(AppModule)id} must be flipped to IsIncluded=false on Free (D1).");

        // Idempotence : un second passage ne doit rien changer (ni lever d'exception).
        await using (var db2 = NewDb(dbName))
        {
            await PlanSeeder.SeedAsync(db2);
        }

        await using var assertDb2 = NewDb(dbName);
        var free2 = await assertDb2.Plans.Include(p => p.Modules)
            .SingleAsync(p => p.Code == nameof(SubscriptionPlan.Free));
        foreach (var id in premium)
            Assert.False(free2.Modules.Single(m => m.Module == id).IsIncluded);
    }
}
