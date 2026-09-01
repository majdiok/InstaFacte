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

    /// <summary>Total segment↔domain matrix pairs (plan §3.1) — sum of each segment's AllowedDomainCodes count.</summary>
    private static int ExpectedSegmentDomainLinkCount() =>
        SectorConfigurationCatalog.Segments.Sum(s => s.AllowedDomainCodes.Count);

    [Fact]
    public async Task Seed_on_empty_db_inserts_full_catalog()
    {
        await using var db = NewDb();

        var result = await SectorRuleSeeder.SeedAsync(db, force: false, actor: "test", CancellationToken.None);

        Assert.Equal(6, await db.SectorSegments.CountAsync());
        Assert.Equal(10, await db.SectorDomains.CountAsync());
        Assert.Equal(ExpectedSegmentDomainLinkCount(), await db.SectorSegmentDomains.CountAsync());
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

    /// <summary>Seed_ne_cree_que_les_liens_de_la_matrice (plan §3.5): only matrix pairs are inserted, never every combination.</summary>
    [Fact]
    public async Task Seed_ne_cree_que_les_liens_de_la_matrice()
    {
        await using var db = NewDb();
        await SectorRuleSeeder.SeedAsync(db, force: false, actor: "test", CancellationToken.None);

        var segmentsByCode = await db.SectorSegments.ToDictionaryAsync(s => s.Code);
        var domainsByCode = await db.SectorDomains.ToDictionaryAsync(d => d.Code);
        var links = await db.SectorSegmentDomains.Where(l => l.IsActive).ToListAsync();
        var linkedPairs = links
            .Select(l => (
                Segment: segmentsByCode.Values.Single(s => s.Id == l.SegmentId).Code,
                Domain: domainsByCode.Values.Single(d => d.Id == l.DomainId).Code))
            .ToHashSet();

        foreach (var segmentDef in SectorConfigurationCatalog.Segments)
        {
            foreach (var domainDef in SectorConfigurationCatalog.Domains)
            {
                var isMatrixPair = segmentDef.AllowedDomainCodes.Contains(domainDef.Code, StringComparer.Ordinal);
                Assert.Equal(isMatrixPair, linkedPairs.Contains((segmentDef.Code, domainDef.Code)));
            }
        }

        Assert.Equal(ExpectedSegmentDomainLinkCount(), links.Count);
    }

    /// <summary>Force_desactive_les_liens_retires_du_catalogue_sans_supprimer (plan §3.5).</summary>
    [Fact]
    public async Task Force_desactive_les_liens_retires_du_catalogue_sans_supprimer()
    {
        await using var db = NewDb();
        await SectorRuleSeeder.SeedAsync(db, force: false, actor: "test", CancellationToken.None);

        // Simulate a legacy deployment that still has the old "every domain for every segment"
        // 60-link seed: manually add every pair the matrix does NOT already contain.
        var segmentsByCode = await db.SectorSegments.ToDictionaryAsync(s => s.Code);
        var domainsByCode = await db.SectorDomains.ToDictionaryAsync(d => d.Code);
        var extraLinksAdded = 0;
        foreach (var segmentDef in SectorConfigurationCatalog.Segments)
        {
            foreach (var domainDef in SectorConfigurationCatalog.Domains)
            {
                if (segmentDef.AllowedDomainCodes.Contains(domainDef.Code, StringComparer.Ordinal))
                    continue;

                var extra = SectorSegmentDomain.Create(segmentsByCode[segmentDef.Code].Id, domainsByCode[domainDef.Code].Id, domainDef.SortOrder);
                db.SectorSegmentDomains.Add(extra);
                extraLinksAdded++;
            }
        }
        await db.SaveChangesAsync();
        Assert.Equal(60, ExpectedSegmentDomainLinkCount() + extraLinksAdded); // sanity: full 6×10 grid.

        await SectorRuleSeeder.SeedAsync(db, force: true, actor: "admin", CancellationToken.None);

        var allLinks = await db.SectorSegmentDomains.ToListAsync();
        Assert.Equal(60, allLinks.Count); // never deleted.

        var activeLinks = allLinks.Where(l => l.IsActive).ToList();
        Assert.Equal(ExpectedSegmentDomainLinkCount(), activeLinks.Count);

        var activePairs = activeLinks
            .Select(l => (
                Segment: segmentsByCode.Values.Single(s => s.Id == l.SegmentId).Code,
                Domain: domainsByCode.Values.Single(d => d.Id == l.DomainId).Code))
            .ToHashSet();

        foreach (var segmentDef in SectorConfigurationCatalog.Segments)
        {
            foreach (var domainDef in SectorConfigurationCatalog.Domains)
            {
                var isMatrixPair = segmentDef.AllowedDomainCodes.Contains(domainDef.Code, StringComparer.Ordinal);
                Assert.Equal(isMatrixPair, activePairs.Contains((segmentDef.Code, domainDef.Code)));
            }
        }
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
