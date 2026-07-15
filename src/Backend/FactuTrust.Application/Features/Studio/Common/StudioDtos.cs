using System.Text.Json.Nodes;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.Studio.Common;

// ---- Entities (custom "tables") ----

public sealed record CustomEntityDto(
    Guid Id,
    string Key,
    string DisplayName,
    string DisplayNamePlural,
    string? Icon,
    string? Description,
    bool IsActive,
    int FieldCount,
    Guid? SystemId,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateCustomEntityRequest(
    string Key,
    string DisplayName,
    string? DisplayNamePlural,
    string? Icon,
    string? Description,
    Guid? SystemId = null);

public sealed record UpdateCustomEntityRequest(
    string DisplayName,
    string? DisplayNamePlural,
    string? Icon,
    string? Description,
    bool IsActive);

// ---- Fields ----

public sealed record CustomFieldDto(
    Guid Id,
    string Key,
    string Label,
    CustomFieldType FieldType,
    bool IsRequired,
    bool IsUnique,
    int SortOrder,
    FieldValidationRules? Rules,
    IReadOnlyList<SelectOptionDto>? Options,
    RelationRefDto? Relation,
    bool IsActive,
    JsonNode? Config = null);

public sealed record CreateCustomFieldRequest(
    string Key,
    string Label,
    CustomFieldType FieldType,
    bool IsRequired,
    bool IsUnique,
    FieldValidationRules? Rules,
    IReadOnlyList<SelectOptionDto>? Options,
    RelationRefDto? Relation,
    Dictionary<string, JsonNode?>? Config = null);

public sealed record UpdateCustomFieldRequest(
    string Label,
    bool IsRequired,
    bool IsUnique,
    FieldValidationRules? Rules,
    IReadOnlyList<SelectOptionDto>? Options,
    RelationRefDto? Relation,
    bool IsActive,
    Dictionary<string, JsonNode?>? Config = null);

public sealed record ReorderCustomFieldsRequest(IReadOnlyList<Guid> OrderedFieldIds);

/// <summary>The entity, its active fields, and the default form layout — used by the runtime form/table renderer.</summary>
public sealed record CustomEntitySchemaDto(
    CustomEntityDto Entity,
    IReadOnlyList<CustomFieldDto> Fields,
    FormLayout Form);

// ---- Records ----

public sealed record CustomRecordDto(
    Guid Id,
    JsonNode? Data,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? RowVersion);

public sealed record SaveCustomRecordRequest(Dictionary<string, JsonNode?> Data, string? RowVersion = null);

// ---- Navigation ----

/// <summary>Hierarchical sidebar node: system group or standalone entity.</summary>
public sealed record StudioNavNodeDto(
    string Kind,
    string Key,
    string Label,
    string? Icon,
    string? Route,
    IReadOnlyList<StudioNavNodeDto>? Children);

/// <summary>A custom entity surfaced as a sidebar entry for end users (legacy flat shape).</summary>
public sealed record StudioNavItemDto(string Key, string Label, string? Icon);

// ---- Systems (multi-table apps) ----

public sealed record CustomSystemDto(
    Guid Id,
    string Key,
    string DisplayName,
    string? Icon,
    string? Description,
    IReadOnlyList<string>? OnboardingSteps,
    bool IsActive,
    int EntityCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CustomSystemDetailDto(
    CustomSystemDto System,
    IReadOnlyList<CustomEntityDto> Entities);

public sealed record CreateCustomSystemRequest(
    string Key,
    string DisplayName,
    string? Icon,
    string? Description,
    IReadOnlyList<string>? OnboardingSteps);

public sealed record UpdateCustomSystemRequest(
    string DisplayName,
    string? Icon,
    string? Description,
    IReadOnlyList<string>? OnboardingSteps,
    bool IsActive);
