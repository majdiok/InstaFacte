using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.SectorConfiguration;

namespace FactuTrust.Infrastructure.Services.SectorCatalog;

/// <summary>
/// Permanent fallback <see cref="ISectorCatalogProvider"/> wrapping <see cref="SectorConfigurationCatalog"/>
/// (plan §WP-B2, D2). Built once in a <c>static readonly</c> — zero allocation per call, so the
/// hot registration path pays nothing extra when <c>UseDbRules=false</c>. <b>Never deleted</b>:
/// this is the class that keeps Phase 1 behavior available forever, flag-off or DB-outage.
/// </summary>
public sealed class StaticSectorCatalogProvider : ISectorCatalogProvider
{
    private static readonly SectorRuleSnapshot CachedSnapshot = BuildSnapshot();

    public SectorRuleSnapshot GetSnapshot() => CachedSnapshot;

    private static SectorRuleSnapshot BuildSnapshot()
    {
        // Phase 1 dynamic configuration (plan §3.1/§3.2): each segment only lists the domains from
        // its catalog matrix (SegmentDefinition.AllowedDomainCodes) — no longer "every domain for
        // every segment".
        var segments = SectorConfigurationCatalog.Segments
            .OrderBy(s => s.SortOrder)
            .Select(s => new SegmentSnapshot
            {
                Code = s.Code,
                LabelFr = s.LabelFr,
                DescriptionFr = s.DescriptionFr,
                IconKey = s.IconKey,
                SortOrder = s.SortOrder,
                DefaultWarehouseName = s.DefaultWarehouseName,
                BaseRecommendedModules = s.BaseRecommendedModules,
                DomainCodes = s.AllowedDomainCodes
            })
            .ToList();

        var domains = SectorConfigurationCatalog.Domains
            .OrderBy(d => d.SortOrder)
            .Select(d => new DomainSnapshot
            {
                Code = d.Code,
                LabelFr = d.LabelFr,
                SortOrder = d.SortOrder,
                OverlayModules = d.OverlayModules
            })
            .ToList();

        var defaultSettings = SectorConfigurationCatalog.Segments
            .Where(s => s.DefaultWarehouseName is not null)
            .Select(s => new DefaultSettingSnapshot
            {
                SegmentCode = s.Code,
                DomainCode = null,
                SettingKey = "default-warehouse-name",
                SettingValue = s.DefaultWarehouseName!,
                ValueType = "string"
            })
            .ToList();

        var moduleDependencies = SectorConfigurationCatalog.ModuleDependencies
            .Select(e => new ModuleDependencySnapshot
            {
                ModuleId = (int)e.Module,
                RequiredModuleId = (int)e.RequiredModule
            })
            .ToList();

        var dataTemplates = SectorConfigurationCatalog.DataTemplates
            .OrderBy(t => t.SortOrder)
            .Select(t => new DataTemplateSnapshot
            {
                Code = t.Code,
                SegmentCode = t.SegmentCode,
                DomainCode = t.DomainCode,
                LabelFr = t.LabelFr,
                DescriptionFr = t.DescriptionFr,
                Version = t.Version,
                SortOrder = t.SortOrder,
                Items = t.Items
                    .OrderBy(i => i.SortOrder)
                    .Select(i => new DataTemplateItemSnapshot
                    {
                        ItemKind = i.ItemKind,
                        PayloadJson = i.PayloadJson,
                        SortOrder = i.SortOrder
                    })
                    .ToList()
            })
            .ToList();

        return new SectorRuleSnapshot
        {
            Source = SectorRuleSource.Static,
            Version = 0,
            Segments = segments,
            Domains = domains,
            ModuleDependencies = moduleDependencies,
            DefaultSettings = defaultSettings,
            DataTemplates = dataTemplates
        };
    }
}
