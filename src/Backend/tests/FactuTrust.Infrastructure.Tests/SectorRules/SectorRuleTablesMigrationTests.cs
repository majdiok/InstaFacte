using FactuTrust.Domain.Entities.SectorRules;
using FactuTrust.Domain.SectorConfiguration;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Persistence.Seeds;
using Microsoft.Data.SqlClient;
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

    /// <summary>
    /// Non-régression du tri topologique des INSERT du seeder sectoriel, contre un VRAI SQL Server.
    /// <para>
    /// Les tables sector-rules portent cinq FK réelles (<c>AddSectorRuleTables_Master</c>). EF
    /// ordonne les INSERT via un tri topologique alimenté par les FK DÉCLARÉES DANS LE MODÈLE ;
    /// tant que <c>MasterDbContext</c> n'en déclarait aucune, EF émettait un lot par type d'entité
    /// dans l'ordre ordinal des noms de types, plaçant <c>SectorDataTemplateItems</c> avant
    /// <c>SectorDataTemplates</c> et <c>SectorModuleRules</c> avant <c>SectorSegments</c> : le tout
    /// premier seed (base vide) explosait en <c>SqlException</c> 547.
    /// </para>
    /// <para>
    /// Tout le reste de la suite du seeder tourne sur <c>UseInMemoryDatabase</c>, qui n'applique
    /// AUCUNE contrainte de clé étrangère — d'où l'angle mort. Ce test est le seul capable
    /// d'attraper la régression, il exige donc SQL Server (même portail d'activation que le test
    /// ci-dessus) et s'exécute sur une base jetable dédiée pour ne jamais toucher aux données d'un
    /// poste de développement.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Seed_from_empty_database_orders_parent_inserts_before_children()
    {
        if (Environment.GetEnvironmentVariable("RUN_SECTOR_RULE_MIGRATION_SQL_TESTS") != "1")
            return;

        var baseConnectionString = Environment.GetEnvironmentVariable("FACTUTRUST_TEST_MASTER_CONNECTION")
            ?? "Server=tcp:localhost,1433;Database=FactuTrust_Master;User Id=sa;Password=IsoTest_Str0ng_Pw1!Xy;TrustServerCertificate=True;Encrypt=True";

        var connectionString = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = $"FT_Test_SectorSeedOrdering_{Guid.NewGuid():N}"
        }.ConnectionString;

        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using var db = new MasterDbContext(options);
        try
        {
            await db.Database.MigrateAsync();

            // Sanity : le scénario reproduit est bien « tout est vide », le seul cas où parents ET
            // enfants sont insérés dans un même SaveChangesAsync.
            Assert.Equal(0, await db.SectorSegments.CountAsync());
            Assert.Equal(0, await db.SectorDataTemplates.CountAsync());

            // Avant le correctif : Microsoft.Data.SqlClient.SqlException 547 sur
            // FK_SectorModuleRules_SectorSegments / FK_SectorDataTemplateItems_SectorDataTemplates.
            var result = await SectorRuleSeeder.SeedAsync(db, force: false, actor: "test", CancellationToken.None);

            Assert.Equal(6, await db.SectorSegments.CountAsync());
            Assert.Equal(10, await db.SectorDomains.CountAsync());
            Assert.Equal(
                SectorConfigurationCatalog.Segments.Sum(s => s.AllowedDomainCodes.Count),
                await db.SectorSegmentDomains.CountAsync());
            Assert.Equal(
                SectorConfigurationCatalog.Segments.Sum(s => s.BaseRecommendedModules.Count)
                    + SectorConfigurationCatalog.Domains.Sum(d => d.OverlayModules.Count),
                await db.SectorModuleRules.CountAsync());
            Assert.Equal(
                SectorConfigurationCatalog.DataTemplates.Count,
                await db.SectorDataTemplates.CountAsync());
            Assert.Equal(
                SectorConfigurationCatalog.DataTemplates.Sum(t => t.Items.Count),
                await db.SectorDataTemplateItems.CountAsync());
            Assert.Equal(1, result.NewVersion);

            // Idempotence contre SQL réel : un second passage ne réinsère rien et ne viole rien.
            var second = await SectorRuleSeeder.SeedAsync(db, force: false, actor: "test", CancellationToken.None);
            Assert.Equal(0, second.Inserted);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
