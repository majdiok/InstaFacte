using System.Reflection;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class PlanQuotaServiceTests
{
    private static MasterDbContext NewDb(string? dbName = null) =>
        new(new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options);

    private static PlanQuotaService CreateService(
        MasterDbContext db,
        Mock<IPlanResolver>? planResolverMock = null)
    {
        var resolver = planResolverMock ?? new Mock<IPlanResolver>();
        return new PlanQuotaService(db, resolver.Object, NullLogger<PlanQuotaService>.Instance);
    }

    private static void SetInvoicesThisMonth(Subscription sub, int count)
    {
        typeof(Subscription)
            .GetProperty(nameof(Subscription.InvoicesThisMonth))!
            .SetValue(sub, count);
    }

    private static void SetStatus(Subscription sub, SubscriptionStatus status)
    {
        typeof(Subscription)
            .GetProperty(nameof(Subscription.Status))!
            .SetValue(sub, status);
    }

    [Fact]
    public async Task EnsureCanCreateInvoiceAsync_AllowsActiveMonthlyUnlimited()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb();
        var sub = Subscription.CreateTrial(tenantId);
        SetInvoicesThisMonth(sub, 100);
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        var resolver = new Mock<IPlanResolver>();
        resolver
            .Setup(r => r.GetIntLimitAsync(
                SubscriptionPlan.Monthly,
                "MaxInvoicesPerMonth",
                SubscriptionLimits.Monthly.MaxInvoicesPerMonth,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(int.MaxValue);

        var svc = CreateService(db, resolver);
        var result = await svc.EnsureCanCreateInvoiceAsync(tenantId);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task EnsureCanCreateInvoiceAsync_BlocksFreePlanAtQuota_WithEnumFallbackWhenDbMissing()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb();
        var sub = Subscription.CreateFree(tenantId);
        SetInvoicesThisMonth(sub, SubscriptionLimits.Free.MaxInvoicesPerMonth);
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        var resolver = new Mock<IPlanResolver>();
        resolver
            .Setup(r => r.GetIntLimitAsync(
                SubscriptionPlan.Free,
                "MaxInvoicesPerMonth",
                SubscriptionLimits.Free.MaxInvoicesPerMonth,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SubscriptionLimits.Free.MaxInvoicesPerMonth);

        var svc = CreateService(db, resolver);
        var result = await svc.EnsureCanCreateInvoiceAsync(tenantId);

        Assert.True(result.IsFailure);
        Assert.Equal(PlanQuotaService.QuotaExceededCode, result.Error.Code);
        Assert.Contains("10/10", result.Error.Description);
    }

    [Fact]
    public async Task EnsureCanCreateInvoiceAsync_BlocksCancelledSubscription_WithStatusCode()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb();
        var sub = Subscription.CreateTrial(tenantId);
        sub.Cancel("Test cancellation");
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var result = await svc.EnsureCanCreateInvoiceAsync(tenantId);

        Assert.True(result.IsFailure);
        Assert.Equal(PlanQuotaService.SubscriptionCancelledCode, result.Error.Code);
        Assert.Contains("annulé", result.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnsureCanCreateInvoiceAsync_BlocksSuspendedSubscription_WithStatusCode()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb();
        var sub = Subscription.CreateTrial(tenantId);
        sub.Suspend("Test suspension");
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var result = await svc.EnsureCanCreateInvoiceAsync(tenantId);

        Assert.True(result.IsFailure);
        Assert.Equal(PlanQuotaService.SubscriptionSuspendedCode, result.Error.Code);
    }

    [Fact]
    public async Task EnsureCanCreateInvoiceAsync_AllowsUnderQuotaFreePlan()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb();
        var sub = Subscription.CreateFree(tenantId);
        SetInvoicesThisMonth(sub, 3);
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        var resolver = new Mock<IPlanResolver>();
        resolver
            .Setup(r => r.GetIntLimitAsync(
                SubscriptionPlan.Free,
                "MaxInvoicesPerMonth",
                SubscriptionLimits.Free.MaxInvoicesPerMonth,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SubscriptionLimits.Free.MaxInvoicesPerMonth);

        var svc = CreateService(db, resolver);
        var result = await svc.EnsureCanCreateInvoiceAsync(tenantId);

        Assert.True(result.IsSuccess);
    }
}
