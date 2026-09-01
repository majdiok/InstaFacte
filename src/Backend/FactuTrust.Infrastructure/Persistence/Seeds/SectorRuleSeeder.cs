using FactuTrust.Application.Common;
using FactuTrust.Domain.Entities.SectorRules;
using FactuTrust.Domain.SectorConfiguration;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FactuTrust.Infrastructure.Persistence.Seeds;

/// <summary>
/// Idempotent upsert-by-code seeder that projects the static <see cref="SectorConfigurationCatalog"/>
/// into the master-DB sector-rule tables (plan §WP-B3, D3). Mirrors <see cref="PlanSeeder"/>'s style.
///
/// <para>
/// <c>force=false</c> (default): inserts rows missing from the DB only — never touches an existing
/// row, so any admin edit made through the backoffice CRUD (WP-B5) survives every startup/re-sync.
/// </para>
/// <para>
/// <c>force=true</c>: additionally resets every catalog-known row back to the catalog's values and
/// reactivates it (<c>IsActive=true</c>) — an explicit "restore factory defaults" for the rows the
/// catalog knows about. Rows an admin created that aren't in the catalog (extra segments, domains,
/// dependency edges, data templates, ...) are never touched, and nothing is ever deleted.
/// </para>
/// <para>
/// <see cref="SectorModuleDependency"/> and <see cref="SectorDataTemplate"/> rows: the catalog-declared
/// edges/templates (plan §4.2/§4.3) are seeded below the same way as segments/domains
/// (insert-if-missing, reset-on-force). Dependency edges are a link-type row over a bounded,
/// enumerable (ModuleId, RequiredModuleId) space — exactly like the segment↔domain link matrix
/// above — so on <c>force=true</c> any edge in that space that is no longer catalog-declared is
/// deactivated (never deleted), including one an admin added directly between two existing
/// modules. Templates are an entity-type row identified by <c>Code</c> (like segments/domains): a
/// template whose <c>Code</c> isn't in the catalog is never touched. Template items are matched by
/// the natural key <c>(ItemKind, SortOrder)</c> within their template; on <c>force=true</c> an
/// existing item's <c>PayloadJson</c> is refreshed to the catalog's current value (review R3) so a
/// template <c>Version</c> bump can't silently publish a new version number over a stale payload.
/// </para>
/// </summary>
public static class SectorRuleSeeder
{
    private const string GlobalPlanComptableVariantKey = "plan-comptable-variant";
    private const string DefaultWarehouseNameSettingKey = "default-warehouse-name";

    /// <summary>No-ops unless <c>SectorSegments</c> has zero rows — safe to call unconditionally at startup.</summary>
    public static async Task SeedIfEmptyAsync(MasterDbContext context, CancellationToken cancellationToken = default)
    {
        var hasAnySegment = await context.SectorSegments.AnyAsync(cancellationToken);
        if (hasAnySegment)
            return;

        await SeedAsync(context, force: false, actor: "seeder", cancellationToken);
    }

    /// <summary>
    /// Deterministic SHA-256 hex digest of the in-memory catalog's full content (review R2) — used
    /// to detect a catalog change across app restarts without diffing every row. Built from the
    /// same <see cref="SectorConfigurationCatalog.BuildCatalogSnapshot"/> the parity checker and
    /// seeder itself consume, so any segment/domain/dependency/template addition, removal, or edit
    /// in the catalog source changes the hash.
    /// </summary>
    public static string ComputeCatalogHash()
    {
        var snapshot = SectorConfigurationCatalog.BuildCatalogSnapshot();
        var json = JsonSerializer.Serialize(snapshot);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// Startup entry point (review R2), replacing the old <see cref="SeedIfEmptyAsync"/> call in
    /// <c>Program.cs</c>: an empty <c>SectorSegments</c> table still gets a plain seed, but an
    /// EXISTING deployment now also reconciles automatically whenever the catalog's content hash
    /// has drifted from the one recorded on the last seed/reconcile run — e.g. a code deploy that
    /// added a segment, a dependency edge, or bumped a template's payload. Without this, an old
    /// deployment's stale rule set (from before this catalog revision) would keep governing
    /// <c>UseDbRules=true</c> registrations forever until an admin manually hit the force-seed
    /// endpoint.
    ///
    /// <para>
    /// Concurrency: multiple app instances can start simultaneously and race into this method with
    /// an identical, deterministic <c>force=true</c> seed. Every write below is upsert-by-natural-
    /// key over unique-indexed tables, so the SQL-level failure mode of a genuine race is a unique
    /// key violation surfaced as a <see cref="DbUpdateException"/> — never a partial or
    /// inconsistent row (each <c>SaveChangesAsync</c> call is one implicit transaction: it's all-
    /// or-nothing). This method deliberately does NOT swallow that exception: the existing startup
    /// seeding block in <c>Program.cs</c> already wraps the whole seeding sequence in a try/catch
    /// that logs and continues in production (rethrows only in Development) — exactly the same
    /// resilience every other seeder here (roles, plans, fiscal calendar) already relies on. A
    /// losing instance simply retries reconciliation on its next restart; the DB is left exactly as
    /// the winning instance left it, which already matches the current catalog.
    /// </para>
    /// </summary>
    public static async Task<SectorRuleSeedResult> ReconcileOnStartupAsync(MasterDbContext context, CancellationToken cancellationToken = default)
    {
        var hasAnySegment = await context.SectorSegments.AnyAsync(cancellationToken);
        if (!hasAnySegment)
            return await SeedAsync(context, force: false, actor: "startup-reconcile", cancellationToken);

        var currentHash = ComputeCatalogHash();
        var stamp = await context.SectorRuleSetStamps.SingleOrDefaultAsync(
            s => s.Id == SectorRuleSetStamp.SingletonId, cancellationToken);

        if (stamp is not null && string.Equals(stamp.CatalogContentHash, currentHash, StringComparison.Ordinal))
        {
            // Catalog unchanged since the last seed/reconcile run — nothing to do.
            return new SectorRuleSeedResult(0, 0, 0, stamp.Version, Forced: false);
        }

        // Hash missing (upgrading from a pre-R2 deployment) or drifted: run a full reconciliation
        // so every catalog-known surface (segment↔domain matrix, dependency edges, template
        // payloads) converges to the current catalog. Admin-authored, non-catalog rows are never
        // touched — same guarantee as any other force=true run (see class doc above).
        return await SeedAsync(context, force: true, actor: "startup-reconcile", cancellationToken);
    }

    public static async Task<SectorRuleSeedResult> SeedAsync(
        MasterDbContext context,
        bool force,
        string? actor,
        CancellationToken cancellationToken = default)
    {
        var effectiveActor = actor ?? "seeder";
        var inserted = 0;
        var updated = 0;
        var skippedExisting = 0;

        // ---------- Segments ----------
        var segmentsByCode = (await context.SectorSegments.ToListAsync(cancellationToken))
            .ToDictionary(s => s.Code, StringComparer.Ordinal);

        foreach (var def in SectorConfigurationCatalog.Segments)
        {
            if (segmentsByCode.TryGetValue(def.Code, out var existing))
            {
                if (force)
                {
                    existing.ResetFromCatalog(def.LabelFr, def.DescriptionFr, def.IconKey, def.SortOrder, def.DefaultWarehouseName);
                    existing.SetAuditInfo(effectiveActor, isUpdate: true);
                    updated++;
                }
                else
                {
                    skippedExisting++;
                }
            }
            else
            {
                var created = SectorSegment.Create(def.Code, def.LabelFr, def.DescriptionFr, def.IconKey, def.SortOrder, def.DefaultWarehouseName);
                created.SetAuditInfo(effectiveActor);
                context.SectorSegments.Add(created);
                segmentsByCode[def.Code] = created;
                inserted++;
            }
        }

        // ---------- Domains ----------
        var domainsByCode = (await context.SectorDomains.ToListAsync(cancellationToken))
            .ToDictionary(d => d.Code, StringComparer.Ordinal);

        foreach (var def in SectorConfigurationCatalog.Domains)
        {
            if (domainsByCode.TryGetValue(def.Code, out var existing))
            {
                if (force)
                {
                    existing.ResetFromCatalog(def.LabelFr, def.SortOrder);
                    existing.SetAuditInfo(effectiveActor, isUpdate: true);
                    updated++;
                }
                else
                {
                    skippedExisting++;
                }
            }
            else
            {
                var created = SectorDomain.Create(def.Code, def.LabelFr, def.SortOrder);
                created.SetAuditInfo(effectiveActor);
                context.SectorDomains.Add(created);
                domainsByCode[def.Code] = created;
                inserted++;
            }
        }

        // A brand-new segment/domain has Id == Guid.Empty until SaveChanges assigns nothing (Id is
        // generated client-side by the Entity ctor, so it's already valid at this point) — no
        // intermediate SaveChangesAsync is needed before wiring the links/rules below.

        // ---------- Segment ↔ Domain links (plan §3.1/§3.2 matrix: only the segment↔domain pairs
        // declared in SegmentDefinition.AllowedDomainCodes; per-link SortOrder mirrors the domain's
        // own SortOrder) ----------
        var existingLinks = (await context.SectorSegmentDomains.ToListAsync(cancellationToken))
            .ToDictionary(l => (l.SegmentId, l.DomainId));

        foreach (var segmentDef in SectorConfigurationCatalog.Segments)
        {
            var segment = segmentsByCode[segmentDef.Code];
            var allowedDomainCodes = new HashSet<string>(segmentDef.AllowedDomainCodes, StringComparer.Ordinal);

            foreach (var domainDef in SectorConfigurationCatalog.Domains)
            {
                var domain = domainsByCode[domainDef.Code];
                var key = (segment.Id, domain.Id);
                var isMatrixPair = allowedDomainCodes.Contains(domainDef.Code);

                if (existingLinks.TryGetValue(key, out var existingLink))
                {
                    if (isMatrixPair)
                    {
                        if (force)
                        {
                            existingLink.UpdateSortOrder(domainDef.SortOrder);
                            existingLink.Reactivate();
                            existingLink.SetAuditInfo(effectiveActor, isUpdate: true);
                            updated++;
                        }
                        else
                        {
                            skippedExisting++;
                        }
                    }
                    else
                    {
                        // Catalog-known segment/domain pair that is no longer in the matrix. Never
                        // DELETE — force=true deactivates it (restore-factory-defaults semantics);
                        // without force the existing row (whatever an admin left it as) is untouched.
                        if (force && existingLink.IsActive)
                        {
                            existingLink.Deactivate();
                            existingLink.SetAuditInfo(effectiveActor, isUpdate: true);
                            updated++;
                        }
                        else
                        {
                            skippedExisting++;
                        }
                    }
                }
                else if (isMatrixPair)
                {
                    var created = SectorSegmentDomain.Create(segment.Id, domain.Id, domainDef.SortOrder);
                    created.SetAuditInfo(effectiveActor);
                    context.SectorSegmentDomains.Add(created);
                    existingLinks[key] = created;
                    inserted++;
                }
                // else: pair absent from both the DB and the matrix — nothing to do (admin-created
                // links on non-catalog segments/domains are never enumerated here in the first place).
            }
        }

        // ---------- Module rules: segment base recommendations + domain overlays ----------
        var existingSegmentBaseRules = (await context.SectorModuleRules
                .Where(r => r.RuleKind == SectorModuleRuleKind.SegmentBase)
                .ToListAsync(cancellationToken))
            .ToDictionary(r => (r.SegmentId, r.ModuleId));

        foreach (var segmentDef in SectorConfigurationCatalog.Segments)
        {
            var segment = segmentsByCode[segmentDef.Code];
            var sortOrder = 0;
            foreach (var module in segmentDef.BaseRecommendedModules)
            {
                var key = ((Guid?)segment.Id, (int)module);
                if (existingSegmentBaseRules.TryGetValue(key, out var existingRule))
                {
                    if (force)
                    {
                        existingRule.UpdateSortOrder(sortOrder);
                        existingRule.Reactivate();
                        existingRule.SetAuditInfo(effectiveActor, isUpdate: true);
                        updated++;
                    }
                    else
                    {
                        skippedExisting++;
                    }
                }
                else
                {
                    var created = SectorModuleRule.CreateSegmentBase(segment.Id, (int)module, sortOrder);
                    created.SetAuditInfo(effectiveActor);
                    context.SectorModuleRules.Add(created);
                    existingSegmentBaseRules[key] = created;
                    inserted++;
                }

                sortOrder++;
            }
        }

        var existingDomainOverlayRules = (await context.SectorModuleRules
                .Where(r => r.RuleKind == SectorModuleRuleKind.DomainOverlay)
                .ToListAsync(cancellationToken))
            .ToDictionary(r => (r.DomainId, r.ModuleId));

        foreach (var domainDef in SectorConfigurationCatalog.Domains)
        {
            var domain = domainsByCode[domainDef.Code];
            var sortOrder = 0;
            foreach (var module in domainDef.OverlayModules)
            {
                var key = ((Guid?)domain.Id, (int)module);
                if (existingDomainOverlayRules.TryGetValue(key, out var existingRule))
                {
                    if (force)
                    {
                        existingRule.UpdateSortOrder(sortOrder);
                        existingRule.Reactivate();
                        existingRule.SetAuditInfo(effectiveActor, isUpdate: true);
                        updated++;
                    }
                    else
                    {
                        skippedExisting++;
                    }
                }
                else
                {
                    var created = SectorModuleRule.CreateDomainOverlay(domain.Id, (int)module, sortOrder);
                    created.SetAuditInfo(effectiveActor);
                    context.SectorModuleRules.Add(created);
                    existingDomainOverlayRules[key] = created;
                    inserted++;
                }

                sortOrder++;
            }
        }

        // ---------- Module dependencies (plan §4.2: catalog-declared edges only) ----------
        var existingDependencies = (await context.SectorModuleDependencies.ToListAsync(cancellationToken))
            .ToDictionary(d => (d.ModuleId, d.RequiredModuleId));

        var catalogDependencyKeys = new HashSet<(int ModuleId, int RequiredModuleId)>();
        foreach (var edge in SectorConfigurationCatalog.ModuleDependencies)
        {
            var key = (ModuleId: (int)edge.Module, RequiredModuleId: (int)edge.RequiredModule);
            catalogDependencyKeys.Add(key);

            if (existingDependencies.TryGetValue(key, out var existingDependency))
            {
                if (force)
                {
                    existingDependency.Reactivate();
                    updated++;
                }
                else
                {
                    skippedExisting++;
                }
            }
            else
            {
                var created = SectorModuleDependency.Create(key.ModuleId, key.RequiredModuleId);
                context.SectorModuleDependencies.Add(created);
                existingDependencies[key] = created;
                inserted++;
            }
        }

        if (force)
        {
            foreach (var (key, dependency) in existingDependencies)
            {
                if (!catalogDependencyKeys.Contains(key) && dependency.IsActive)
                {
                    // Catalog-known-turned-removed edge: same "deactivate, never delete" semantics
                    // as the segment↔domain link matrix above. Non-catalog edges authored by an
                    // admin (key never appeared in a past catalog run) are indistinguishable from
                    // this case at the DB level, so force=true also retires those — acceptable
                    // because force is an explicit "restore factory defaults" operation.
                    dependency.Deactivate();
                    updated++;
                }
            }
        }

        // ---------- Data templates + items (plan §4.3: additive sector presets) ----------
        var existingTemplatesByCode = (await context.SectorDataTemplates.ToListAsync(cancellationToken))
            .ToDictionary(t => t.Code, StringComparer.Ordinal);

        foreach (var templateDef in SectorConfigurationCatalog.DataTemplates)
        {
            SectorDataTemplate template;
            if (existingTemplatesByCode.TryGetValue(templateDef.Code, out var existingTemplate))
            {
                template = existingTemplate;
                if (force)
                {
                    existingTemplate.UpdateDetails(templateDef.LabelFr, templateDef.DescriptionFr, templateDef.Version, templateDef.SortOrder);
                    existingTemplate.UpdateScope(templateDef.SegmentCode, templateDef.DomainCode);
                    existingTemplate.Reactivate();
                    updated++;
                }
                else
                {
                    skippedExisting++;
                }
            }
            else
            {
                template = SectorDataTemplate.Create(
                    templateDef.Code,
                    templateDef.SegmentCode,
                    templateDef.DomainCode,
                    templateDef.LabelFr,
                    templateDef.DescriptionFr,
                    templateDef.Version,
                    templateDef.SortOrder);
                context.SectorDataTemplates.Add(template);
                existingTemplatesByCode[templateDef.Code] = template;
                inserted++;
            }

            // Items have no natural business key beyond (ItemKind, SortOrder) within their
            // template — insert-if-missing, and on force=true the payload is refreshed to the
            // catalog's current value (review R3) so a template Version bump doesn't publish a
            // new version number over a stale account payload.
            var existingItemsByKey = (await context.SectorDataTemplateItems
                    .Where(i => i.TemplateId == template.Id)
                    .ToListAsync(cancellationToken))
                .ToDictionary(i => (i.ItemKind, i.SortOrder));

            foreach (var itemDef in templateDef.Items)
            {
                var itemKey = (itemDef.ItemKind, itemDef.SortOrder);
                if (existingItemsByKey.TryGetValue(itemKey, out var existingItem))
                {
                    if (force)
                    {
                        existingItem.UpdatePayload(itemDef.PayloadJson);
                        if (!existingItem.IsActive)
                            existingItem.Reactivate();
                        updated++;
                    }
                    else
                    {
                        skippedExisting++;
                    }
                }
                else
                {
                    var createdItem = SectorDataTemplateItem.Create(template.Id, itemDef.ItemKind, itemDef.PayloadJson, itemDef.SortOrder);
                    context.SectorDataTemplateItems.Add(createdItem);
                    existingItemsByKey[itemKey] = createdItem;
                    inserted++;
                }
            }
        }

        // ---------- Default settings: per-segment default warehouse name + one global row ----------
        var existingSettings = (await context.SectorDefaultSettings.ToListAsync(cancellationToken))
            .ToDictionary(s => (s.SegmentCode, s.DomainCode, s.SettingKey), StringTupleComparer.Instance);

        var settingSortOrder = 0;
        foreach (var segmentDef in SectorConfigurationCatalog.Segments)
        {
            if (segmentDef.DefaultWarehouseName is null)
                continue;

            UpsertDefaultSetting(
                context,
                existingSettings,
                segmentDef.Code,
                domainCode: null,
                DefaultWarehouseNameSettingKey,
                segmentDef.DefaultWarehouseName,
                "string",
                settingSortOrder,
                force,
                effectiveActor,
                ref inserted,
                ref updated,
                ref skippedExisting);

            settingSortOrder++;
        }

        UpsertDefaultSetting(
            context,
            existingSettings,
            segmentCode: null,
            domainCode: null,
            GlobalPlanComptableVariantKey,
            "nct01",
            "string",
            settingSortOrder,
            force,
            effectiveActor,
            ref inserted,
            ref updated,
            ref skippedExisting);

        // ---------- Version stamp: bumped exactly once per run, in the same SaveChangesAsync.
        // CatalogContentHash (review R2) is recorded on EVERY run (not just force) so
        // ReconcileOnStartupAsync can tell a hash-less/stale stamp apart from an up-to-date one on
        // the very next boot, even after a plain non-force seed. ----------
        var stamp = await context.SectorRuleSetStamps.SingleOrDefaultAsync(
            s => s.Id == SectorRuleSetStamp.SingletonId, cancellationToken);
        if (stamp is null)
        {
            stamp = SectorRuleSetStamp.CreateInitial();
            context.SectorRuleSetStamps.Add(stamp);
        }
        stamp.Bump(effectiveActor);
        stamp.SetCatalogHash(ComputeCatalogHash());

        await context.SaveChangesAsync(cancellationToken);

        return new SectorRuleSeedResult(inserted, updated, skippedExisting, stamp.Version, force);
    }

    private static void UpsertDefaultSetting(
        MasterDbContext context,
        Dictionary<(string? SegmentCode, string? DomainCode, string SettingKey), SectorDefaultSetting> existingSettings,
        string? segmentCode,
        string? domainCode,
        string settingKey,
        string settingValue,
        string valueType,
        int sortOrder,
        bool force,
        string actor,
        ref int inserted,
        ref int updated,
        ref int skippedExisting)
    {
        var key = (segmentCode, domainCode, settingKey);
        if (existingSettings.TryGetValue(key, out var existing))
        {
            if (force)
            {
                existing.ResetFromCatalog(settingValue, valueType, sortOrder);
                existing.SetAuditInfo(actor, isUpdate: true);
                updated++;
            }
            else
            {
                skippedExisting++;
            }
        }
        else
        {
            var created = SectorDefaultSetting.Create(segmentCode, domainCode, settingKey, settingValue, valueType, sortOrder);
            created.SetAuditInfo(actor);
            context.SectorDefaultSettings.Add(created);
            existingSettings[key] = created;
            inserted++;
        }
    }

    /// <summary>Ordinal comparer for the (string?, string?, string) natural key used by default-setting lookups.</summary>
    private sealed class StringTupleComparer : IEqualityComparer<(string? SegmentCode, string? DomainCode, string SettingKey)>
    {
        public static readonly StringTupleComparer Instance = new();

        public bool Equals((string? SegmentCode, string? DomainCode, string SettingKey) x, (string? SegmentCode, string? DomainCode, string SettingKey) y) =>
            string.Equals(x.SegmentCode, y.SegmentCode, StringComparison.Ordinal)
            && string.Equals(x.DomainCode, y.DomainCode, StringComparison.Ordinal)
            && string.Equals(x.SettingKey, y.SettingKey, StringComparison.Ordinal);

        public int GetHashCode((string? SegmentCode, string? DomainCode, string SettingKey) obj) =>
            HashCode.Combine(obj.SegmentCode, obj.DomainCode, obj.SettingKey);
    }
}
