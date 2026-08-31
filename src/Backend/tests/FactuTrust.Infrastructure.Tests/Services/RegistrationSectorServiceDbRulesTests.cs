using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Billing;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.SectorConfiguration;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Phase 2 — moteur de règles sectorielles en base (plan §WP-B4): domain↔segment link
/// validation and module-dependency transitive-closure auto-pull, exercised against a
/// hand-crafted <see cref="ISectorCatalogProvider"/> stub (no real DB needed).
/// </summary>
public sealed class RegistrationSectorServiceDbRulesTests
{
    private sealed class FixedSnapshotProvider : ISectorCatalogProvider
    {
        private readonly SectorRuleSnapshot _snapshot;
        public FixedSnapshotProvider(SectorRuleSnapshot snapshot) => _snapshot = snapshot;
        public SectorRuleSnapshot GetSnapshot() => _snapshot;
    }

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

    private static MasterDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static SegmentSnapshot MakeSegment(string code, IReadOnlyList<string> domainCodes) => new()
    {
        Code = code,
        LabelFr = code,
        DescriptionFr = code,
        IconKey = "icon",
        SortOrder = 0,
        DefaultWarehouseName = null,
        BaseRecommendedModules = Array.Empty<AppModule>(),
        DomainCodes = domainCodes
    };

    private static DomainSnapshot MakeDomain(string code) => new()
    {
        Code = code,
        LabelFr = code,
        SortOrder = 0,
        OverlayModules = Array.Empty<AppModule>()
    };

    private static SectorRuleSnapshot MakeSnapshot(
        SectorRuleSource source,
        IReadOnlyList<SegmentSnapshot> segments,
        IReadOnlyList<DomainSnapshot>? domains = null,
        IReadOnlyList<ModuleDependencySnapshot>? moduleDependencies = null) => new()
    {
        Source = source,
        Version = 1,
        Segments = segments,
        Domains = domains ?? new[] { MakeDomain(BusinessDomains.Autre) },
        ModuleDependencies = moduleDependencies ?? Array.Empty<ModuleDependencySnapshot>(),
        DefaultSettings = Array.Empty<DefaultSettingSnapshot>(),
        DataTemplates = Array.Empty<DataTemplateSnapshot>()
    };

    private static RegistrationSectorService NewService(
        MasterDbContext db,
        IPlanResolver planResolver,
        ISectorCatalogProvider catalogProvider,
        bool enabled = true) =>
        new(
            db,
            planResolver,
            catalogProvider,
            Options.Create(new RegistrationSectorOptions { Enabled = enabled }),
            NullLogger<RegistrationSectorService>.Instance);

    // ---------- ResolveProfile: domain↔segment link validation ----------

    [Fact]
    public void ResolveProfile_rejects_domain_not_linked_to_segment_when_db_rules_active()
    {
        var snapshot = MakeSnapshot(
            SectorRuleSource.Db,
            new[] { MakeSegment(CompanySegments.Commerce, new[] { BusinessDomains.Autre }) },
            domains: new[] { MakeDomain(BusinessDomains.Autre), MakeDomain(BusinessDomains.SanteParamedical) });

        var service = NewService(NewDb(), new AllowAllPlanResolver(), new FixedSnapshotProvider(snapshot));
        var result = service.ResolveProfile(CompanySegments.Commerce, BusinessDomains.SanteParamedical);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.BusinessDomain", result.Error.Code);
        Assert.Equal("Domaine d'activité non disponible pour ce type de société.", result.Error.Description);
    }

    [Fact]
    public void ResolveProfile_allows_any_known_domain_when_static_fallback_active()
    {
        // Same restrictive segment link list as above, but Source = Static — a segment link
        // restriction must never be enforced against the permanent Phase 1 static catalog.
        var snapshot = MakeSnapshot(
            SectorRuleSource.Static,
            new[] { MakeSegment(CompanySegments.Commerce, new[] { BusinessDomains.Autre }) },
            domains: new[] { MakeDomain(BusinessDomains.Autre), MakeDomain(BusinessDomains.SanteParamedical) });

        var service = NewService(NewDb(), new AllowAllPlanResolver(), new FixedSnapshotProvider(snapshot));
        var result = service.ResolveProfile(CompanySegments.Commerce, BusinessDomains.SanteParamedical);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ResolveProfile_allows_domain_when_segment_has_empty_explicit_list()
    {
        // An empty DomainCodes list (e.g. an admin accidentally pruned every link) must mean
        // "no restriction", not "no domain allowed" — protects against a self-inflicted outage.
        var snapshot = MakeSnapshot(
            SectorRuleSource.Db,
            new[] { MakeSegment(CompanySegments.Commerce, Array.Empty<string>()) },
            domains: new[] { MakeDomain(BusinessDomains.Autre), MakeDomain(BusinessDomains.SanteParamedical) });

        var service = NewService(NewDb(), new AllowAllPlanResolver(), new FixedSnapshotProvider(snapshot));
        var result = service.ResolveProfile(CompanySegments.Commerce, BusinessDomains.SanteParamedical);

        Assert.True(result.IsSuccess);
    }

    // ---------- ApplyModuleSelectionAsync: dependency transitive closure ----------

    [Fact]
    public async Task ApplyModuleSelection_auto_pulls_transitive_dependencies()
    {
        await using var db = NewDb();
        var snapshot = MakeSnapshot(
            SectorRuleSource.Db,
            new[] { MakeSegment(CompanySegments.Commerce, Array.Empty<string>()) },
            moduleDependencies: new[]
            {
                new ModuleDependencySnapshot { ModuleId = (int)AppModule.Stock, RequiredModuleId = (int)AppModule.Purchases }
            });

        var service = NewService(db, new AllowAllPlanResolver(), new FixedSnapshotProvider(snapshot));
        var userId = Guid.NewGuid();

        await service.ApplyModuleSelectionAsync(
            userId, null, new[] { (int)AppModule.Stock }, SubscriptionPlan.Free, CancellationToken.None);
        await db.SaveChangesAsync();

        var rows = await db.UserModuleGrants.Where(g => g.UserId == userId).ToDictionaryAsync(r => r.Module, r => r.IsEnabled);
        Assert.True(rows[AppModule.Stock]);
        Assert.True(rows[AppModule.Purchases], "Purchases should be auto-pulled as a dependency of Stock.");
    }

    [Fact]
    public async Task ApplyModuleSelection_dependency_still_subject_to_plan_ceiling()
    {
        await using var db = NewDb();
        var snapshot = MakeSnapshot(
            SectorRuleSource.Db,
            new[] { MakeSegment(CompanySegments.Commerce, Array.Empty<string>()) },
            moduleDependencies: new[]
            {
                new ModuleDependencySnapshot { ModuleId = (int)AppModule.Stock, RequiredModuleId = (int)AppModule.Purchases }
            });

        // Plan denies the auto-pulled dependency (Purchases) — it must end up excluded, not
        // silently escalated past the plan ceiling.
        var service = NewService(db, new DenyingPlanResolver(new[] { AppModule.Purchases }), new FixedSnapshotProvider(snapshot));
        var userId = Guid.NewGuid();

        await service.ApplyModuleSelectionAsync(
            userId, null, new[] { (int)AppModule.Stock }, SubscriptionPlan.Free, CancellationToken.None);
        await db.SaveChangesAsync();

        var rows = await db.UserModuleGrants.Where(g => g.UserId == userId).ToDictionaryAsync(r => r.Module, r => r.IsEnabled);
        Assert.True(rows[AppModule.Stock]);
        Assert.False(rows[AppModule.Purchases], "A plan-denied dependency must be excluded, not escalated.");
    }

    [Fact]
    public async Task ApplyModuleSelection_without_dependencies_matches_phase1_grants()
    {
        await using var db = NewDb();
        // No ModuleDependencies rows at all (Phase 1 static parity) — behavior must be identical
        // to the pre-WP-B4 code path.
        var snapshot = MakeSnapshot(
            SectorRuleSource.Db,
            new[] { MakeSegment(CompanySegments.Commerce, Array.Empty<string>()) });

        var service = NewService(db, new AllowAllPlanResolver(), new FixedSnapshotProvider(snapshot));
        var userId = Guid.NewGuid();

        await service.ApplyModuleSelectionAsync(
            userId, null, new[] { (int)AppModule.Stock, (int)AppModule.CRM }, SubscriptionPlan.Free, CancellationToken.None);
        await db.SaveChangesAsync();

        var rows = await db.UserModuleGrants.Where(g => g.UserId == userId).ToListAsync();
        Assert.Equal(AppModuleExtensions.AllValues.Length, rows.Count);

        var enabled = rows.Where(r => r.IsEnabled).Select(r => r.Module).ToHashSet();
        Assert.Contains(AppModule.Stock, enabled);
        Assert.Contains(AppModule.CRM, enabled);
        Assert.DoesNotContain(AppModule.Purchases, enabled);
    }
}
