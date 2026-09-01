using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.SectorConfiguration;

/// <summary>Origin of a <see cref="SectorRuleSnapshot"/> — informs cache headers and log warnings (plan §WP-B2).</summary>
public enum SectorRuleSource
{
    Static = 0,
    Db = 1
}

/// <summary>
/// Immutable snapshot of one segment's rule-table row (plan §WP-B2). Mirrors
/// <c>SegmentDefinition</c> plus the Phase 2 additive <see cref="DomainCodes"/> link list.
/// </summary>
public sealed record SegmentSnapshot
{
    public required string Code { get; init; }
    public required string LabelFr { get; init; }
    public required string DescriptionFr { get; init; }
    public required string IconKey { get; init; }
    public required int SortOrder { get; init; }
    public string? DefaultWarehouseName { get; init; }
    public required IReadOnlyList<AppModule> BaseRecommendedModules { get; init; }

    /// <summary>
    /// Domains explicitly available for this segment (ordered). Empty ⇒ no restriction (every
    /// known domain is allowed) — this is both the static provider's permanent behavior and the
    /// defensive fallback for a DB segment an admin has stripped of all its links.
    /// </summary>
    public required IReadOnlyList<string> DomainCodes { get; init; }
}

/// <summary>Immutable snapshot of one domain's rule-table row (plan §WP-B2). Mirrors <c>DomainDefinition</c>.</summary>
public sealed record DomainSnapshot
{
    public required string Code { get; init; }
    public required string LabelFr { get; init; }
    public required int SortOrder { get; init; }
    public required IReadOnlyList<AppModule> OverlayModules { get; init; }
}

/// <summary>Directed dependency edge: selecting <see cref="ModuleId"/> auto-pulls <see cref="RequiredModuleId"/> (plan §WP-B4).</summary>
public sealed record ModuleDependencySnapshot
{
    public required int ModuleId { get; init; }
    public required int RequiredModuleId { get; init; }
}

/// <summary>One <c>SectorDefaultSettings</c> row (plan §WP-B3), informational only.</summary>
public sealed record DefaultSettingSnapshot
{
    public string? SegmentCode { get; init; }
    public string? DomainCode { get; init; }
    public required string SettingKey { get; init; }
    public required string SettingValue { get; init; }
    public required string ValueType { get; init; }
}

/// <summary>One <c>SectorDataTemplateItems</c> row nested under its template (plan §WP-B6).</summary>
public sealed record DataTemplateItemSnapshot
{
    public required string ItemKind { get; init; }
    public required string PayloadJson { get; init; }
    public required int SortOrder { get; init; }
}

/// <summary>One <c>SectorDataTemplates</c> row with its items (plan §WP-B6).</summary>
public sealed record DataTemplateSnapshot
{
    public required string Code { get; init; }
    public string? SegmentCode { get; init; }
    public string? DomainCode { get; init; }
    public required string LabelFr { get; init; }
    public string? DescriptionFr { get; init; }
    public required int Version { get; init; }
    public required int SortOrder { get; init; }
    public required IReadOnlyList<DataTemplateItemSnapshot> Items { get; init; }
}

/// <summary>
/// Immutable, cache-friendly view over the sector rule set — either the permanent static catalog
/// or a point-in-time read of the master-DB rule tables (plan §WP-B2, D2). Consumed synchronously
/// by <c>IRegistrationSectorService.ResolveProfile</c> (locked sync signature) and by
/// <c>PublicSectorCatalogController</c>/<c>SectorDataTemplateApplier</c>.
/// </summary>
public sealed record SectorRuleSnapshot
{
    public required SectorRuleSource Source { get; init; }
    public required long Version { get; init; }
    public required IReadOnlyList<SegmentSnapshot> Segments { get; init; }
    public required IReadOnlyList<DomainSnapshot> Domains { get; init; }
    public required IReadOnlyList<ModuleDependencySnapshot> ModuleDependencies { get; init; }
    public required IReadOnlyList<DefaultSettingSnapshot> DefaultSettings { get; init; }
    public required IReadOnlyList<DataTemplateSnapshot> DataTemplates { get; init; }

    /// <summary>
    /// Opaque version tag exposed to clients (plan §2.1 — catalogue sectoriel versionné), e.g.
    /// <c>"static:0"</c> or <c>"db:12"</c>. Lowercase source name + <see cref="Version"/>. Stable
    /// across process restarts for the static source (always <c>"static:0"</c>); bumped whenever
    /// <c>SectorRuleAdminService</c> commits a rule change for the DB source.
    /// </summary>
    public string CatalogVersionTag => $"{Source.ToString().ToLowerInvariant()}:{Version}";

    /// <summary>
    /// Merges segment base ∪ domain overlay − core, mirroring
    /// <c>SectorConfigurationCatalog.Resolve</c>'s exact semantics. Null/unknown segment (after
    /// normalization) ⇒ null. Unknown domain resolves as if none was supplied.
    /// </summary>
    public SectorProfile? Resolve(string? segmentCode, string? domainCode)
    {
        var normalizedSegment = CompanySegments.Normalize(segmentCode);
        if (normalizedSegment is null)
            return null;

        var segment = Segments.FirstOrDefault(s => string.Equals(s.Code, normalizedSegment, StringComparison.Ordinal));
        if (segment is null)
            return null;

        var normalizedDomain = BusinessDomains.Normalize(domainCode);
        DomainSnapshot? domain = null;
        if (normalizedDomain is not null)
            domain = Domains.FirstOrDefault(d => string.Equals(d.Code, normalizedDomain, StringComparison.Ordinal));

        var coreModules = SectorConfigurationCatalog.CoreModules;
        var coreModuleSet = new HashSet<AppModule>(coreModules);

        var recommended = new List<AppModule>();
        var recommendedSet = new HashSet<AppModule>();

        void AddRecommended(IEnumerable<AppModule> modules)
        {
            foreach (var module in modules)
            {
                if (coreModuleSet.Contains(module))
                    continue;
                if (recommendedSet.Add(module))
                    recommended.Add(module);
            }
        }

        AddRecommended(segment.BaseRecommendedModules);
        if (domain is not null)
            AddRecommended(domain.OverlayModules);

        var optional = AppModuleExtensions.AllValues
            .Where(m => !coreModuleSet.Contains(m) && !recommendedSet.Contains(m) && m != AppModule.Honoraires)
            .ToList();

        return new SectorProfile
        {
            SegmentCode = segment.Code,
            DomainCode = domain?.Code,
            CoreModules = coreModules,
            RecommendedModules = recommended,
            OptionalModules = optional,
            DefaultWarehouseName = segment.DefaultWarehouseName
        };
    }
}
