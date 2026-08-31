using FactuTrust.Application.Common;
using FactuTrust.Domain.Entities.SectorRules;
using FactuTrust.Domain.SectorConfiguration;
using Microsoft.EntityFrameworkCore;

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
/// <see cref="SectorModuleDependency"/> and <see cref="SectorDataTemplate"/> rows are intentionally
/// never seeded here — the static catalog has none of either; they only exist once authored through
/// the admin CRUD (WP-B5/WP-B6).
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

        // ---------- Segment ↔ Domain links (exact Phase 1 semantics: every domain available to
        // every segment; per-segment SortOrder mirrors the domain's own SortOrder) ----------
        var existingLinks = (await context.SectorSegmentDomains.ToListAsync(cancellationToken))
            .ToDictionary(l => (l.SegmentId, l.DomainId));

        foreach (var segmentDef in SectorConfigurationCatalog.Segments)
        {
            var segment = segmentsByCode[segmentDef.Code];
            foreach (var domainDef in SectorConfigurationCatalog.Domains)
            {
                var domain = domainsByCode[domainDef.Code];
                var key = (segment.Id, domain.Id);

                if (existingLinks.TryGetValue(key, out var existingLink))
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
                    var created = SectorSegmentDomain.Create(segment.Id, domain.Id, domainDef.SortOrder);
                    created.SetAuditInfo(effectiveActor);
                    context.SectorSegmentDomains.Add(created);
                    existingLinks[key] = created;
                    inserted++;
                }
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

        // ---------- Version stamp: bumped exactly once per run, in the same SaveChangesAsync ----------
        var stamp = await context.SectorRuleSetStamps.SingleOrDefaultAsync(
            s => s.Id == SectorRuleSetStamp.SingletonId, cancellationToken);
        if (stamp is null)
        {
            stamp = SectorRuleSetStamp.CreateInitial();
            context.SectorRuleSetStamps.Add(stamp);
        }
        stamp.Bump(effectiveActor);

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
