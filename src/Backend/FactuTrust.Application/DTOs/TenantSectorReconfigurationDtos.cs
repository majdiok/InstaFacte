using FactuTrust.Application.Common;

namespace FactuTrust.Application.DTOs;

/// <summary>
/// Phase 2 — tenant sector re-configuration request (plan §WP-B7, D6). Posted to both the
/// <c>preview</c> and <c>apply</c> endpoints. All fields optional/additive — a null segment keeps
/// the tenant's current classification; a null domain keeps the current domain, while an empty
/// string clears it.
/// </summary>
public sealed record SectorReconfigurationRequestDto
{
    /// <summary>Target company segment code, or null to keep the current one.</summary>
    public string? CompanySegment { get; init; }

    /// <summary>Target business domain code. Null = keep current; empty string = clear.</summary>
    public string? BusinessDomain { get; init; }

    /// <summary>When true, rewrite each affected user's <c>UserModuleGrant</c> rows from the resolved profile.</summary>
    public bool RecomputeModuleGrants { get; init; } = true;

    /// <summary>When true, apply matching sector data templates additively to the tenant DB.</summary>
    public bool ApplyDataTemplates { get; init; } = true;

    /// <summary>Explicit subset of user ids to touch, or null for all active users of the tenant (capped).</summary>
    public IReadOnlyList<Guid>? UserIds { get; init; }
}

/// <summary>Per-user module diff in a re-configuration preview (plan §WP-B7).</summary>
public sealed record UserModuleDiffDto
{
    public required Guid UserId { get; init; }
    public required IReadOnlyList<int> CurrentEnabledModuleIds { get; init; }
    public required IReadOnlyList<int> TargetEnabledModuleIds { get; init; }
    public required IReadOnlyList<int> ModulesToEnable { get; init; }
    public required IReadOnlyList<int> ModulesToDisable { get; init; }
}

/// <summary>One sector data template as it would apply (plan §WP-B7) — <c>ItemOutcomes</c> come from a dry run.</summary>
public sealed record TemplatePreviewDto
{
    public required string Code { get; init; }
    public required int Version { get; init; }
    public required bool AlreadyApplied { get; init; }
    public required IReadOnlyList<TemplateItemOutcome> ItemOutcomes { get; init; }
}

/// <summary>Informational default setting surfaced from <c>SectorDefaultSettings</c> (plan §WP-B7).</summary>
public sealed record SettingPreviewDto
{
    public required string Key { get; init; }
    public required string Value { get; init; }
}

/// <summary>Preview of a sector re-configuration — strictly no side effects (templates evaluated as dry run).</summary>
public sealed record SectorReconfigurationPreviewDto
{
    public string? CurrentSegment { get; init; }
    public string? CurrentDomain { get; init; }
    public string? TargetSegment { get; init; }
    public string? TargetDomain { get; init; }
    public required IReadOnlyList<UserModuleDiffDto> Users { get; init; }
    public required IReadOnlyList<TemplatePreviewDto> Templates { get; init; }
    public required IReadOnlyList<SettingPreviewDto> Settings { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
}

/// <summary>Outcome of one fault-isolated step in a re-configuration apply (plan §WP-B7).</summary>
public sealed record StepResultDto
{
    public required string Step { get; init; }
    public required bool Success { get; init; }
    public string? Error { get; init; }
}

/// <summary>Result of applying a sector re-configuration — per-step outcomes + the effective change preview.</summary>
public sealed record SectorReconfigurationApplyResultDto
{
    public required IReadOnlyList<StepResultDto> Steps { get; init; }
    public required SectorReconfigurationPreviewDto EffectiveChange { get; init; }
}
