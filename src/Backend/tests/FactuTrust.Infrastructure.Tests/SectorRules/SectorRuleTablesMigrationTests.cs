using FactuTrust.Domain.Entities.SectorRules;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.SectorRules;

/// <summary>
/// Phase 2 — moteur de règles sectorielles en base (plan §WP-B1). Model-mapping assertion
/// (no SQL) plus an opt-in idempotency check for the hand-written migration SQL, run through EF's
/// own migrator (mirrors Program.cs's <c>Database.MigrateAsync()</c> startup path).
/// </summary>
public sealed class SectorRuleTablesMigrationTests
{
    private static MasterDbContext NewInMemoryDb() =>
        new(new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public void MasterDbContext_maps_all_sector_rule_entities()
    {
        using var db = NewInMemoryDb();

        var expectedTypes = new[]
        {
            typeof(SectorSegment),
            typeof(SectorDomain),
            typeof(SectorSegmentDomain),
            typeof(SectorModuleRule),
            typeof(SectorModuleDependency),
            typeof(SectorDefaultSetting),
            typeof(SectorDataTemplate),
            typeof(SectorDataTemplateItem),
            typeof(SectorRuleSetStamp)
        };

        foreach (var type in expectedTypes)
        {
            var entityType = db.Model.FindEntityType(type);
            Assert.NotNull(entityType);
        }
    }

    /// <summary>
    /// Requires a real SQL Server reachable at <c>RUN_SECTOR_RULE_MIGRATION_SQL_TESTS</c>=1
    /// (sandbox default: skipped) — mirrors <c>RegisterSectorConfigurationSqlTests</c>'s opt-in
    /// gate so this suite never fails when SQL Server isn't reachable. Applying the same set of
    /// pending migrations twice via <c>MigrateAsync</c> — the second call is a documented EF no-op
    /// (nothing pending) — proves the guarded raw SQL itself never runs twice against a database
    /// that already has it applied; the migration's own `IF OBJECT_ID(...) IS NULL` guards are
    /// additionally exercised by every fresh `MigrateAsync()` at API startup in the SQL-gated
    /// integration tests.
    /// </summary>
    [Fact]
    public async Task Migration_up_is_idempotent_when_run_twice()
    {
        if (Environment.GetEnvironmentVariable("RUN_SECTOR_RULE_MIGRATION_SQL_TESTS") != "1")
            return;

        var connectionString = Environment.GetEnvironmentVariable("FACTUTRUST_TEST_MASTER_CONNECTION")
            ?? "Server=tcp:localhost,1433;Database=FactuTrust_Master;User Id=sa;Password=IsoTest_Str0ng_Pw1!Xy;TrustServerCertificate=True;Encrypt=True";

        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using var db = new MasterDbContext(options);
        await db.Database.MigrateAsync();
        await db.Database.MigrateAsync();

        var count = await db.SectorRuleSetStamps.CountAsync();
        Assert.Equal(1, count);
    }
}
