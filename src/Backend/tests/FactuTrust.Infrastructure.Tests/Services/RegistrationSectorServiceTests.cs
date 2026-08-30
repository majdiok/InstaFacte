using FactuTrust.Domain.Billing;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.SectorConfiguration;
using FactuTrust.Application.Configuration;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Sector-aware registration wizard — <see cref="RegistrationSectorService"/> tests
/// (plan §3 C4/C5, §6.1 B4, §8). <see cref="ApplyModuleSelectionAsync"/> is security-critical
/// (restriction-only): several tests here are no-escalation regression proofs.
/// </summary>
public sealed class RegistrationSectorServiceTests
{
    private static MasterDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static RegistrationSectorService NewService(
        MasterDbContext db,
        FactuTrust.Application.Common.Interfaces.IPlanResolver planResolver,
        bool enabled = true)
    {
        return new RegistrationSectorService(
            db,
            planResolver,
            Options.Create(new RegistrationSectorOptions { Enabled = enabled }),
            NullLogger<RegistrationSectorService>.Instance);
    }

    /// <summary>Plan resolver stub that allows every module (mirrors DbPlanResolver's permissive fallback).</summary>
    private sealed class AllowAllPlanResolver : FactuTrust.Application.Common.Interfaces.IPlanResolver
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

    /// <summary>Plan resolver stub that denies a specific set of modules — used for the no-escalation proof.</summary>
    private sealed class DenyingPlanResolver : FactuTrust.Application.Common.Interfaces.IPlanResolver
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

    // ---------- ResolveProfile ----------

    [Fact]
    public void ResolveProfile_returns_null_when_both_codes_absent()
    {
        var service = NewService(NewDb(), new AllowAllPlanResolver());
        var result = service.ResolveProfile(null, null);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public void ResolveProfile_returns_null_when_flag_disabled_despite_valid_codes()
    {
        var service = NewService(NewDb(), new AllowAllPlanResolver(), enabled: false);
        var result = service.ResolveProfile(CompanySegments.Commerce, BusinessDomains.Autre);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public void ResolveProfile_fails_with_French_message_for_unknown_segment()
    {
        var service = NewService(NewDb(), new AllowAllPlanResolver());
        var result = service.ResolveProfile("not-a-real-segment", null);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.CompanySegment", result.Error.Code);
    }

    [Fact]
    public void ResolveProfile_fails_with_French_message_for_unknown_domain()
    {
        var service = NewService(NewDb(), new AllowAllPlanResolver());
        var result = service.ResolveProfile(CompanySegments.Commerce, "not-a-real-domain");

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.BusinessDomain", result.Error.Code);
    }

    [Fact]
    public void ResolveProfile_fails_when_domain_provided_without_segment()
    {
        var service = NewService(NewDb(), new AllowAllPlanResolver());
        var result = service.ResolveProfile(null, BusinessDomains.Autre);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.CompanySegment", result.Error.Code);
    }

    [Fact]
    public void ResolveProfile_succeeds_for_known_segment_and_domain()
    {
        var service = NewService(NewDb(), new AllowAllPlanResolver());
        var result = service.ResolveProfile(" COMMERCE ", " Autre ");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(CompanySegments.Commerce, result.Value!.SegmentCode);
    }

    // ---------- ApplyModuleSelectionAsync ----------

    [Fact]
    public async Task ApplyModuleSelection_null_request_writes_no_grant_rows()
    {
        await using var db = NewDb();
        var service = NewService(db, new AllowAllPlanResolver());
        var userId = Guid.NewGuid();

        await service.ApplyModuleSelectionAsync(userId, null, null, SubscriptionPlan.Free, CancellationToken.None);

        Assert.Equal(0, await db.UserModuleGrants.CountAsync());
    }

    [Fact]
    public async Task ApplyModuleSelection_empty_request_writes_no_grant_rows()
    {
        await using var db = NewDb();
        var service = NewService(db, new AllowAllPlanResolver());
        var userId = Guid.NewGuid();

        await service.ApplyModuleSelectionAsync(
            userId, null, Array.Empty<int>(), SubscriptionPlan.Free, CancellationToken.None);

        Assert.Equal(0, await db.UserModuleGrants.CountAsync());
    }

    [Fact]
    public async Task ApplyModuleSelection_full_module_list_writes_no_grant_rows_canonical_legacy_representation()
    {
        await using var db = NewDb();
        var service = NewService(db, new AllowAllPlanResolver());
        var userId = Guid.NewGuid();

        var allModuleIds = AppModuleExtensions.AllValues.Select(m => (int)m).ToList();
        await service.ApplyModuleSelectionAsync(
            userId, null, allModuleIds, SubscriptionPlan.Free, CancellationToken.None);

        Assert.Equal(0, await db.UserModuleGrants.CountAsync());
    }

    [Fact]
    public async Task ApplyModuleSelection_partial_selection_writes_18_rows_with_expected_flags()
    {
        await using var db = NewDb();
        var service = NewService(db, new AllowAllPlanResolver());
        var userId = Guid.NewGuid();

        var requested = new[] { (int)AppModule.Stock, (int)AppModule.CRM };
        await service.ApplyModuleSelectionAsync(
            userId, null, requested, SubscriptionPlan.Free, CancellationToken.None);
        await db.SaveChangesAsync();

        var rows = await db.UserModuleGrants.Where(g => g.UserId == userId).ToListAsync();
        Assert.Equal(AppModuleExtensions.AllValues.Length, rows.Count);

        var enabled = rows.Where(r => r.IsEnabled).Select(r => r.Module).ToHashSet();
        Assert.Contains(AppModule.Stock, enabled);
        Assert.Contains(AppModule.CRM, enabled);
        Assert.DoesNotContain(AppModule.Payroll, enabled);
        Assert.DoesNotContain(AppModule.Accounting, enabled);
    }

    [Fact]
    public async Task ApplyModuleSelection_forces_core_modules_on_even_if_omitted()
    {
        await using var db = NewDb();
        var service = NewService(db, new AllowAllPlanResolver());
        var userId = Guid.NewGuid();

        // Request only Stock — core modules (Administration, Clients, Products, Sales, Treasury,
        // Reports) must still end up enabled even though none were requested.
        await service.ApplyModuleSelectionAsync(
            userId, null, new[] { (int)AppModule.Stock }, SubscriptionPlan.Free, CancellationToken.None);
        await db.SaveChangesAsync();

        var rows = await db.UserModuleGrants.Where(g => g.UserId == userId).ToDictionaryAsync(r => r.Module, r => r.IsEnabled);

        foreach (var coreModule in SectorConfigurationCatalog.CoreModules)
            Assert.True(rows[coreModule], $"{coreModule} should be forced on.");
    }

    [Fact]
    public async Task ApplyModuleSelection_filters_unknown_ids_and_drops_Honoraires()
    {
        await using var db = NewDb();
        var service = NewService(db, new AllowAllPlanResolver());
        var userId = Guid.NewGuid();

        var requested = new[] { 9999, -1, (int)AppModule.Honoraires, (int)AppModule.Stock };
        await service.ApplyModuleSelectionAsync(
            userId, null, requested, SubscriptionPlan.Free, CancellationToken.None);
        await db.SaveChangesAsync();

        var rows = await db.UserModuleGrants.Where(g => g.UserId == userId).ToListAsync();
        Assert.Equal(AppModuleExtensions.AllValues.Length, rows.Count);
        Assert.False(rows.Single(r => r.Module == AppModule.Honoraires).IsEnabled);
        Assert.True(rows.Single(r => r.Module == AppModule.Stock).IsEnabled);
    }

    [Fact]
    public async Task ApplyModuleSelection_disables_module_not_allowed_by_plan_no_escalation_proof()
    {
        await using var db = NewDb();
        // Plan denies Stock outright: even though the client asks for it, it must end up disabled.
        var service = NewService(db, new DenyingPlanResolver(new[] { AppModule.Stock }));
        var userId = Guid.NewGuid();

        await service.ApplyModuleSelectionAsync(
            userId, null, new[] { (int)AppModule.Stock, (int)AppModule.CRM }, SubscriptionPlan.Free, CancellationToken.None);
        await db.SaveChangesAsync();

        var rows = await db.UserModuleGrants.Where(g => g.UserId == userId).ToDictionaryAsync(r => r.Module, r => r.IsEnabled);
        Assert.False(rows[AppModule.Stock]);
        Assert.True(rows[AppModule.CRM]);
    }

    [Fact]
    public async Task ApplyModuleSelection_forces_core_on_even_when_plan_would_deny_it()
    {
        await using var db = NewDb();
        // A plan that (incorrectly) tries to deny a core module must not be able to switch it off —
        // core modules bypass the plan check entirely (mirrors the Administration self-lockout rule).
        var service = NewService(db, new DenyingPlanResolver(new[] { AppModule.Administration }));
        var userId = Guid.NewGuid();

        await service.ApplyModuleSelectionAsync(
            userId, null, new[] { (int)AppModule.Stock }, SubscriptionPlan.Free, CancellationToken.None);
        await db.SaveChangesAsync();

        var rows = await db.UserModuleGrants.Where(g => g.UserId == userId).ToDictionaryAsync(r => r.Module, r => r.IsEnabled);
        Assert.True(rows[AppModule.Administration]);
    }

    [Fact]
    public async Task ApplyModuleSelection_does_not_call_SaveChanges_rows_ride_callers_transaction()
    {
        await using var db = NewDb();
        var service = NewService(db, new AllowAllPlanResolver());
        var userId = Guid.NewGuid();

        await service.ApplyModuleSelectionAsync(
            userId, null, new[] { (int)AppModule.Stock }, SubscriptionPlan.Free, CancellationToken.None);

        // The rows are tracked/added but nothing has been persisted since no SaveChangesAsync
        // was called by the service itself.
        Assert.Equal(0, await db.UserModuleGrants.AsNoTracking().CountAsync());
        Assert.True(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task ApplyModuleSelection_written_rows_resolve_through_EffectivePermissionService_to_the_same_selection()
    {
        await using var db = NewDb();
        var service = NewService(db, new AllowAllPlanResolver());
        var userId = Guid.NewGuid();

        var requested = new[] { (int)AppModule.Stock, (int)AppModule.CRM };
        await service.ApplyModuleSelectionAsync(
            userId, null, requested, SubscriptionPlan.Free, CancellationToken.None);
        await db.SaveChangesAsync();

        var grants = await db.UserModuleGrants.Where(g => g.UserId == userId).ToListAsync();

        // Simulate an actor whose role grants every permission tied to every module (e.g.
        // Administrator) so the effective-permission filter is a no-op and the comparison isolates
        // the grant-toggle logic itself.
        var fullPermissionUniverse = AppModuleExtensions.GetAllModulesPermissionUniverse().ToHashSet();

        var resolved = EffectivePermissionService.ResolveEnabledModules(grants, fullPermissionUniverse);

        var expected = SectorConfigurationCatalog.CoreModules
            .Concat(new[] { AppModule.Stock, AppModule.CRM })
            .ToHashSet();

        Assert.Equal(expected, resolved.ToHashSet());
    }
}
