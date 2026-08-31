using FactuTrust.Domain.Entities.SectorRules;
using FactuTrust.Domain.SectorConfiguration;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Persistence.Seeds;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.SectorRules;

/// <summary>Phase 2 — moteur de règles sectorielles en base (plan §WP-B3). Catalog→DB seeder.</summary>
public sealed class SectorRuleSeederTests
{
    private static MasterDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static int ExpectedModuleRuleCount() =>
        SectorConfigurationCatalog.Segments.Sum(s => s.BaseRecommendedModules.Count)
        + SectorConfigurationCatalog.Domains.Sum(d => d.OverlayModules.Count);

    [Fact]
    public async Task Seed_on_empty_db_inserts_full_catalog()
    {
        await using var db = NewDb();

        var result = await SectorRuleSeeder.SeedAsync(db, force: false, actor: "test", CancellationToken.None);

        Assert.Equal(6, await db.SectorSegments.CountAsync());
        Assert.Equal(10, await db.SectorDomains.CountAsync());
        Assert.Equal(60, await db.SectorSegmentDomains.CountAsync());
        Assert.Equal(ExpectedModuleRuleCount(), await db.SectorModuleRules.CountAsync());
        // 5 of the 6 segments have a DefaultWarehouseName + 1 global plan-comptable-variant row.
        Assert.Equal(7, await db.SectorDefaultSettings.CountAsync());
        Assert.Equal(0, await db.SectorModuleDependencies.CountAsync());
        Assert.Equal(0, await db.SectorDataTemplates.CountAsync());
        Assert.Equal(1, result.NewVersion);
        Assert.False(result.Forced);
        Assert.Equal(0, result.Updated);
        Assert.Equal(0, result.SkippedExisting);
    }

    [Fact]
    public async Task Seed_twice_without_force_is_idempotent_zero_duplicates()
    {
        await using var db = NewDb();
        await SectorRuleSeeder.SeedAsync(db, force: false, actor: "test", CancellationToken.None);

        var segmentCountBefore = await db.SectorSegments.CountAsync();
        var linkCountBefore = await db.SectorSegmentDomains.CountAsync();
        var ruleCountBefore = await db.SectorModuleRules.CountAsync();
        var settingCountBefore = await db.SectorDefaultSettings.CountAsync();

        var second = await SectorRuleSeeder.SeedAsync(db, force: false, actor: "test", CancellationToken.None);

        Assert.Equal(0, second.Inserted);
        Assert.Equal(0, second.Updated);
        Assert.True(second.SkippedExisting > 0);
        Assert.Equal(segmentCountBefore, await db.SectorSegments.CountAsync());
        Assert.Equal(linkCountBefore, await db.SectorSegmentDomains.CountAsync());
        Assert.Equal(ruleCountBefore, await db.SectorModuleRules.CountAsync());
        Assert.Equal(settingCountBefore, await db.SectorDefaultSettings.CountAsync());
        Assert.Equal(2, second.NewVersion);
    }

    [Fact]
    public async Task Seed_without_force_preserves_admin_edited_label()
    {
        await using var db = NewDb();
        await SectorRuleSeeder.SeedAsync(db, force: false, actor: "test", CancellationToken.None);

        var segment = await db.SectorSegments.SingleAsync(s => s.Code == CompanySegments.Commerce);
        segment.UpdateDetails("Commerce (personnalisé)", segment.DescriptionFr, segment.IconKey, segment.SortOrder, segment.DefaultWarehouseName);
        await db.SaveChangesAsync();

        await SectorRuleSeeder.SeedAsync(db, force: false, actor: "test", CancellationToken.None);

        var reread = await db.SectorSegments.SingleAsync(s => s.Code == CompanySegments.Commerce);
        Assert.Equal("Commerce (personnalisé)", reread.LabelFr);
    }

    [Fact]
    public async Task Seed_with_force_resets_catalog_rows_but_keeps_admin_added_rows()
    {
        await using var db = NewDb();
        await SectorRuleSeeder.SeedAsync(db, force: false, actor: "test", CancellationToken.None);

        var segment = await db.SectorSegments.SingleAsync(s => s.Code == CompanySegments.Commerce);
        segment.UpdateDetails("Commerce (personnalisé)", segment.DescriptionFr, segment.IconKey, segment.SortOrder, segment.DefaultWarehouseName);

        // Admin-created domain not present in the catalog.
        var extraDomain = SectorDomain.Create("domaine-maison", "Domaine maison", 99);
        db.SectorDomains.Add(extraDomain);
        await db.SaveChangesAsync();

        var result = await SectorRuleSeeder.SeedAsync(db, force: true, actor: "admin", CancellationToken.None);

        var resetSegment = await db.SectorSegments.SingleAsync(s => s.Code == CompanySegments.Commerce);
        Assert.Equal("Commerce", resetSegment.LabelFr);
        Assert.True(result.Updated > 0);

        var stillThere = await db.SectorDomains.SingleOrDefaultAsync(d => d.Code == "domaine-maison");
        Assert.NotNull(stillThere);
        Assert.Equal(11, await db.SectorDomains.CountAsync()); // 10 catalog domains + 1 admin-added, none deleted.
    }

    [Fact]
    public async Task SeedIfEmpty_noops_when_segments_exist()
    {
        await using var db = NewDb();
        await SectorRuleSeeder.SeedAsync(db, force: false, actor: "test", CancellationToken.None);
        var versionAfterFirstSeed = await db.SectorRuleSetStamps.Select(s => s.Version).SingleAsync();

        await SectorRuleSeeder.SeedIfEmptyAsync(db, CancellationToken.None);

        var versionAfterNoop = await db.SectorRuleSetStamps.Select(s => s.Version).SingleAsync();
        Assert.Equal(versionAfterFirstSeed, versionAfterNoop);
        Assert.Equal(6, await db.SectorSegments.CountAsync());
    }
}
