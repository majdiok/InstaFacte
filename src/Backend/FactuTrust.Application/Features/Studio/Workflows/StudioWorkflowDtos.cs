using System.Text.Json.Nodes;

namespace FactuTrust.Application.Features.Studio.Workflows;

/// <summary>
/// DTO de l'API de conception des workflows Studio (4.1k). Partiel : complété en 4.1j
/// (<c>WorkflowDefinitionDto</c>, <c>WorkflowInstanceDto</c>…).
/// </summary>
public sealed record SaveWorkflowRequest(
    string Key,
    string Name,
    string? Description,
    string Trigger,
    JsonObject? TriggerConfig,
    JsonObject Steps,
    bool IsActive,
    string? RowVersion);

/// <summary>Résultat de <c>POST entities/{entityId}/workflows/validate</c> (200 même invalide).</summary>
public sealed record WorkflowValidationResultDto(
    bool IsValid,
    IReadOnlyList<WorkflowValidationIssueDto> Errors,
    IReadOnlyList<WorkflowValidationIssueDto> Warnings,
    int StepCount);

/// <summary>Un problème de validation localisé (miroir de <c>WorkflowValidationIssue</c>).</summary>
public sealed record WorkflowValidationIssueDto(string Path, string Message);

/// <summary>Catalogue des types d'étapes (<c>GET workflows/step-catalog</c>).</summary>
public sealed record WorkflowStepCatalogDto(IReadOnlyList<StepCatalogEntryDto> Entries);

/// <summary>Une entrée du catalogue des types d'étapes.</summary>
public sealed record StepCatalogEntryDto(
    string Type,
    string Label,
    string Description,
    IReadOnlyList<StepCatalogPropertyDto> Properties);

/// <summary>Une propriété typée d'un type d'étape.</summary>
public sealed record StepCatalogPropertyDto(
    string Name,
    string Kind,
    bool Required,
    string Help,
    IReadOnlyList<string>? AllowedValues,
    int? Min,
    int? Max);
