namespace FactuTrust.Application.DTOs;

/// <summary>One catalog entry (segment or domain) as exposed by <c>GET /api/company/sector</c> (plan §2.3).</summary>
public sealed record CompanySectorCatalogEntryDto
{
    public required string Code { get; init; }
    public required string LabelFr { get; init; }
}

/// <summary>Response shape for <c>GET /api/company/sector</c> (plan §2.3).</summary>
public sealed record CompanySectorDto
{
    public string? CompanySegment { get; init; }
    public string? BusinessDomain { get; init; }
    public required IReadOnlyList<CompanySectorCatalogEntryDto> AvailableSegments { get; init; }
    public required IReadOnlyList<CompanySectorCatalogEntryDto> AvailableDomains { get; init; }
}

/// <summary>
/// Request body shared by <c>POST /api/company/sector/preview</c> and <c>PUT /api/company/sector</c>
/// (plan §2.3). Null segment/domain keeps the tenant's current value; an empty-string domain clears
/// it — mirrors <see cref="SectorReconfigurationRequestDto"/>'s semantics (this self-service endpoint
/// always recomputes module grants and applies templates, unlike the platform-admin flow which lets
/// an operator opt out of either step).
/// </summary>
public sealed record CompanySectorChangeRequestDto
{
    public string? CompanySegment { get; init; }
    public string? BusinessDomain { get; init; }
}

/// <summary>One module that would newly become enabled by the pending sector change (plan §2.3).</summary>
public sealed record CompanySectorModulePreviewDto
{
    public required int Id { get; init; }
    public required string LabelFr { get; init; }
}

/// <summary>Response shape for <c>POST /api/company/sector/preview</c> (plan §2.3) — strictly no side effects.</summary>
public sealed record CompanySectorPreviewDto
{
    public required IReadOnlyList<CompanySectorModulePreviewDto> ModulesToEnable { get; init; }

    /// <summary>Codes of the sector data templates that would newly apply (already-applied templates are excluded).</summary>
    public required IReadOnlyList<string> Templates { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
}

/// <summary>Response shape for <c>PUT /api/company/sector</c> (plan §2.3).</summary>
public sealed record CompanySectorApplyResultDto
{
    public string? CompanySegment { get; init; }
    public string? BusinessDomain { get; init; }
    public required IReadOnlyList<int> EnabledModuleIds { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
}
