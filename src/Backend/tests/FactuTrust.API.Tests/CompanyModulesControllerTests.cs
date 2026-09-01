using FactuTrust.API.Controllers;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.SectorConfiguration;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.API.Tests;

/// <summary>
/// Plan §2.2 — direct (non-HTTP) unit coverage for <see cref="CompanyModulesController"/> plan
/// resolution determinism (code-review fix) and tenant-wide grant application (D4, no cap). These
/// do not boot WebApplicationFactory (no SQL Server available in this sandbox) — the controller is
/// constructed directly against an InMemory <see cref="MasterDbContext"/> with mocked collaborators.
/// </summary>
public sealed class CompanyModulesControllerTests
{
    [Fact]
    public async Task ResolveTenantPlan_ignores_expired_subscription_inserted_first()
    {
        // An Expired subscription is inserted FIRST with a later StartDate than the Active one.
        // The old unfiltered FirstOrDefault (InMemory insertion order) would have resolved the
        // Expired plan; the fix's Active/Trial filter must select the Active plan.
        var tenantId = Guid.NewGuid();
        await using var db = NewMasterDb();

        await AddSubscriptionAsync(db, tenantId, SubscriptionStatus.Expired, SubscriptionPlan.Annual, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await AddSubscriptionAsync(db, tenantId, SubscriptionStatus.Active, SubscriptionPlan.Monthly, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await AddTenantAsync(db, tenantId);

        var controller = BuildController(db, tenantId);

        var result = await controller.GetModules(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var api = Assert.IsType<FactuTrust.API.Controllers.ApiResponse<CompanyModulesDto>>(ok.Value);
        Assert.True(api.Success);
        Assert.Equal(SubscriptionPlan.Monthly.ToString(), api.Data!.PlanCode);
    }

    [Fact]
    public async Task ResolveTenantPlan_picks_most_recently_started_active_subscription()
    {
        // Two Active subscriptions: the earlier-Started one (Monthly) is inserted first. The fix's
        // OrderByDescending(StartDate) must deterministically pick the later-Started one (Annual),
        // not InMemory insertion order.
        var tenantId = Guid.NewGuid();
        await using var db = NewMasterDb();

        await AddSubscriptionAsync(db, tenantId, SubscriptionStatus.Active, SubscriptionPlan.Monthly, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await AddSubscriptionAsync(db, tenantId, SubscriptionStatus.Active, SubscriptionPlan.Annual, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await AddTenantAsync(db, tenantId);

        var controller = BuildController(db, tenantId);

        var result = await controller.GetModules(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var api = Assert.IsType<FactuTrust.API.Controllers.ApiResponse<CompanyModulesDto>>(ok.Value);
        Assert.True(api.Success);
        Assert.Equal(SubscriptionPlan.Annual.ToString(), api.Data!.PlanCode);
    }

    [Fact]
    public async Task UpdateModules_applies_grants_to_every_active_user_with_no_cap()
    {
        // Code-review fix (D4): PUT /api/company/modules must process EVERY active user. The old
        // .Take(500) silently left a tail with stale grants for tenants with more than 500 active
        // users. Seeding 501 active users (above the historical cap) plus one inactive user proves
        // every active user is kept in sync and the inactive one is left untouched.
        const int activeUserCount = 501;
        var tenantId = Guid.NewGuid();
        await using var db = NewMasterDb();
        await AddTenantAsync(db, tenantId);
        await AddSubscriptionAsync(db, tenantId, SubscriptionStatus.Active, SubscriptionPlan.Annual, DateTime.UtcNow);

        var activeUserIds = new List<Guid>();
        for (var i = 0; i < activeUserCount; i++)
            activeUserIds.Add(AddUser(db, tenantId, isActive: true));
        var inactiveUserId = AddUser(db, tenantId, isActive: false);
        await db.SaveChangesAsync();

        var controller = BuildController(db, tenantId);

        // Request every offered module (all except the firm-native Honoraires) so nothing is dropped
        // and no plan-denial warning is emitted.
        var requestedIds = AppModuleExtensions.AllValues
            .Where(m => m != AppModule.Honoraires)
            .Select(m => (int)m)
            .ToArray();
        var result = await controller.UpdateModules(
            new UpdateCompanyModulesRequestDto { EnabledModuleIds = requestedIds },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var api = Assert.IsType<FactuTrust.API.Controllers.ApiResponse<UpdateCompanyModulesResultDto>>(ok.Value);
        Assert.True(api.Success);

        var expectedEnabled = requestedIds.OrderBy(x => x).ToArray();
        Assert.Equal(expectedEnabled, api.Data!.EnabledModuleIds.ToArray());
        Assert.Empty(api.Data.Warnings); // no "processed only the first N users" warning — cap removed.

        var expectedSet = requestedIds.Select(i => (AppModule)i).ToHashSet();

        // Every active user has its grants rewritten to the final set.
        foreach (var userId in activeUserIds)
        {
            var grants = await db.UserModuleGrants.AsNoTracking().Where(g => g.UserId == userId).ToListAsync();
            Assert.Equal(AppModuleExtensions.AllValues.Length, grants.Count);
            var enabledSet = grants.Where(g => g.IsEnabled).Select(g => g.Module).ToHashSet();
            Assert.True(expectedSet.SetEquals(enabledSet));
        }

        // The inactive user is never touched.
        var inactiveGrants = await db.UserModuleGrants.AsNoTracking().Where(g => g.UserId == inactiveUserId).ToListAsync();
        Assert.Empty(inactiveGrants);

        // Exactly one audit row for the tenant-wide change.
        var audit = await db.ModuleGrantAuditEntries.AsNoTracking().ToListAsync();
        Assert.Single(audit);
        Assert.Equal("company-modules-update", audit[0].Action);
    }

    private static MasterDbContext NewMasterDb() =>
        new(new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static async Task AddTenantAsync(MasterDbContext db, Guid tenantId)
    {
        var nif = NIF.Create("1234567/A/B/C/000").Value;
        var address = Address.Create("1 rue Test", "Tunis", "Tunis", postalCode: "1000").Value;
        var email = Email.Create("tenant@test.tn").Value;
        var phone = PhoneNumber.Create("20123456").Value;
        var tenant = Tenant.Create("Ste Test", nif, address, email, phone, TaxRegime.RealRegime).Value;
        typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(tenant, tenantId);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
    }

    private static async Task AddSubscriptionAsync(
        MasterDbContext db, Guid tenantId, SubscriptionStatus status, SubscriptionPlan plan, DateTime startDate)
    {
        var sub = Subscription.CreateFree(tenantId);
        typeof(Subscription).GetProperty(nameof(Subscription.Status))!.SetValue(sub, status);
        typeof(Subscription).GetProperty(nameof(Subscription.Plan))!.SetValue(sub, plan);
        typeof(Subscription).GetProperty(nameof(Subscription.StartDate))!.SetValue(sub, startDate);
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();
    }

    private static Guid AddUser(MasterDbContext db, Guid tenantId, bool isActive)
    {
        var id = Guid.NewGuid();
        db.Users.Add(new ApplicationUser
        {
            Id = id,
            Email = $"user-{id:N}@test.tn",
            UserName = $"user-{id:N}@test.tn",
            FirstName = "U",
            LastName = "Test",
            TenantId = tenantId,
            IsActive = isActive
        });
        return id;
    }

    private static CompanyModulesController BuildController(MasterDbContext db, Guid tenantId)
    {
        var tenantContext = new Mock<ITenantContext>();
        tenantContext.Setup(t => t.TenantId).Returns(tenantId);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(c => c.UserId).Returns(Guid.NewGuid());

        var planResolver = new Mock<IPlanResolver>();
        planResolver.Setup(p => p.IsModuleAllowedAsync(It.IsAny<SubscriptionPlan>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var catalogProvider = new Mock<ISectorCatalogProvider>();
        catalogProvider.Setup(c => c.GetSnapshot()).Returns(SectorConfigurationCatalog.BuildCatalogSnapshot());

        var registrationSector = new Mock<IRegistrationSectorService>();
        registrationSector.Setup(r => r.ResolveProfile(It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns<string?, string?>((seg, dom) =>
                Result<SectorProfile?>.Success(SectorConfigurationCatalog.Resolve(seg, dom)));

        return new CompanyModulesController(
            db,
            tenantContext.Object,
            currentUser.Object,
            planResolver.Object,
            catalogProvider.Object,
            registrationSector.Object,
            NullLogger<CompanyModulesController>.Instance);
    }
}
