namespace FactuTrust.Application.DTOs;

// Phase 2 — moteur de règles sectorielles en base : backoffice admin CRUD (plan §WP-B5).
// One admin DTO + Create/Update request pair per SectorRules table, plus a full-dump DTO for
// backoffice bootstrap and the seed-from-catalog result DTO.

public sealed record SectorSegmentAdminDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string LabelFr { get; init; }
    public required string DescriptionFr { get; init; }
    public required string IconKey { get; init; }
    public required int SortOrder { get; init; }
    public string? DefaultWarehouseName { get; init; }
    public required bool IsActive { get; init; }
}

public sealed record CreateSectorSegmentRequest
{
    public required string Code { get; init; }
    public required string LabelFr { get; init; }
    public required string DescriptionFr { get; init; }
    public required string IconKey { get; init; }
    public int SortOrder { get; init; }
    public string? DefaultWarehouseName { get; init; }
}

public sealed record UpdateSectorSegmentRequest
{
    public required string LabelFr { get; init; }
    public required string DescriptionFr { get; init; }
    public required string IconKey { get; init; }
    public int SortOrder { get; init; }
    public string? DefaultWarehouseName { get; init; }
}

public sealed record SectorDomainAdminDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string LabelFr { get; init; }
    public required int SortOrder { get; init; }
    public required bool IsActive { get; init; }
}

public sealed record CreateSectorDomainRequest
{
    public required string Code { get; init; }
    public required string LabelFr { get; init; }
    public int SortOrder { get; init; }
}

public sealed record UpdateSectorDomainRequest
{
    public required string LabelFr { get; init; }
    public int SortOrder { get; init; }
}

public sealed record SectorSegmentDomainAdminDto
{
    public required Guid Id { get; init; }
    public required Guid SegmentId { get; init; }
    public required Guid DomainId { get; init; }
    public required int SortOrder { get; init; }
    public required bool IsActive { get; init; }
}

public sealed record CreateSectorSegmentDomainRequest
{
    public required Guid SegmentId { get; init; }
    public required Guid DomainId { get; init; }
    public int SortOrder { get; init; }
}

public sealed record UpdateSectorSegmentDomainRequest
{
    public int SortOrder { get; init; }
}

public sealed record SectorModuleRuleAdminDto
{
    public required Guid Id { get; init; }
    public required string RuleKind { get; init; } // "SegmentBase" | "DomainOverlay"
    public Guid? SegmentId { get; init; }
    public Guid? DomainId { get; init; }
    public required int ModuleId { get; init; }
    public required int SortOrder { get; init; }
    public required bool IsActive { get; init; }
}

public sealed record CreateSectorModuleRuleRequest
{
    public required string RuleKind { get; init; }
    public Guid? SegmentId { get; init; }
    public Guid? DomainId { get; init; }
    public required int ModuleId { get; init; }
    public int SortOrder { get; init; }
}

public sealed record UpdateSectorModuleRuleRequest
{
    public int SortOrder { get; init; }
}

public sealed record SectorModuleDependencyAdminDto
{
    public required Guid Id { get; init; }
    public required int ModuleId { get; init; }
    public required int RequiredModuleId { get; init; }
    public required bool IsActive { get; init; }
}

public sealed record CreateSectorModuleDependencyRequest
{
    public required int ModuleId { get; init; }
    public required int RequiredModuleId { get; init; }
}

public sealed record SectorDefaultSettingAdminDto
{
    public required Guid Id { get; init; }
    public string? SegmentCode { get; init; }
    public string? DomainCode { get; init; }
    public required string SettingKey { get; init; }
    public required string SettingValue { get; init; }
    public required string ValueType { get; init; }
    public required int SortOrder { get; init; }
    public required bool IsActive { get; init; }
}

public sealed record CreateSectorDefaultSettingRequest
{
    public string? SegmentCode { get; init; }
    public string? DomainCode { get; init; }
    public required string SettingKey { get; init; }
    public required string SettingValue { get; init; }
    public string ValueType { get; init; } = "string";
    public int SortOrder { get; init; }
}

public sealed record UpdateSectorDefaultSettingRequest
{
    public required string SettingValue { get; init; }
    public string ValueType { get; init; } = "string";
    public int SortOrder { get; init; }
}

public sealed record SectorDataTemplateItemAdminDto
{
    public required Guid Id { get; init; }
    public required string ItemKind { get; init; }
    public required string PayloadJson { get; init; }
    public required int SortOrder { get; init; }
    public required bool IsActive { get; init; }
}

public sealed record SectorDataTemplateItemRequest
{
    public required string ItemKind { get; init; }
    public required string PayloadJson { get; init; }
    public int SortOrder { get; init; }
}

public sealed record SectorDataTemplateAdminDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public string? SegmentCode { get; init; }
    public string? DomainCode { get; init; }
    public required string LabelFr { get; init; }
    public string? DescriptionFr { get; init; }
    public required int Version { get; init; }
    public required int SortOrder { get; init; }
    public required bool IsActive { get; init; }
    public required IReadOnlyList<SectorDataTemplateItemAdminDto> Items { get; init; }
}

public sealed record CreateSectorDataTemplateRequest
{
    public required string Code { get; init; }
    public string? SegmentCode { get; init; }
    public string? DomainCode { get; init; }
    public required string LabelFr { get; init; }
    public string? DescriptionFr { get; init; }
    public int Version { get; init; } = 1;
    public int SortOrder { get; init; }
    public IReadOnlyList<SectorDataTemplateItemRequest> Items { get; init; } = Array.Empty<SectorDataTemplateItemRequest>();
}

public sealed record UpdateSectorDataTemplateRequest
{
    public required string LabelFr { get; init; }
    public string? DescriptionFr { get; init; }
    public int Version { get; init; } = 1;
    public int SortOrder { get; init; }
    public IReadOnlyList<SectorDataTemplateItemRequest> Items { get; init; } = Array.Empty<SectorDataTemplateItemRequest>();
}

/// <summary>Full dump of every sector-rule table + the current version stamp — backoffice bootstrap.</summary>
public sealed record SectorRuleSetAdminDto
{
    public required long Version { get; init; }
    public required IReadOnlyList<SectorSegmentAdminDto> Segments { get; init; }
    public required IReadOnlyList<SectorDomainAdminDto> Domains { get; init; }
    public required IReadOnlyList<SectorSegmentDomainAdminDto> SegmentDomains { get; init; }
    public required IReadOnlyList<SectorModuleRuleAdminDto> ModuleRules { get; init; }
    public required IReadOnlyList<SectorModuleDependencyAdminDto> ModuleDependencies { get; init; }
    public required IReadOnlyList<SectorDefaultSettingAdminDto> Settings { get; init; }
    public required IReadOnlyList<SectorDataTemplateAdminDto> Templates { get; init; }
}

public sealed record SectorRuleSeedResultDto
{
    public required int Inserted { get; init; }
    public required int Updated { get; init; }
    public required int SkippedExisting { get; init; }
    public required long NewVersion { get; init; }
    public required bool Forced { get; init; }
}


/// <summary>
/// Phase 2 — parity check result (plan §WP-B9): machine-checkable proof that the DB rule set
/// reproduces the static catalog before the <c>UseDbRules</c> flag flips. Surfaces whether the two
/// rule sets match and a flat list of human-readable difference strings.
/// </summary>
public sealed record SectorRuleParityDto
{
    /// <summary>True when no observable divergence was detected between the static and DB snapshots.</summary>
    public required bool IsMatch { get; init; }

    /// <summary>Version stamp of the DB rule set (<c>0</c> when the rule tables are empty/unseeded).</summary>
    public required long DbVersion { get; init; }

    /// <summary>Flat, human-readable difference strings (empty when <see cref="IsMatch"/> is true).</summary>
    public required IReadOnlyList<string> Differences { get; init; }
}
