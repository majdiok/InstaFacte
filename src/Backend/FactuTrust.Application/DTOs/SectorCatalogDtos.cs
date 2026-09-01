namespace FactuTrust.Application.DTOs;

/// <summary>
/// Response shape for <c>GET /api/public/sector-catalog</c> (plan §3 C1/C8, §6.1 B6). Built 100%
/// from the static <c>SectorConfigurationCatalog</c> — no DB, no parameters.
/// </summary>
public sealed record SectorCatalogDto
{
    public required IReadOnlyList<SectorSegmentDto> Segments { get; init; }
    public required IReadOnlyList<SectorDomainDto> Domains { get; init; }
    public required IReadOnlyList<SectorModuleDto> Modules { get; init; }

    /// <summary>Phase 2 (plan §WP-B4) — empty with the static provider; populated once dependencies are authored via the admin CRUD.</summary>
    public IReadOnlyList<SectorModuleDependencyDto> ModuleDependencies { get; init; } = Array.Empty<SectorModuleDependencyDto>();

    /// <summary>
    /// Plan §2.1 — opaque version tag (<c>"{source}:{version}"</c>, e.g. <c>"static:0"</c> or
    /// <c>"db:12"</c>), mirrors <c>SectorRuleSnapshot.CatalogVersionTag</c>. Clients cache the
    /// catalog and compare this to detect a change without re-fetching. Also echoed as the
    /// response's <c>ETag</c> header.
    /// </summary>
    public required string CatalogVersion { get; init; }
}

public sealed record SectorSegmentDto
{
    public required string Code { get; init; }
    public required string LabelFr { get; init; }
    public required string DescriptionFr { get; init; }
    public required string IconKey { get; init; }
    public required int SortOrder { get; init; }
    public required IReadOnlyList<int> CoreModuleIds { get; init; }
    public required IReadOnlyList<int> RecommendedModuleIds { get; init; }
    public string? DefaultWarehouseName { get; init; }

    /// <summary>Phase 2 (plan §WP-B4) — domains explicitly available for this segment; the static provider always lists every domain (no restriction).</summary>
    public IReadOnlyList<string> DomainCodes { get; init; } = Array.Empty<string>();
}

public sealed record SectorDomainDto
{
    public required string Code { get; init; }
    public required string LabelFr { get; init; }
    public required int SortOrder { get; init; }

    /// <summary>Modules added on top of the segment base when this domain is selected.</summary>
    public required IReadOnlyList<int> AdditionalModuleIds { get; init; }
}

public sealed record SectorModuleDto
{
    public required int Id { get; init; }
    public required string Code { get; init; }
    public required string LabelFr { get; init; }
    public required bool IsCore { get; init; }
}

/// <summary>Phase 2 (plan §WP-B4) — a dependency edge: selecting <see cref="ModuleId"/> auto-pulls <see cref="RequiredModuleId"/>.</summary>
public sealed record SectorModuleDependencyDto
{
    public required int ModuleId { get; init; }
    public required int RequiredModuleId { get; init; }
}
