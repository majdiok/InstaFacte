using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Entities.SectorRules;
using FactuTrust.Domain.SectorConfiguration;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace FactuTrust.Infrastructure.Services.SectorCatalog;

/// <summary>
/// Reads the WP-B1 master rule tables (active rows only, ordered by <c>SortOrder</c>) — plan
/// §WP-B2, D2. Two-entry <see cref="IMemoryCache"/>: a 60 s TTL single-row version read
/// (<c>sector-rules:version</c>) and a 10 min TTL full snapshot keyed by that version
/// (<c>sector-rules:snapshot:{version}</c>) so an edit anywhere in the backoffice reaches
/// registration within ~60 s. Cache-miss path uses <b>sync</b> EF queries (bounded, ≤ 8 queries)
/// — only exercised when <c>UseDbRules=true</c> and only once per version change per process.
/// </summary>
public sealed class DbSectorCatalogProvider : ISectorCatalogProvider
{
    private const string VersionCacheKey = "sector-rules:version";
    private const string SnapshotCacheKeyPrefix = "sector-rules:snapshot:";
    private static readonly TimeSpan VersionTtl = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan SnapshotTtl = TimeSpan.FromMinutes(10);

    private readonly MasterDbContext _db;
    private readonly IMemoryCache _cache;

    public DbSectorCatalogProvider(MasterDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public SectorRuleSnapshot GetSnapshot()
    {
        var version = _cache.GetOrCreate(VersionCacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = VersionTtl;
            return _db.SectorRuleSetStamps.AsNoTracking().Select(s => s.Version).SingleOrDefault();
        });

        var snapshotKey = SnapshotCacheKeyPrefix + version;
        return _cache.GetOrCreate(snapshotKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = SnapshotTtl;
            return LoadSnapshot(version);
        })!;
    }

    private SectorRuleSnapshot LoadSnapshot(long version)
    {
        var segmentRows = _db.SectorSegments.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.SortOrder)
            .ToList();

        var domainRows = _db.SectorDomains.AsNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.SortOrder)
            .ToList();

        var segmentDomainRows = _db.SectorSegmentDomains.AsNoTracking()
            .Where(sd => sd.IsActive)
            .OrderBy(sd => sd.SortOrder)
            .ToList();

        var moduleRuleRows = _db.SectorModuleRules.AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.SortOrder)
            .ToList();

        var dependencyRows = _db.SectorModuleDependencies.AsNoTracking()
            .Where(d => d.IsActive)
            .ToList();

        var defaultSettingRows = _db.SectorDefaultSettings.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.SortOrder)
            .ToList();

        var templateRows = _db.SectorDataTemplates.AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.SortOrder)
            .ToList();

        var templateItemRows = _db.SectorDataTemplateItems.AsNoTracking()
            .Where(i => i.IsActive)
            .OrderBy(i => i.SortOrder)
            .ToList();

        var domainsById = domainRows.ToDictionary(d => d.Id);
        var domainCodesBySegmentId = segmentDomainRows
            .Where(sd => domainsById.ContainsKey(sd.DomainId))
            .GroupBy(sd => sd.SegmentId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(sd => domainsById[sd.DomainId].Code).ToList());

        var baseModulesBySegmentId = moduleRuleRows
            .Where(r => r.RuleKind == SectorModuleRuleKind.SegmentBase && r.SegmentId.HasValue)
            .GroupBy(r => r.SegmentId!.Value)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Domain.Enums.AppModule>)g.Select(r => (Domain.Enums.AppModule)r.ModuleId).ToList());

        var overlayModulesByDomainId = moduleRuleRows
            .Where(r => r.RuleKind == SectorModuleRuleKind.DomainOverlay && r.DomainId.HasValue)
            .GroupBy(r => r.DomainId!.Value)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Domain.Enums.AppModule>)g.Select(r => (Domain.Enums.AppModule)r.ModuleId).ToList());

        var segments = segmentRows.Select(s => new SegmentSnapshot
        {
            Code = s.Code,
            LabelFr = s.LabelFr,
            DescriptionFr = s.DescriptionFr,
            IconKey = s.IconKey,
            SortOrder = s.SortOrder,
            DefaultWarehouseName = s.DefaultWarehouseName,
            BaseRecommendedModules = baseModulesBySegmentId.TryGetValue(s.Id, out var baseModules)
                ? baseModules
                : Array.Empty<Domain.Enums.AppModule>(),
            DomainCodes = domainCodesBySegmentId.TryGetValue(s.Id, out var domainCodes)
                ? domainCodes
                : Array.Empty<string>()
        }).ToList();

        var domains = domainRows.Select(d => new DomainSnapshot
        {
            Code = d.Code,
            LabelFr = d.LabelFr,
            SortOrder = d.SortOrder,
            OverlayModules = overlayModulesByDomainId.TryGetValue(d.Id, out var overlayModules)
                ? overlayModules
                : Array.Empty<Domain.Enums.AppModule>()
        }).ToList();

        var moduleDependencies = dependencyRows
            .Select(d => new ModuleDependencySnapshot { ModuleId = d.ModuleId, RequiredModuleId = d.RequiredModuleId })
            .ToList();

        var defaultSettings = defaultSettingRows
            .Select(s => new DefaultSettingSnapshot
            {
                SegmentCode = s.SegmentCode,
                DomainCode = s.DomainCode,
                SettingKey = s.SettingKey,
                SettingValue = s.SettingValue,
                ValueType = s.ValueType
            })
            .ToList();

        var itemsByTemplateId = templateItemRows
            .GroupBy(i => i.TemplateId)
            .ToDictionary(g => g.Key, g => g.Select(i => new DataTemplateItemSnapshot
            {
                ItemKind = i.ItemKind,
                PayloadJson = i.PayloadJson,
                SortOrder = i.SortOrder
            }).ToList());

        var dataTemplates = templateRows.Select(t => new DataTemplateSnapshot
        {
            Code = t.Code,
            SegmentCode = t.SegmentCode,
            DomainCode = t.DomainCode,
            LabelFr = t.LabelFr,
            DescriptionFr = t.DescriptionFr,
            Version = t.Version,
            SortOrder = t.SortOrder,
            Items = itemsByTemplateId.TryGetValue(t.Id, out var items)
                ? items
                : Array.Empty<DataTemplateItemSnapshot>()
        }).ToList();

        return new SectorRuleSnapshot
        {
            Source = SectorRuleSource.Db,
            Version = version,
            Segments = segments,
            Domains = domains,
            ModuleDependencies = moduleDependencies,
            DefaultSettings = defaultSettings,
            DataTemplates = dataTemplates
        };
    }
}
