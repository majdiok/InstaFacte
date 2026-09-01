using FactuTrust.Domain.Entities.SectorRules;
using FactuTrust.Domain.Enums;
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

    private static int ExpectedDataTemplateItemCount() =>
        SectorConfigurationCatalog.DataTemplates.Sum(t => t.Items.Count);

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
        // Phase 2 (plan §4.2/§4.3): the catalog now declares 4 module dependency edges and 3
        // additive data templates (one chart-account item each).
        Assert.Equal(SectorConfigurationCatalog.ModuleDependencies.Count, await db.SectorModuleDependencies.CountAsync());
        Assert.Equal(4, await db.SectorModuleDependencies.CountAsync());
        Assert.Equal(SectorConfigurationCatalog.DataTemplates.Count, await db.SectorDataTemplates.CountAsync());
        Assert.Equal(3, await db.SectorDataTemplates.CountAsync());
        Assert.Equal(ExpectedDataTemplateItemCount(), await db.SectorDataTemplateItems.CountAsync());
        Assert.Equal(1, result.NewVersion);
        Assert.False(result.Forced);
        Assert.Equal(0, result.Updated);
        Assert.Equal(0, result.SkippedExisting);
    }

    /// <summary>Seed_projette_les_dependances_du_catalogue (plan §4.6): the 4 approved edges land as active rows.</summary>
    [Fact]
    public async Task Seed_projette_les_dependances_du_catalogue()
    {
        await using var db = NewDb();

        await SectorRuleSeeder.SeedAsync(db, force: false, actor: "test", CancellationToken.None);

        var rows = await db.SectorModuleDependencies.ToListAsync();
        Assert.Equal(4, rows.Count);
        Assert.All(rows, r => Assert.True(r.IsActive));

        var pairs = rows.Select(r => (r.ModuleId, r.RequiredModuleId)).ToHashSet();
        foreach (var edge in SectorConfigurationCatalog.ModuleDependencies)
        {
            Assert.Contains(((int)edge.Module, (int)edge.RequiredModule), pairs);
        }
    }

    /// <summary>Seeding the data templates also projects their items (plan §4.3/§4.6).</summary>
    [Fact]
    public async Task Seed_projette_les_templates_de_donnees_du_catalogue()
    {
        await using var db = NewDb();

        await SectorRuleSeeder.SeedAsync(db, force: false, actor: "test", CancellationToken.None);

        var templates = await db.SectorDataTemplates.ToListAsync();
        Assert.Equal(3, templates.Count);
        Assert.All(templates, t => Assert.True(t.IsActive));

        foreach (var templateDef in SectorConfigurationCatalog.DataTemplates)
        {
            var template = templates.Single(t => t.Code == templateDef.Code);
            Assert.Equal(templateDef.DomainCode, template.DomainCode);
            Assert.Equal(templateDef.SegmentCode, template.SegmentCode);

            var items = await db.SectorDataTemplateItems.Where(i => i.TemplateId == template.Id).ToListAsync();
            Assert.Equal(templateDef.Items.Count, items.Count);
            Assert.All(items, i => Assert.True(i.IsActive));
        }
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
        var dependencyCountBefore = await db.SectorModuleDependencies.CountAsync();
        var templateCountBefore = await db.SectorDataTemplates.CountAsync();
        var templateItemCountBefore = await db.SectorDataTemplateItems.CountAsync();

        var second = await SectorRuleSeeder.SeedAsync(db, force: false, actor: "test", CancellationToken.None);

        Assert.Equal(0, second.Inserted);
        Assert.Equal(0, second.Updated);
        Assert.True(second.SkippedExisting > 0);
        Assert.Equal(segmentCountBefore, await db.SectorSegments.CountAsync());
        Assert.Equal(linkCountBefore, await db.SectorSegmentDomains.CountAsync());
        Assert.Equal(ruleCountBefore, await db.SectorModuleRules.CountAsync());
        Assert.Equal(settingCountBefore, await db.SectorDefaultSettings.CountAsync());
        Assert.Equal(dependencyCountBefore, await db.SectorModuleDependencies.CountAsync());
        Assert.Equal(templateCountBefore, await db.SectorDataTemplates.CountAsync());
        Assert.Equal(templateItemCountBefore, await db.SectorDataTemplateItems.CountAsync());
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

    /// <summary>
    /// Module dependency edges are a bounded, enumerable link-type row (like segment↔domain
    /// links): force=true deactivates any (ModuleId, RequiredModuleId) pair no longer catalog-declared,
    /// but never deletes it.
    /// </summary>
    [Fact]
    public async Task Force_desactive_une_dependance_de_module_retiree_du_catalogue_sans_supprimer()
    {
        await using var db = NewDb();
        await SectorRuleSeeder.SeedAsync(db, force: false, actor: "test", CancellationToken.None);

        // Admin-added edge not in the catalog list.
        var extraEdge = SectorModuleDependency.Create((int)AppModule.CRM, (int)AppModule.Clients);
        db.SectorModuleDependencies.Add(extraEdge);
        await db.SaveChangesAsync();

        await SectorRuleSeeder.SeedAsync(db, force: true, actor: "admin", CancellationToken.None);

        var all = await db.SectorModuleDependencies.ToListAsync();
        Assert.Equal(5, all.Count); // 4 catalog edges + 1 admin-added, none deleted.

        var extra = all.Single(d => d.ModuleId == (int)AppModule.CRM && d.RequiredModuleId == (int)AppModule.Clients);
        Assert.False(extra.IsActive);

        var catalogEdges = all.Where(d => d.ModuleId != (int)AppModule.CRM || d.RequiredModuleId != (int)AppModule.Clients).ToList();
        Assert.Equal(4, catalogEdges.Count);
        Assert.All(catalogEdges, d => Assert.True(d.IsActive));
    }

    /// <summary>
    /// A data template is an entity-type row identified by Code (like segments/domains): force=true
    /// resets its details back to the catalog's and reactivates it, but never touches a template item
    /// beyond insert-if-missing (no reset-on-force, no deletion of an admin-added item).
    /// </summary>
    [Fact]
    public async Task Seed_with_force_resets_template_details_but_keeps_admin_added_item()
    {
        await using var db = NewDb();
        await SectorRuleSeeder.SeedAsync(db, force: false, actor: "test", CancellationToken.None);

        var templateDef = SectorConfigurationCatalog.DataTemplates[0];
        var template = await db.SectorDataTemplates.SingleAsync(t => t.Code == templateDef.Code);
        template.UpdateDetails("Libellé personnalisé", template.DescriptionFr, template.Version, template.SortOrder);

        var extraItem = SectorDataTemplateItem.Create(template.Id, "chart-account", "{\"accountNumber\":\"7099\"}", 99);
        db.SectorDataTemplateItems.Add(extraItem);
        await db.SaveChangesAsync();

        await SectorRuleSeeder.SeedAsync(db, force: true, actor: "admin", CancellationToken.None);

        var resetTemplate = await db.SectorDataTemplates.SingleAsync(t => t.Code == templateDef.Code);
        Assert.Equal(templateDef.LabelFr, resetTemplate.LabelFr);

        var items = await db.SectorDataTemplateItems.Where(i => i.TemplateId == template.Id).ToListAsync();
        Assert.Equal(templateDef.Items.Count + 1, items.Count); // catalog item(s) + admin-added, none deleted.
        Assert.Contains(items, i => i.SortOrder == 99 && i.ItemKind == "chart-account");
    }

    [Fact]
    public async Task Force_rafraichit_le_payload_d_un_item_de_template_catalogue()
    {
        // Review R3: a template item is matched by (ItemKind, SortOrder), not by its PayloadJson —
        // previously force=true only reactivated an already-active item and never touched its
        // payload, so a catalog Version bump could silently republish stale account data under a
        // new version number. force=true must now overwrite the payload to the catalog's current
        // value.
        await using var db = NewDb();
        await SectorRuleSeeder.SeedAsync(db, force: false, actor: "test", CancellationToken.None);

        var templateDef = SectorConfigurationCatalog.DataTemplates[0];
        var itemDef = templateDef.Items[0];
        var template = await db.SectorDataTemplates.SingleAsync(t => t.Code == templateDef.Code);
        var item = await db.SectorDataTemplateItems.SingleAsync(
            i => i.TemplateId == template.Id && i.ItemKind == itemDef.ItemKind && i.SortOrder == itemDef.SortOrder);

        // Simulate a stale payload left over from a previous catalog revision.
        item.UpdatePayload("{\"stale\":\"payload-from-an-older-catalog-revision\"}");
        await db.SaveChangesAsync();

        await SectorRuleSeeder.SeedAsync(db, force: true, actor: "admin", CancellationToken.None);

        var refreshedItem = await db.SectorDataTemplateItems.SingleAsync(i => i.Id == item.Id);
        Assert.Equal(itemDef.PayloadJson, refreshedItem.PayloadJson);
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

    // ---------- ReconcileOnStartupAsync (review R2) ----------

    [Fact]
    public void ComputeCatalogHash_is_deterministic_and_matches_across_calls()
    {
        Assert.Equal(SectorRuleSeeder.ComputeCatalogHash(), SectorRuleSeeder.ComputeCatalogHash());
    }

    [Fact]
    public async Task ReconcileOnStartup_seeds_full_catalog_on_an_empty_db()
    {
        await using var db = NewDb();

        var result = await SectorRuleSeeder.ReconcileOnStartupAsync(db, CancellationToken.None);

        Assert.Equal(6, await db.SectorSegments.CountAsync());
        var stamp = await db.SectorRuleSetStamps.SingleAsync();
        Assert.Equal(SectorRuleSeeder.ComputeCatalogHash(), stamp.CatalogContentHash);
        Assert.Equal(stamp.Version, result.NewVersion);
    }

    [Fact]
    public async Task ReconcileOnStartup_is_a_pure_noop_when_the_catalog_hash_is_unchanged()
    {
        await using var db = NewDb();
        await SectorRuleSeeder.ReconcileOnStartupAsync(db, CancellationToken.None);
        var versionAfterFirstRun = await db.SectorRuleSetStamps.Select(s => s.Version).SingleAsync();

        // An admin-only label edit on a non-catalog surface must survive the reconcile no-op —
        // proof that the second call really did nothing (unlike a force=true seed, which would
        // reset it).
        var segment = await db.SectorSegments.FirstAsync(s => s.Code == CompanySegments.Commerce);
        segment.ResetFromCatalog("Libellé admin", segment.DescriptionFr, segment.IconKey, segment.SortOrder, segment.DefaultWarehouseName);
        await db.SaveChangesAsync();

        var result = await SectorRuleSeeder.ReconcileOnStartupAsync(db, CancellationToken.None);

        var versionAfterSecondRun = await db.SectorRuleSetStamps.Select(s => s.Version).SingleAsync();
        Assert.Equal(versionAfterFirstRun, versionAfterSecondRun);
        Assert.Equal(versionAfterSecondRun, result.NewVersion);
        var untouchedSegment = await db.SectorSegments.SingleAsync(s => s.Code == CompanySegments.Commerce);
        Assert.Equal("Libellé admin", untouchedSegment.LabelFr);
    }

    [Fact]
    public async Task ReconcileOnStartup_force_reseeds_when_the_recorded_hash_is_stale()
    {
        await using var db = NewDb();
        await SectorRuleSeeder.ReconcileOnStartupAsync(db, CancellationToken.None);

        // Simulate drift: an admin-edited catalog-known segment label, plus a stale recorded hash
        // (as if this row had been seeded by an older catalog revision).
        var segment = await db.SectorSegments.FirstAsync(s => s.Code == CompanySegments.Commerce);
        segment.ResetFromCatalog("Libellé obsolète", segment.DescriptionFr, segment.IconKey, segment.SortOrder, segment.DefaultWarehouseName);
        var stamp = await db.SectorRuleSetStamps.SingleAsync();
        stamp.SetCatalogHash("stale-hash-from-an-older-catalog-revision");
        await db.SaveChangesAsync();

        var result = await SectorRuleSeeder.ReconcileOnStartupAsync(db, CancellationToken.None);

        Assert.True(result.Forced);
        var resetSegment = await db.SectorSegments.SingleAsync(s => s.Code == CompanySegments.Commerce);
        Assert.Equal("Commerce", resetSegment.LabelFr);
        var refreshedStamp = await db.SectorRuleSetStamps.SingleAsync();
        Assert.Equal(SectorRuleSeeder.ComputeCatalogHash(), refreshedStamp.CatalogContentHash);
    }

    [Fact]
    public async Task ReconcileOnStartup_force_reseeds_when_no_hash_was_ever_recorded()
    {
        // Simulate a pre-R2 deployment: at least one segment already exists (from an old seed run)
        // and the stamp predates the CatalogContentHash column — CreateInitial() leaves it null,
        // exactly like a row created before this migration landed.
        await using var db = NewDb();
        var preExistingSegment = SectorSegment.Create(CompanySegments.Commerce, "Commerce", "desc", "icon", 0, null);
        db.SectorSegments.Add(preExistingSegment);
        db.SectorRuleSetStamps.Add(SectorRuleSetStamp.CreateInitial());
        await db.SaveChangesAsync();
        Assert.Null((await db.SectorRuleSetStamps.SingleAsync()).CatalogContentHash);

        var result = await SectorRuleSeeder.ReconcileOnStartupAsync(db, CancellationToken.None);

        Assert.True(result.Forced);
        var refreshedStamp = await db.SectorRuleSetStamps.SingleAsync();
        Assert.Equal(SectorRuleSeeder.ComputeCatalogHash(), refreshedStamp.CatalogContentHash);
        Assert.Equal(6, await db.SectorSegments.CountAsync());
    }
}
