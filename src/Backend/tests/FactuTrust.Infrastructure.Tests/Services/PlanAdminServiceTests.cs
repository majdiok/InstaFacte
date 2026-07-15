using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Billing;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Lot C1 — Tests de régression du <see cref="PlanAdminService"/> ciblant le bug B1
/// (compteur SubscriptionsCount basé sur Subscription.Plan.ToString() au lieu de PlanId).
/// </summary>
public sealed class PlanAdminServiceTests
{
    private static MasterDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task ListAsync_ReturnsCorrectCount_ForCustomPlanWithPlanIdFk()
    {
        // Arrange : un plan custom (code non-seed) avec 2 Subscriptions attachées via FK PlanId.
        await using var db = NewDb();
        var customPlan = Plan.Create(
            code: "PRO_2026",
            name: "Pro 2026",
            description: null,
            billingPeriod: BillingPeriod.Monthly,
            basePriceTND: 99m);
        db.Plans.Add(customPlan);
        await db.SaveChangesAsync();

        var sub1 = Subscription.CreateFree(Guid.NewGuid());
        sub1.AttachToPlan(customPlan.Id, SubscriptionPlan.Monthly);
        var sub2 = Subscription.CreateFree(Guid.NewGuid());
        sub2.AttachToPlan(customPlan.Id, SubscriptionPlan.Monthly);
        db.Subscriptions.AddRange(sub1, sub2);
        await db.SaveChangesAsync();

        var service = new PlanAdminService(db, NullLogger<PlanAdminService>.Instance);

        // Act
        var list = await service.ListAsync(includeArchived: false);

        // Assert : le bug originel retournait 0 pour les plans custom (enum match impossible).
        var pro2026 = Assert.Single(list, p => p.Code == "PRO_2026");
        Assert.Equal(2, pro2026.SubscriptionsCount);
    }

    [Fact]
    public async Task ListAsync_StillCountsLegacySubscriptions_WhereSeedCodeMatches()
    {
        // Arrange : plan seed "Free" + 3 Subscriptions historiques sans PlanId
        // (cas pré-Lot C1 jamais backfillées).
        await using var db = NewDb();
        var freePlan = Plan.Create(
            code: nameof(SubscriptionPlan.Free),
            name: "Gratuit",
            description: null,
            billingPeriod: BillingPeriod.Free,
            basePriceTND: 0m);
        db.Plans.Add(freePlan);
        await db.SaveChangesAsync();

        for (var i = 0; i < 3; i++)
        {
            db.Subscriptions.Add(Subscription.CreateFree(Guid.NewGuid()));
        }
        await db.SaveChangesAsync();

        var service = new PlanAdminService(db, NullLogger<PlanAdminService>.Instance);

        // Act
        var list = await service.ListAsync(includeArchived: false);

        // Assert : rétro-compat préservée — les 3 subs sans PlanId comptent pour le plan Free.
        var free = Assert.Single(list, p => p.Code == nameof(SubscriptionPlan.Free));
        Assert.Equal(3, free.SubscriptionsCount);
    }
}
