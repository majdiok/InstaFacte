using FactuTrust.Domain.Entities.SectorRules;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.SectorConfiguration;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Persistence.Seeds;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.SectorRules;

/// <summary>Phase 2 — moteur de règles sectorielles en base (plan §WP-B3). Catalog→DB seeder.</summary>
public sealed class SectorRuleSeederTests
{
    private static MasterDbContext NewDb(string? databaseName = null) =>
        new(new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
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
        // Phase 2 (plan §4.2/§4.3): the catalog declares 4 module dependency edges and additive data
        // templates. Phase 3 §3.4 enriched the catalog with 5 more templates (product families, extra
        // warehouses, BTP chart-account, numbering prefixes) on top of the 3 original chart-account
        // templates — 8 total. Lot 2.2 ajoute 2 modèles de familles de produits (santé & paramédical,
        // artisanat), qui couvraient jusqu'ici zéro modèle — 10 total.
        Assert.Equal(SectorConfigurationCatalog.ModuleDependencies.Count, await db.SectorModuleDependencies.CountAsync());
        Assert.Equal(4, await db.SectorModuleDependencies.CountAsync());
        Assert.Equal(SectorConfigurationCatalog.DataTemplates.Count, await db.SectorDataTemplates.CountAsync());
        Assert.Equal(10, await db.SectorDataTemplates.CountAsync());
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
        Assert.Equal(SectorConfigurationCatalog.DataTemplates.Count, templates.Count);
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

        // Admin-created domain not present in the catalog (flagged admin-managed, as the
        // backoffice CRUD does).
        var extraDomain = SectorDomain.Create("domaine-maison", "Domaine maison", 99);
        extraDomain.MarkAdminManaged();
        db.SectorDomains.Add(extraDomain);
        await db.SaveChangesAsync();

        var result = await SectorRuleSeeder.SeedAsync(db, force: true, actor: "admin", CancellationToken.None);

        var resetSegment = await db.SectorSegments.SingleAsync(s => s.Code == CompanySegments.Commerce);
        Assert.Equal("Commerce", resetSegment.LabelFr);
        Assert.True(result.Updated > 0);

        var stillThere = await db.SectorDomains.SingleOrDefaultAsync(d => d.Code == "domaine-maison");
        Assert.NotNull(stillThere);
        Assert.True(stillThere.IsActive); // admin-added domain survives force unchanged
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
        extraItem.MarkAdminManaged();
        db.SectorDataTemplateItems.Add(extraItem);
        await db.SaveChangesAsync();

        await SectorRuleSeeder.SeedAsync(db, force: true, actor: "admin", CancellationToken.None);

        var resetTemplate = await db.SectorDataTemplates.SingleAsync(t => t.Code == templateDef.Code);
        Assert.Equal(templateDef.LabelFr, resetTemplate.LabelFr);

        var items = await db.SectorDataTemplateItems.Where(i => i.TemplateId == template.Id).ToListAsync();
        Assert.Equal(templateDef.Items.Count + 1, items.Count); // catalog item(s) + admin-added, none deleted.
        Assert.Contains(items, i => i.SortOrder == 99 && i.ItemKind == "chart-account" && i.IsActive);
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

        var result = await SectorRuleSeeder.ReconcileOnStartupAsync(db, cancellationToken: CancellationToken.None);

        Assert.Equal(6, await db.SectorSegments.CountAsync());
        var stamp = await db.SectorRuleSetStamps.SingleAsync();
        Assert.Equal(SectorRuleSeeder.ComputeCatalogHash(), stamp.CatalogContentHash);
        Assert.Equal(stamp.Version, result.NewVersion);
    }

    [Fact]
    public async Task ReconcileOnStartup_is_a_pure_noop_when_the_catalog_hash_is_unchanged()
    {
        await using var db = NewDb();
        await SectorRuleSeeder.ReconcileOnStartupAsync(db, cancellationToken: CancellationToken.None);
        var versionAfterFirstRun = await db.SectorRuleSetStamps.Select(s => s.Version).SingleAsync();

        // An admin-only label edit on a non-catalog surface must survive the reconcile no-op —
        // proof that the second call really did nothing (unlike a force=true seed, which would
        // reset it).
        var segment = await db.SectorSegments.FirstAsync(s => s.Code == CompanySegments.Commerce);
        segment.ResetFromCatalog("Libellé admin", segment.DescriptionFr, segment.IconKey, segment.SortOrder, segment.DefaultWarehouseName);
        await db.SaveChangesAsync();

        var result = await SectorRuleSeeder.ReconcileOnStartupAsync(db, cancellationToken: CancellationToken.None);

        var versionAfterSecondRun = await db.SectorRuleSetStamps.Select(s => s.Version).SingleAsync();
        Assert.Equal(versionAfterFirstRun, versionAfterSecondRun);
        Assert.Equal(versionAfterSecondRun, result.NewVersion);
        var untouchedSegment = await db.SectorSegments.SingleAsync(s => s.Code == CompanySegments.Commerce);
        Assert.Equal("Libellé admin", untouchedSegment.LabelFr);
    }

    [Fact]
    public async Task ReconcileOnStartup_preserves_admin_managed_row_when_the_recorded_hash_is_stale()
    {
        await using var db = NewDb();
        await SectorRuleSeeder.ReconcileOnStartupAsync(db, cancellationToken: CancellationToken.None);

        // Simulate drift after an older catalog revision: one catalog-known segment edited by an
        // admin (flagged admin-managed, as the backoffice CRUD does) and one edited directly
        // (still catalog-owned), plus a stale recorded hash.
        var adminSegment = await db.SectorSegments.SingleAsync(s => s.Code == CompanySegments.Commerce);
        adminSegment.ResetFromCatalog("Libellé obsolète", adminSegment.DescriptionFr, adminSegment.IconKey, adminSegment.SortOrder, adminSegment.DefaultWarehouseName);
        adminSegment.MarkAdminManaged();

        var btpDef = SectorConfigurationCatalog.Segments.Single(s => s.Code == CompanySegments.BtpConstruction);
        var catalogSegment = await db.SectorSegments.SingleAsync(s => s.Code == CompanySegments.BtpConstruction);
        catalogSegment.ResetFromCatalog("BTP drifté", catalogSegment.DescriptionFr, catalogSegment.IconKey, catalogSegment.SortOrder, catalogSegment.DefaultWarehouseName);

        var stamp = await db.SectorRuleSetStamps.SingleAsync();
        stamp.SetCatalogHash("stale-hash-from-an-older-catalog-revision");
        await db.SaveChangesAsync();

        var result = await SectorRuleSeeder.ReconcileOnStartupAsync(db, cancellationToken: CancellationToken.None);

        Assert.False(result.Forced); // reconcile is non-destructive, not a force reset
        var untouchedAdminSegment = await db.SectorSegments.SingleAsync(s => s.Code == CompanySegments.Commerce);
        Assert.Equal("Libellé obsolète", untouchedAdminSegment.LabelFr); // admin-managed survives

        var resetCatalogSegment = await db.SectorSegments.SingleAsync(s => s.Code == CompanySegments.BtpConstruction);
        Assert.Equal(btpDef.LabelFr, resetCatalogSegment.LabelFr); // catalog-owned overwritten

        var refreshedStamp = await db.SectorRuleSetStamps.SingleAsync();
        Assert.Equal(SectorRuleSeeder.ComputeCatalogHash(), refreshedStamp.CatalogContentHash);
    }

    [Fact]
    public async Task ReconcileOnStartup_reseeds_when_no_hash_was_ever_recorded()
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

        var result = await SectorRuleSeeder.ReconcileOnStartupAsync(db, cancellationToken: CancellationToken.None);

        Assert.False(result.Forced);
        var refreshedStamp = await db.SectorRuleSetStamps.SingleAsync();
        Assert.Equal(SectorRuleSeeder.ComputeCatalogHash(), refreshedStamp.CatalogContentHash);
        Assert.Equal(6, await db.SectorSegments.CountAsync());
    }

    /// <summary>
    /// Two independent instances (fresh contexts over the same database name) each reconciling at
    /// startup must converge to a single seeded rule set without duplicates. On relational providers
    /// this is serialized with sp_getapplock; on the InMemory host the idempotent single-commit seed
    /// is sufficient.
    /// </summary>
    [Fact]
    public async Task ReconcileOnStartup_two_instances_converge_without_duplicates()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using (var first = NewDb(databaseName))
            await SectorRuleSeeder.ReconcileOnStartupAsync(first, cancellationToken: CancellationToken.None);
        await using (var second = NewDb(databaseName))
            await SectorRuleSeeder.ReconcileOnStartupAsync(second, cancellationToken: CancellationToken.None);

        await using var check = NewDb(databaseName);
        Assert.Equal(6, await check.SectorSegments.CountAsync());
        Assert.Equal(ExpectedSegmentDomainLinkCount(), await check.SectorSegmentDomains.CountAsync());
        Assert.Equal(1, await check.SectorRuleSetStamps.CountAsync()); // singleton stamp, no duplicate
    }

    [Fact]
    public async Task ReconcileOnStartup_logs_a_warning_for_a_preserved_admin_managed_row()
    {
        await using var db = NewDb();
        await SectorRuleSeeder.ReconcileOnStartupAsync(db, cancellationToken: CancellationToken.None);

        var segment = await db.SectorSegments.SingleAsync(s => s.Code == CompanySegments.Commerce);
        segment.ResetFromCatalog("Libellé perso", segment.DescriptionFr, segment.IconKey, segment.SortOrder, segment.DefaultWarehouseName);
        segment.MarkAdminManaged();

        var stamp = await db.SectorRuleSetStamps.SingleAsync();
        stamp.SetCatalogHash("stale-hash-from-an-older-catalog-revision");
        await db.SaveChangesAsync();

        var logger = new CapturingLogger();
        await SectorRuleSeeder.ReconcileOnStartupAsync(db, logger, CancellationToken.None);

        Assert.Contains(logger.Messages, m => m.Contains("admin-managed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReconcileOnStartup_preserves_admin_created_dependency()
    {
        await using var db = NewDb();
        await SectorRuleSeeder.ReconcileOnStartupAsync(db, cancellationToken: CancellationToken.None);

        // An admin-authored edge (non-catalog key) marked admin-managed, exactly as the backoffice
        // CRUD does. Reconcile must not deactivate it.
        var extraEdge = SectorModuleDependency.Create((int)AppModule.CRM, (int)AppModule.Clients);
        extraEdge.MarkAdminManaged();
        db.SectorModuleDependencies.Add(extraEdge);

        var stamp = await db.SectorRuleSetStamps.SingleAsync();
        stamp.SetCatalogHash("stale-hash-from-an-older-catalog-revision");
        await db.SaveChangesAsync();

        await SectorRuleSeeder.ReconcileOnStartupAsync(db, cancellationToken: CancellationToken.None);

        var preserved = await db.SectorModuleDependencies.SingleAsync(
            d => d.ModuleId == (int)AppModule.CRM && d.RequiredModuleId == (int)AppModule.Clients);
        Assert.True(preserved.IsActive);
    }

    /// <summary>
    /// Review F3: catalog-owned rows whose key was removed from the catalog are deactivated on
    /// reconcile (never deleted), so a stale additive predefault (e.g. an accounting subaccount
    /// template item, or a base module-rule) does not linger as active metadata forever.
    /// </summary>
    [Fact]
    public async Task ReconcileOnStartup_deactivates_removed_catalog_owned_rows()
    {
        await using var db = NewDb();
        await SectorRuleSeeder.ReconcileOnStartupAsync(db, cancellationToken: CancellationToken.None);

        var templateDef = SectorConfigurationCatalog.DataTemplates[0];
        var template = await db.SectorDataTemplates.SingleAsync(t => t.Code == templateDef.Code);
        var staleItem = SectorDataTemplateItem.Create(template.Id, "chart-account", "{\"accountNumber\":\"7099\"}", 979);
        db.SectorDataTemplateItems.Add(staleItem);

        var commerceDef = SectorConfigurationCatalog.Segments.Single(s => s.Code == CompanySegments.Commerce);
        var commerce = await db.SectorSegments.SingleAsync(s => s.Code == CompanySegments.Commerce);
        var recommended = commerceDef.BaseRecommendedModules.Select(m => (int)m).ToHashSet();
        var strayModule = AppModuleExtensions.AllValues.Select(m => (int)m).First(m => !recommended.Contains(m));
        var staleRule = SectorModuleRule.CreateSegmentBase(commerce.Id, strayModule, 900);
        db.SectorModuleRules.Add(staleRule);

        var stamp = await db.SectorRuleSetStamps.SingleAsync();
        stamp.SetCatalogHash("stale-hash-from-an-older-catalog-revision");
        await db.SaveChangesAsync();

        await SectorRuleSeeder.ReconcileOnStartupAsync(db, cancellationToken: CancellationToken.None);

        var rereadItem = await db.SectorDataTemplateItems.FindAsync(staleItem.Id);
        Assert.NotNull(rereadItem);
        Assert.False(rereadItem!.IsActive);

        var rereadRule = await db.SectorModuleRules.FindAsync(staleRule.Id);
        Assert.NotNull(rereadRule);
        Assert.False(rereadRule!.IsActive);
    }

    /// <summary>
    /// A legacy duplicate template-item row (the pre-unique-index bug) must not crash the seeder's
    /// key lookups (the DB-level filtered unique index plus the migration de-dupe prevent this going
    /// forward; the InMemory host verifies the defensive first-wins dictionary).
    /// </summary>
    [Fact]
    public async Task ReconcileOnStartup_is_resilient_to_legacy_duplicate_template_items()
    {
        await using var db = NewDb();
        await SectorRuleSeeder.ReconcileOnStartupAsync(db, cancellationToken: CancellationToken.None);

        var templateDef = SectorConfigurationCatalog.DataTemplates[0];
        var template = await db.SectorDataTemplates.SingleAsync(t => t.Code == templateDef.Code);
        var itemDef = templateDef.Items[0];
        var canonicalId = (await db.SectorDataTemplateItems.SingleAsync(
            i => i.TemplateId == template.Id && i.ItemKind == itemDef.ItemKind && i.SortOrder == itemDef.SortOrder)).Id;

        var duplicate = SectorDataTemplateItem.Create(template.Id, itemDef.ItemKind, "{\"stale\":true}", itemDef.SortOrder);
        db.SectorDataTemplateItems.Add(duplicate);

        var stamp = await db.SectorRuleSetStamps.SingleAsync();
        stamp.SetCatalogHash("stale-hash-from-an-older-catalog-revision");
        await db.SaveChangesAsync();

        var result = await SectorRuleSeeder.ReconcileOnStartupAsync(db, cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        var canonical = await db.SectorDataTemplateItems.FindAsync(canonicalId);
        Assert.NotNull(canonical);
        Assert.True(canonical!.IsActive);
        Assert.Equal(itemDef.PayloadJson, canonical.PayloadJson);
    }

    /// <summary>
    /// A failed reconcile must not stamp the current catalog hash: the recorded hash only advances on
    /// a successful run, so a later restart correctly retries instead of believing it converged.
    /// </summary>
    [Fact]
    public async Task ReconcileOnStartup_does_not_stamp_hash_when_seed_fails()
    {
        await using var db = NewDb();
        await SectorRuleSeeder.ReconcileOnStartupAsync(db, cancellationToken: CancellationToken.None);

        // Introduce a duplicate segment Code so the seed's ToDictionary throws mid-reconcile.
        db.SectorSegments.Add(SectorSegment.Create(CompanySegments.Commerce, "Doublon", "d", "ic", 0, null));
        var stamp = await db.SectorRuleSetStamps.SingleAsync();
        stamp.SetCatalogHash("stale-hash-from-an-older-catalog-revision");
        await db.SaveChangesAsync();

        await Assert.ThrowsAnyAsync<Exception>(
            async () => { await SectorRuleSeeder.ReconcileOnStartupAsync(db, cancellationToken: CancellationToken.None); });

        var stampAfter = await db.SectorRuleSetStamps.AsNoTracking().SingleAsync();
        Assert.Equal("stale-hash-from-an-older-catalog-revision", stampAfter.CatalogContentHash);
    }

    private sealed class CapturingLogger : ILogger
    {
        public readonly List<string> Messages = new();

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));

        public bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    }
}
