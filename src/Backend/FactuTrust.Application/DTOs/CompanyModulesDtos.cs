namespace FactuTrust.Application.DTOs;

/// <summary>
/// Response shape for <c>GET /api/company/modules</c> (plan §2.2). Exact camelCase contract — a
/// parallel frontend agent builds a settings UI against this shape.
/// </summary>
public sealed record CompanyModulesDto
{
    /// <summary>Tenant's current subscription plan code (e.g. "Free", "Monthly", "Annual").</summary>
    public required string PlanCode { get; init; }
    public required IReadOnlyList<CompanyModuleItemDto> Modules { get; init; }
}

public sealed record CompanyModuleItemDto
{
    public required int Id { get; init; }
    public required string Code { get; init; }
    public required string LabelFr { get; init; }

    /// <summary>Always enabled, never disablable via <c>PUT /api/company/modules</c>.</summary>
    public required bool IsCore { get; init; }

    /// <summary>Whether the module is currently granted (tenant-wide — plan §2.2 D4, every active user is kept in sync by the PUT handler).</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>Whether the current subscription plan includes this module. A denied module can still be requested — the response's <c>warnings</c> will report the denial instead of a hard failure.</summary>
    public required bool AllowedByPlan { get; init; }

    /// <summary>Module ids this module depends on (selecting it auto-pulls these).</summary>
    public required IReadOnlyList<int> Requires { get; init; }

    /// <summary>Module ids that depend on this one (disabling this one while one of these is enabled is rejected).</summary>
    public required IReadOnlyList<int> RequiredBy { get; init; }

    /// <summary>True when this module is part of the tenant's resolved sector profile's recommended set.</summary>
    public required bool RecommendedForSector { get; init; }
}

/// <summary>Request body for <c>PUT /api/company/modules</c> (plan §2.2).</summary>
public sealed record UpdateCompanyModulesRequestDto
{
    public required IReadOnlyList<int> EnabledModuleIds { get; init; }
}

/// <summary>Response body for <c>PUT /api/company/modules</c> (plan §2.2).</summary>
public sealed record UpdateCompanyModulesResultDto
{
    public required IReadOnlyList<int> EnabledModuleIds { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
}
