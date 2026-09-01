using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services.Background;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services.Background;

/// <summary>
/// Plan §1.5 mini-saga cleanup — <see cref="OrphanTenantDatabaseCleanupJob"/> tests.
///
/// Two coverage gaps, both documented rather than silently skipped (neither is fixable without a
/// real SQL Server instance, unavailable in this sandbox):
/// <list type="bullet">
///   <item><c>CleanupOrphanPhysicalDatabasesAsync</c>'s happy path lists physical databases via a
///   raw <see cref="Microsoft.Data.SqlClient.SqlConnection"/> query against <c>sys.databases</c> —
///   only its config-guard branch (missing <c>MasterConnection</c> ⇒ pass skipped, no exception) is
///   covered here.</item>
///   <item><c>CleanupStaleUnprovisionedTenantsAsync</c>'s actual DELETE branch calls
///   <c>ExecuteDeleteAsync</c> (bulk delete) on <c>UserModuleGrants</c>/<c>Subscriptions</c>, which
///   the EF Core InMemory provider does not support ("could not be translated" at query-compile
///   time, regardless of whether any row matches — confirmed by running it). Switching to a SQLite
///   in-memory database was tried and rejected: <c>MasterDbContext</c> pins several columns to
///   explicit SQL-Server-only <c>HasColumnType</c> strings (e.g. <c>nvarchar(max)</c>,
///   <c>uniqueidentifier</c>), which <c>EnsureCreated()</c> then emits verbatim and SQLite rejects
///   ("near 'max': syntax error"). Making that path testable would require either a real SQL Server
///   (unavailable here) or restructuring <c>MasterDbContext</c>'s column mappings — out of scope for
///   this fix. The self-heal branch (tenant already has a connection string) and the "leave alone"
///   branches (Ready tenants, tenants still within the 24h grace period) never reach
///   <c>ExecuteDeleteAsync</c> and ARE fully covered below.</item>
/// </list>
/// </summary>
public sealed class OrphanTenantDatabaseCleanupJobTests
{
    private sealed class TestContext : IAsyncDisposable
    {
        public required MasterDbContext Db { get; init; }
        public required UserManager<ApplicationUser> UserManager { get; init; }
        public required Mock<ITenantService> TenantService { get; init; }
        public required OrphanTenantDatabaseCleanupJob Job { get; init; }

        public async ValueTask DisposeAsync() => await Db.DisposeAsync();
    }

    private static Task<TestContext> CreateContextAsync(IConfiguration? configuration = null)
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MasterDbContext(options);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(options);
        services.AddScoped(_ => db);
        services.AddIdentityCore<ApplicationUser>(o =>
            {
                o.Password.RequireDigit = false;
                o.Password.RequireLowercase = false;
                o.Password.RequireUppercase = false;
                o.Password.RequireNonAlphanumeric = false;
                o.Password.RequiredLength = 6;
            })
            .AddEntityFrameworkStores<MasterDbContext>();

        var sp = services.BuildServiceProvider();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();

        var tenantService = new Mock<ITenantService>();
        tenantService.Setup(t => t.TryDropDatabaseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // No "ConnectionStrings:MasterConnection" key ⇒ CleanupOrphanPhysicalDatabasesAsync's guard
        // clause returns early (logged warning, no exception) — see class doc comment above.
        var config = configuration ?? new ConfigurationBuilder().Build();

        var job = new OrphanTenantDatabaseCleanupJob(
            db, tenantService.Object, userManager, config,
            NullLogger<OrphanTenantDatabaseCleanupJob>.Instance);

        return Task.FromResult(new TestContext
        {
            Db = db,
            UserManager = userManager,
            TenantService = tenantService,
            Job = job
        });
    }

    private static Tenant NewPendingTenant(DateTime createdAt)
    {
        var nif = NIF.Create($"{Random.Shared.Next(1000000, 9999999)}/A/B/C/000").Value;
        var address = Address.Create("12 rue Test", "Tunis", "Tunis", postalCode: "1000", country: "Tunisie").Value;
        var email = Email.Create($"{Guid.NewGuid():N}@test.tn").Value;
        var phone = PhoneNumber.Create("20123456").Value;
        var tenant = Tenant.Create("Société Test", nif, address, email, phone, TaxRegime.RealRegime).Value;
        // Tenant.Create() defaults to Pending/CreatedAt=now — backdate CreatedAt in place (protected
        // setter, same EF pattern as PlanSeeder's private-setter backfills) to simulate a stale row.
        typeof(FactuTrust.Domain.Common.Entity).GetProperty(nameof(Tenant.CreatedAt))!.SetValue(tenant, createdAt);
        return tenant;
    }

    [Fact]
    public async Task ExecuteAsync_missing_MasterConnection_skips_physical_db_pass_without_throwing()
    {
        await using var ctx = await CreateContextAsync();

        // Nothing in the DB at all — this only proves the guard clause doesn't throw when the
        // config key is absent (the realistic sandbox condition); see class doc comment.
        await ctx.Job.ExecuteAsync(CancellationToken.None);

        ctx.TenantService.Verify(
            t => t.TryDropDatabaseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_self_heals_stale_tenant_that_already_has_a_connection_string_instead_of_destroying_it()
    {
        await using var ctx = await CreateContextAsync();
        var tenant = NewPendingTenant(DateTime.UtcNow.AddHours(-48)); // stale AND still Pending...
        ctx.Db.Tenants.Add(tenant);
        // ...but provisioning actually succeeded: a TenantConnectionString row exists, only the
        // final status flip failed. Must be repaired, not destroyed — this branch `continue`s
        // before ever reaching the ExecuteDeleteAsync-based deletion path.
        ctx.Db.TenantConnectionStrings.Add(new TenantConnectionString
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            EncryptedConnectionString = "encrypted-cs",
            CreatedAt = DateTime.UtcNow
        });
        await ctx.Db.SaveChangesAsync();

        await ctx.Job.ExecuteAsync(CancellationToken.None);

        var reloaded = await ctx.Db.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenant.Id);
        Assert.Equal(TenantProvisioningStatus.Ready, reloaded.ProvisioningStatus);
        ctx.TenantService.Verify(
            t => t.TryDropDatabaseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_leaves_a_Ready_tenant_completely_untouched_regardless_of_age()
    {
        await using var ctx = await CreateContextAsync();
        var nif = NIF.Create("1234567/A/B/C/000").Value;
        var address = Address.Create("12 rue Test", "Tunis", "Tunis", postalCode: "1000", country: "Tunisie").Value;
        var email = Email.Create("legit@test.tn").Value;
        var phone = PhoneNumber.Create("20123456").Value;
        var tenant = Tenant.Create("Société Légitime", nif, address, email, phone, TaxRegime.RealRegime).Value;
        // Firm-managed / synchronously-provisioned tenants default to Ready; simulate that here too
        // (mirrors CreateFirmManaged's post-Create() reset) and backdate CreatedAt well past 24h —
        // the stale query's WHERE clause excludes Ready tenants outright, so this never even reaches
        // the ExecuteDeleteAsync-based deletion path.
        tenant.MarkProvisioningReady();
        typeof(FactuTrust.Domain.Common.Entity).GetProperty(nameof(Tenant.CreatedAt))!.SetValue(tenant, DateTime.UtcNow.AddDays(-30));
        ctx.Db.Tenants.Add(tenant);
        await ctx.Db.SaveChangesAsync();

        await ctx.Job.ExecuteAsync(CancellationToken.None);

        var reloaded = await ctx.Db.Tenants.AsNoTracking().SingleOrDefaultAsync(t => t.Id == tenant.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(TenantProvisioningStatus.Ready, reloaded!.ProvisioningStatus);
        ctx.TenantService.Verify(
            t => t.TryDropDatabaseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_leaves_a_recent_pending_tenant_untouched_within_the_24h_grace_period()
    {
        await using var ctx = await CreateContextAsync();
        // Pending, but well within the 24h grace period — excluded by the stale query's WHERE
        // clause, so this also never reaches the ExecuteDeleteAsync-based deletion path.
        var tenant = NewPendingTenant(DateTime.UtcNow.AddHours(-1));
        ctx.Db.Tenants.Add(tenant);
        await ctx.Db.SaveChangesAsync();

        await ctx.Job.ExecuteAsync(CancellationToken.None);

        var reloaded = await ctx.Db.Tenants.AsNoTracking().SingleOrDefaultAsync(t => t.Id == tenant.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(TenantProvisioningStatus.Pending, reloaded!.ProvisioningStatus);
        ctx.TenantService.Verify(
            t => t.TryDropDatabaseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Proves exactly WHY the actual "stale tenant with no connection string gets deleted" scenario
    /// (drop physical DB, delete Identity user, delete UserModuleGrants/Subscriptions/Tenant row)
    /// cannot be exercised end-to-end in this sandbox: <see cref="OrphanTenantDatabaseCleanupJob"/>'s
    /// deletion branch reaches an <c>ExecuteDeleteAsync</c> call that the EF Core InMemory provider
    /// refuses to translate, regardless of whether any row actually matches — this is an environment
    /// limitation of the test provider, not a product bug. Documents the exact exception so the
    /// gap is explicit rather than silently absent from the suite.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_deleting_a_stale_tenant_without_a_connection_string_hits_an_untranslatable_ExecuteDelete_on_the_InMemory_provider()
    {
        await using var ctx = await CreateContextAsync();
        var tenant = NewPendingTenant(DateTime.UtcNow.AddHours(-30)); // >24h old, still Pending, no connection string.
        ctx.Db.Tenants.Add(tenant);
        await ctx.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ctx.Job.ExecuteAsync(CancellationToken.None));
        Assert.Contains("ExecuteDelete", ex.Message);
        Assert.Contains("could not be translated", ex.Message);
    }
}
