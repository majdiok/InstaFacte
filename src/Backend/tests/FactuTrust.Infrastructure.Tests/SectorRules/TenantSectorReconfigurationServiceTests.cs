using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Billing;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.SectorConfiguration;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using FactuTrust.Infrastructure.Services.SectorCatalog;
using FactuTrust.Infrastructure.Services.SectorRules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.SectorRules;

/// <summary>
/// Phase 2 — backoffice tenant sector re-configuration (plan §WP-B7, D6): preview is side-effect
/// free, apply rewrites module grants via the shared <see cref="SectorModuleSetCalculator"/>, applies
/// templates additively, and fault-isolates every step (fail-continue). InMemory-backed so the
/// grant/audit/state assertions run without SQL Server.
/// </summary>
public sealed class TenantSectorReconfigurationServiceTests
{
    // ---------- fakes ----------

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

    /// <summary>Denies an explicit set of modules regardless of the plan (used for the no-escalation/ceiling proofs).</summary>
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

    /// <summary>Tenant service stub whose connection-string lookup always reports an available tenant DB.</summary>
    private sealed class FakeTenantService : ITenantService
    {
        public Task<string?> GetConnectionStringAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>("Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=fake-tenant;Integrated Security=True");

        public Task<string> CreateTenantDatabaseAsync(Guid tenantId, string databaseName, string? warehouseName = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<string> CreateAccountingFirmDatabaseAsync(Guid tenantId, string databaseName, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<bool> DatabaseExistsAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
        public Task ApplyMigrationsAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task TryDropDatabaseAsync(string databaseName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task EnsureTenantTemplateAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class InMemoryTenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly DbContextOptions<TenantDbContext> _options;
        public InMemoryTenantDbContextFactory(string databaseName)
        {
            _options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(databaseName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }
        public TenantDbContext CreateContext() => new(_options);
    }

    /// <summary>Template applier that always throws — used to exercise the fail-continue step isolation.</summary>
    private sealed class ThrowingSectorDataTemplateApplier : ISectorDataTemplateApplier
    {
        public Task<SectorTemplateApplyResult> ApplyAsync(Guid tenantId, string connectionString, string? segmentCode, string? domainCode, bool dryRun, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("templates-boom");
    }

    /// <summary>
    /// Review R1: <see cref="StaticSectorCatalogProvider"/> deliberately reports empty
    /// <c>DataTemplates</c> (rollback gating for <c>UseDbRules=false</c>) — the tenant-isolation
    /// test below needs a catalog that actually applies a real template (a new chart account) to
    /// prove per-tenant isolation, so it uses this full-catalog reference instead.
    /// </summary>
    private sealed class CatalogReferenceSectorCatalogProvider : ISectorCatalogProvider
    {
        public SectorRuleSnapshot GetSnapshot() => SectorConfigurationCatalog.BuildCatalogSnapshot();
    }

    /// <summary>Registration sector service stub that resolves a profile whose core covers the entire
    /// module universe — drives the canonical no-rows grant path (plan §WP-B7 "canonical form preserved").</summary>
    private sealed class FullCoreRegistrationSectorService : IRegistrationSectorService
    {
        public Result<SectorProfile?> ResolveProfile(string? companySegment, string? businessDomain) =>
            Result.Success<SectorProfile?>(new SectorProfile
            {
                SegmentCode = CompanySegments.Commerce,
                DomainCode = null,
                CoreModules = AppModuleExtensions.AllValues,
                RecommendedModules = Array.Empty<AppModule>(),
                OptionalModules = Array.Empty<AppModule>(),
                DefaultWarehouseName = null
            });

        public Task ApplyModuleSelectionAsync(Guid userId, SectorProfile? profile, IReadOnlyList<int>? requestedModules, SubscriptionPlan plan, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    /// <summary>
    /// Multi-tenant isolation fake: routes <see cref="ITenantDbContextFactory.CreateIsolatedContext(string)"/>
    /// to a distinct InMemory database per connection string — exactly the fan-out
    /// <see cref="TenantSectorReconfigurationService"/> and <see cref="SectorDataTemplateApplier"/> rely on
    /// to reach the CORRECT tenant DB (never the single shared context <see cref="InMemoryTenantDbContextFactory"/>
    /// uses for single-tenant tests).
    /// </summary>
    private sealed class RoutingTenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly Dictionary<string, DbContextOptions<TenantDbContext>> _optionsByConnectionString;

        public RoutingTenantDbContextFactory(IReadOnlyDictionary<string, string> databaseNameByConnectionString)
        {
            _optionsByConnectionString = databaseNameByConnectionString.ToDictionary(
                kvp => kvp.Key,
                kvp => new DbContextOptionsBuilder<TenantDbContext>()
                    .UseInMemoryDatabase(kvp.Value)
                    .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                    .Options);
        }

        public TenantDbContext CreateContext() =>
            throw new NotImplementedException("Not exercised by the reconfiguration/template-applier path — only CreateIsolatedContext(connectionString) is.");

        public TenantDbContext CreateIsolatedContext() =>
            throw new NotImplementedException("Not exercised by the reconfiguration/template-applier path — only CreateIsolatedContext(connectionString) is.");

        public TenantDbContext CreateIsolatedContext(string connectionString) =>
            new(_optionsByConnectionString[connectionString]);
    }

    /// <summary>Tenant service fake that maps each tenant to its own distinct connection string.</summary>
    private sealed class TwoTenantFakeTenantService : ITenantService
    {
        private readonly IReadOnlyDictionary<Guid, string> _connectionStringByTenantId;
        public TwoTenantFakeTenantService(IReadOnlyDictionary<Guid, string> connectionStringByTenantId) =>
            _connectionStringByTenantId = connectionStringByTenantId;

        public Task<string?> GetConnectionStringAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_connectionStringByTenantId.TryGetValue(tenantId, out var cs) ? cs : null);

        public Task<string> CreateTenantDatabaseAsync(Guid tenantId, string databaseName, string? warehouseName = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<string> CreateAccountingFirmDatabaseAsync(Guid tenantId, string databaseName, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<bool> DatabaseExistsAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
        public Task ApplyMigrationsAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task TryDropDatabaseAsync(string databaseName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task EnsureTenantTemplateAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    // ---------- helpers ----------

    private static MasterDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static TenantSectorReconfigurationService NewService(
        MasterDbContext db,
        IPlanResolver planResolver,
        InMemoryTenantDbContextFactory tenantFactory,
        ISectorDataTemplateApplier? applier = null,
        IRegistrationSectorService? registrationService = null)
    {
        var catalog = new StaticSectorCatalogProvider();
        registrationService ??= new RegistrationSectorService(
            db, planResolver, catalog,
            Options.Create(new RegistrationSectorOptions { Enabled = true }),
            NullLogger<RegistrationSectorService>.Instance);
        applier ??= new SectorDataTemplateApplier(catalog, tenantFactory, NullLogger<SectorDataTemplateApplier>.Instance);
        return new TenantSectorReconfigurationService(
            db, registrationService, catalog, applier, planResolver,
            new FakeTenantService(), tenantFactory,
            NullLogger<TenantSectorReconfigurationService>.Instance);
    }

    private static async Task<Tenant> SeedTenantAsync(MasterDbContext db)
    {
        var n = Random.Shared.Next(1000000, 9999999);
        var office = Random.Shared.Next(0, 999).ToString("000");
        var tenant = Tenant.Create(
            $"Reconfig-{n}",
            NIF.Create($"{n}/A/B/C/{office}").Value,
            Address.Create("1 rue Test", "Tunis", "Tunis").Value,
            Email.Create($"reconfig-{n}@example.com").Value,
            PhoneNumber.Create("20123456").Value,
            TaxRegime.RealRegime).Value;
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant;
    }

    private static async Task<ApplicationUser> SeedUserAsync(MasterDbContext db, Guid tenantId, bool isActive = true)
    {
        var n = Guid.NewGuid().ToString("N")[..8];
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"reconfig-user-{n}@example.com",
            NormalizedUserName = $"RECONFIG-USER-{n}@EXAMPLE.COM",
            Email = $"reconfig-user-{n}@example.com",
            NormalizedEmail = $"RECONFIG-USER-{n}@EXAMPLE.COM",
            EmailConfirmed = true,
            FirstName = "Reconfig",
            LastName = "User",
            TenantId = tenantId,
            IsActive = isActive
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task SeedRestrictedGrantsAsync(MasterDbContext db, Guid userId, IReadOnlySet<AppModule> enabled)
    {
        foreach (var module in AppModuleExtensions.AllValues)
        {
            db.UserModuleGrants.Add(new UserModuleGrant
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Module = module,
                IsEnabled = enabled.Contains(module),
                EnabledFeatureKeys = null
            });
        }
        await db.SaveChangesAsync();
    }

    // ---------- Preview ----------

    [Fact]
    public async Task Preview_computes_module_diff_without_writing()
    {
        await using var db = NewDb();
        var tenantFactory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var tenant = await SeedTenantAsync(db);
        var user = await SeedUserAsync(db, tenant.Id); // no grants ⇒ legacy all-modules canonical form

        var service = NewService(db, new AllowAllPlanResolver(), tenantFactory);

        // Templates intentionally OFF so preview never touches a tenant DB; only the module diff is
        // exercised. RecomputeModuleGrants ON so a per-user diff is produced.
        var request = new SectorReconfigurationRequestDto
        {
            CompanySegment = CompanySegments.Commerce,
            RecomputeModuleGrants = true,
            ApplyDataTemplates = false
        };

        var result = await service.PreviewAsync(tenant.Id, request, actorAdminId: Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var diff = Assert.Single(result.Value.Users);
        Assert.Equal(user.Id, diff.UserId);

        // Current = all modules (no grants); target never includes Honoraires (firm-native) ⇒ Honoraires
        // is always among the modules to disable, and nothing can be enabled beyond the current full set.
        Assert.Contains((int)AppModule.Honoraires, diff.ModulesToDisable);
        Assert.Empty(diff.ModulesToEnable);

        // No side effects: tenant classification unchanged, no grant rows written.
        Assert.Null((await db.Tenants.AsNoTracking().FirstAsync(t => t.Id == tenant.Id)).CompanySegment);
        Assert.Equal(0, await db.UserModuleGrants.AsNoTracking().CountAsync(g => g.UserId == user.Id));
    }

    [Fact]
    public async Task Preview_unknown_segment_returns_French_validation_error()
    {
        await using var db = NewDb();
        var tenantFactory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var tenant = await SeedTenantAsync(db);

        var service = NewService(db, new AllowAllPlanResolver(), tenantFactory);
        var request = new SectorReconfigurationRequestDto { CompanySegment = "not-a-real-segment" };

        var result = await service.PreviewAsync(tenant.Id, request, actorAdminId: null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.CompanySegment", result.Error.Code);
    }

    // ---------- Apply ----------

    [Fact]
    public async Task Apply_updates_tenant_classification_and_rewrites_grants()
    {
        await using var db = NewDb();
        var tenantFactory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var tenant = await SeedTenantAsync(db);
        var user = await SeedUserAsync(db, tenant.Id);

        var service = NewService(db, new AllowAllPlanResolver(), tenantFactory);
        var request = new SectorReconfigurationRequestDto
        {
            CompanySegment = CompanySegments.Commerce,
            RecomputeModuleGrants = true,
            ApplyDataTemplates = false
        };

        var result = await service.ApplyAsync(tenant.Id, request, actorAdminId: Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Value.Steps, s => s.Step == "classification" && s.Success);
        Assert.Contains(result.Value.Steps, s => s.Step == $"module-grants:{user.Id}" && s.Success);

        // Classification persisted.
        Assert.Equal(CompanySegments.Commerce, (await db.Tenants.AsNoTracking().FirstAsync(t => t.Id == tenant.Id)).CompanySegment);

        // Grants rewritten to the canonical full-row restriction form: one row per module,
        // Administration forced on, Honoraires off.
        var rows = await db.UserModuleGrants.AsNoTracking().Where(g => g.UserId == user.Id).ToDictionaryAsync(r => r.Module, r => r.IsEnabled);
        Assert.Equal(AppModuleExtensions.AllValues.Length, rows.Count);
        Assert.True(rows[AppModule.Administration]);
        Assert.False(rows[AppModule.Honoraires]);

        // Audit row written to the tenant DB.
        using var tenantCtx = tenantFactory.CreateContext();
        Assert.Equal(1, await tenantCtx.AuditLogs.CountAsync(a => a.Action == TenantSectorReconfigurationService.AuditAction));
    }

    [Fact]
    public async Task Apply_full_module_set_results_in_zero_grant_rows()
    {
        await using var db = NewDb();
        var tenantFactory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var tenant = await SeedTenantAsync(db);
        var user = await SeedUserAsync(db, tenant.Id);
        // User starts from a restricted grant set (only Administration on) — the canonical no-rows
        // form must replace it, not coexist with it.
        await SeedRestrictedGrantsAsync(db, user.Id, new HashSet<AppModule> { AppModule.Administration });

        // A profile whose core covers the whole module universe ⇒ the calculator returns the full set
        // ⇒ RewriteUserGrants deletes the prior rows and writes nothing (legacy "all enabled" form).
        var service = NewService(db, new AllowAllPlanResolver(), tenantFactory, registrationService: new FullCoreRegistrationSectorService());
        var request = new SectorReconfigurationRequestDto
        {
            CompanySegment = CompanySegments.Commerce,
            RecomputeModuleGrants = true,
            ApplyDataTemplates = false
        };

        var result = await service.ApplyAsync(tenant.Id, request, actorAdminId: Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, await db.UserModuleGrants.AsNoTracking().CountAsync(g => g.UserId == user.Id));
    }

    [Fact]
    public async Task Apply_never_disables_administration()
    {
        await using var db = NewDb();
        var tenantFactory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var tenant = await SeedTenantAsync(db);
        var user = await SeedUserAsync(db, tenant.Id);

        // A plan that (mis)configures Administration as denied must not be able to switch it off —
        // mirrors the registration self-lockout carve-out (review §item 2).
        var service = NewService(db, new DenyingPlanResolver(new[] { AppModule.Administration }), tenantFactory);
        var request = new SectorReconfigurationRequestDto
        {
            CompanySegment = CompanySegments.Commerce,
            RecomputeModuleGrants = true,
            ApplyDataTemplates = false
        };

        var result = await service.ApplyAsync(tenant.Id, request, actorAdminId: Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var rows = await db.UserModuleGrants.AsNoTracking().Where(g => g.UserId == user.Id).ToDictionaryAsync(r => r.Module, r => r.IsEnabled);
        Assert.True(rows[AppModule.Administration], "Administration must survive a plan denial.");
    }

    [Fact]
    public async Task Apply_respects_plan_ceiling()
    {
        await using var db = NewDb();
        var tenantFactory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var tenant = await SeedTenantAsync(db);
        var user = await SeedUserAsync(db, tenant.Id);

        // Plan denies every non-core, non-Honoraires module: only the core modules survive the
        // ceiling — recommended modules are dropped even though the profile proposes them.
        var denied = AppModuleExtensions.AllValues
            .Where(m => !SectorConfigurationCatalog.CoreModules.Contains(m))
            .ToArray();
        var service = NewService(db, new DenyingPlanResolver(denied), tenantFactory);
        var request = new SectorReconfigurationRequestDto
        {
            CompanySegment = CompanySegments.Commerce,
            RecomputeModuleGrants = true,
            ApplyDataTemplates = false
        };

        var result = await service.ApplyAsync(tenant.Id, request, actorAdminId: Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var enabled = (await db.UserModuleGrants.AsNoTracking()
            .Where(g => g.UserId == user.Id && g.IsEnabled)
            .Select(g => g.Module)
            .ToListAsync()).ToHashSet();

        Assert.Equal(SectorConfigurationCatalog.CoreModules.ToHashSet(), enabled);
    }

    [Fact]
    public async Task Apply_continues_after_single_step_failure_and_reports_it()
    {
        await using var db = NewDb();
        var tenantFactory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var tenant = await SeedTenantAsync(db);
        var user = await SeedUserAsync(db, tenant.Id);

        // The templates step is forced to fail; classification + per-user grants + audit must still run.
        var service = NewService(db, new AllowAllPlanResolver(), tenantFactory, applier: new ThrowingSectorDataTemplateApplier());
        var request = new SectorReconfigurationRequestDto
        {
            CompanySegment = CompanySegments.Commerce,
            RecomputeModuleGrants = true,
            ApplyDataTemplates = true
        };

        var result = await service.ApplyAsync(tenant.Id, request, actorAdminId: Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess); // fail-continue ⇒ the run still reports a result
        Assert.Contains(result.Value.Steps, s => s.Step == "templates" && !s.Success);
        Assert.Contains(result.Value.Steps, s => s.Step == "classification" && s.Success);
        Assert.Contains(result.Value.Steps, s => s.Step == $"module-grants:{user.Id}" && s.Success);
        Assert.Contains(result.Value.Steps, s => s.Step == "audit" && s.Success);

        // Classification + grants were still applied despite the templates failure.
        Assert.Equal(CompanySegments.Commerce, (await db.Tenants.AsNoTracking().FirstAsync(t => t.Id == tenant.Id)).CompanySegment);
        Assert.Equal(AppModuleExtensions.AllValues.Length, await db.UserModuleGrants.AsNoTracking().CountAsync(g => g.UserId == user.Id));
    }

    [Fact]
    public async Task Apply_with_userIds_subset_only_touches_those_users()
    {
        await using var db = NewDb();
        var tenantFactory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var tenant = await SeedTenantAsync(db);
        var user1 = await SeedUserAsync(db, tenant.Id);
        var user2 = await SeedUserAsync(db, tenant.Id);
        var user3 = await SeedUserAsync(db, tenant.Id);
        // user3 starts restricted (Administration only) and is NOT in the requested subset — it must
        // be left exactly as-is.
        await SeedRestrictedGrantsAsync(db, user3.Id, new HashSet<AppModule> { AppModule.Administration });

        var service = NewService(db, new AllowAllPlanResolver(), tenantFactory);
        var request = new SectorReconfigurationRequestDto
        {
            CompanySegment = CompanySegments.Commerce,
            RecomputeModuleGrants = true,
            ApplyDataTemplates = false,
            UserIds = new[] { user1.Id, user2.Id }
        };

        var result = await service.ApplyAsync(tenant.Id, request, actorAdminId: Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);

        // Touched users were rewritten to the commerce profile (core incl. Clients is enabled).
        var user1Enabled = await db.UserModuleGrants.AsNoTracking()
            .Where(g => g.UserId == user1.Id && g.IsEnabled).Select(g => g.Module).ToListAsync();
        Assert.Contains(AppModule.Clients, user1Enabled);

        // Untouched user keeps its pre-seeded restricted state (only Administration).
        var user3Enabled = await db.UserModuleGrants.AsNoTracking()
            .Where(g => g.UserId == user3.Id && g.IsEnabled).Select(g => g.Module).ToListAsync();
        Assert.Equal(new[] { AppModule.Administration }, user3Enabled);
    }

    // ---------- SectorModuleSetCalculator parity ----------

    [Fact]
    public async Task SectorModuleSetCalculator_matches_registration_service_output()
    {
        // Property-style parity proof (plan §WP-B7): feeding the calculator the same core/seed/plan
        // the registration service consumes yields exactly the enabled set the registration service
        // persists — i.e. the extraction did not change observable behavior.
        await using var calcDb = NewDb();
        await using var regDb = NewDb();
        var planResolver = new AllowAllPlanResolver();
        var catalog = new StaticSectorCatalogProvider();
        var requested = new[] { (int)AppModule.Stock, (int)AppModule.CRM };
        var userId = Guid.NewGuid();

        var finalSet = await SectorModuleSetCalculator.ComputeEnabledSetAsync(
            SectorConfigurationCatalog.CoreModules,
            requested,
            SubscriptionPlan.Free,
            catalog.GetSnapshot().ModuleDependencies,
            planResolver,
            userId,
            NullLogger.Instance,
            CancellationToken.None);

        var registration = new RegistrationSectorService(
            regDb, planResolver, catalog,
            Options.Create(new RegistrationSectorOptions { Enabled = true }),
            NullLogger<RegistrationSectorService>.Instance);
        await registration.ApplyModuleSelectionAsync(userId, profile: null, requested, SubscriptionPlan.Free, CancellationToken.None);
        await regDb.SaveChangesAsync();

        var enabledFromGrants = await regDb.UserModuleGrants.AsNoTracking()
            .Where(g => g.UserId == userId && g.IsEnabled)
            .Select(g => g.Module)
            .ToListAsync();

        Assert.Equal(finalSet, enabledFromGrants.ToHashSet());

        // Re-run with a plan that denies one of the requested modules — both must still agree
        // (the calculator's plan-ceiling intersection and the registration's persisted grants match).
        await using var regDb2 = NewDb();
        var denying = new DenyingPlanResolver(new[] { AppModule.Stock });
        var userId2 = Guid.NewGuid();

        var finalSet2 = await SectorModuleSetCalculator.ComputeEnabledSetAsync(
            SectorConfigurationCatalog.CoreModules,
            requested,
            SubscriptionPlan.Free,
            catalog.GetSnapshot().ModuleDependencies,
            denying,
            userId2,
            NullLogger.Instance,
            CancellationToken.None);

        var registration2 = new RegistrationSectorService(
            regDb2, denying, catalog,
            Options.Create(new RegistrationSectorOptions { Enabled = true }),
            NullLogger<RegistrationSectorService>.Instance);
        await registration2.ApplyModuleSelectionAsync(userId2, profile: null, requested, SubscriptionPlan.Free, CancellationToken.None);
        await regDb2.SaveChangesAsync();

        var enabledFromGrants2 = await regDb2.UserModuleGrants.AsNoTracking()
            .Where(g => g.UserId == userId2 && g.IsEnabled)
            .Select(g => g.Module)
            .ToListAsync();

        Assert.Equal(finalSet2, enabledFromGrants2.ToHashSet());
        Assert.DoesNotContain(AppModule.Stock, finalSet2); // denied module excluded by both
    }

    // ---------- Multi-tenant isolation ----------

    /// <summary>
    /// Cross-tenant isolation proof (no existing coverage): reconfiguring tenant A must never leak
    /// into tenant B — neither the master-DB rows (classification, module grants) nor the isolated
    /// tenant-DB rows (chart-of-accounts additions from the template, applied-template markers,
    /// audit log). Uses <see cref="RoutingTenantDbContextFactory"/>/<see cref="TwoTenantFakeTenantService"/>
    /// so each tenant genuinely resolves to its OWN backing database, unlike the single shared
    /// <see cref="InMemoryTenantDbContextFactory"/> used by every other test in this file.
    /// </summary>
    [Fact]
    public async Task Apply_for_tenant_A_never_writes_to_tenant_B()
    {
        await using var db = NewDb();
        var tenantA = await SeedTenantAsync(db);
        var tenantB = await SeedTenantAsync(db);
        var userA = await SeedUserAsync(db, tenantA.Id);
        var userB = await SeedUserAsync(db, tenantB.Id);

        const string connStringA = "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=fake-tenant-a;Integrated Security=True";
        const string connStringB = "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=fake-tenant-b;Integrated Security=True";

        var routingFactory = new RoutingTenantDbContextFactory(new Dictionary<string, string>
        {
            [connStringA] = $"tenant-a-{Guid.NewGuid()}",
            [connStringB] = $"tenant-b-{Guid.NewGuid()}",
        });
        var tenantService = new TwoTenantFakeTenantService(new Dictionary<Guid, string>
        {
            [tenantA.Id] = connStringA,
            [tenantB.Id] = connStringB,
        });

        var planResolver = new AllowAllPlanResolver();
        var catalog = new CatalogReferenceSectorCatalogProvider();
        var registrationService = new RegistrationSectorService(
            db, planResolver, catalog,
            Options.Create(new RegistrationSectorOptions { Enabled = true }),
            NullLogger<RegistrationSectorService>.Instance);
        var applier = new SectorDataTemplateApplier(catalog, routingFactory, NullLogger<SectorDataTemplateApplier>.Instance);
        var service = new TenantSectorReconfigurationService(
            db, registrationService, catalog, applier, planResolver,
            tenantService, routingFactory, NullLogger<TenantSectorReconfigurationService>.Instance);

        var request = new SectorReconfigurationRequestDto
        {
            CompanySegment = CompanySegments.Commerce,
            BusinessDomain = BusinessDomains.AlimentationAgroalimentaire,
            RecomputeModuleGrants = true,
            ApplyDataTemplates = true
        };

        var result = await service.ApplyAsync(tenantA.Id, request, actorAdminId: Guid.NewGuid(), CancellationToken.None);
        Assert.True(result.IsSuccess);

        // Tenant A: template applied (new chart account), classification + grants updated, audit written.
        using var tenantACtx = routingFactory.CreateIsolatedContext(connStringA);
        Assert.True(await tenantACtx.ChartOfAccounts.AnyAsync(a => a.AccountNumber == "7071"));
        Assert.Equal(1, await tenantACtx.AuditLogs.CountAsync(a => a.Action == TenantSectorReconfigurationService.AuditAction));

        var tenantARow = await db.Tenants.AsNoTracking().FirstAsync(t => t.Id == tenantA.Id);
        Assert.Equal(CompanySegments.Commerce, tenantARow.CompanySegment);
        Assert.Equal(AppModuleExtensions.AllValues.Length, await db.UserModuleGrants.AsNoTracking().CountAsync(g => g.UserId == userA.Id));

        // Tenant B: master-DB classification and grants left completely untouched.
        var tenantBRow = await db.Tenants.AsNoTracking().FirstAsync(t => t.Id == tenantB.Id);
        Assert.Null(tenantBRow.CompanySegment);
        Assert.Null(tenantBRow.BusinessDomain);
        Assert.Equal(0, await db.UserModuleGrants.AsNoTracking().CountAsync(g => g.UserId == userB.Id));

        // Tenant B: isolated tenant DB never touched — zero rows in every sector-reconfiguration
        // surface (chart of accounts, applied-template markers, audit log).
        using var tenantBCtx = routingFactory.CreateIsolatedContext(connStringB);
        Assert.Equal(0, await tenantBCtx.ChartOfAccounts.CountAsync());
        Assert.Equal(0, await tenantBCtx.AppliedSectorTemplates.CountAsync());
        Assert.Equal(0, await tenantBCtx.AuditLogs.CountAsync());
    }
}
