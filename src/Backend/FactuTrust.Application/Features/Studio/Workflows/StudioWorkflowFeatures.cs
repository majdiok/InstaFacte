using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.Workflows.Engine;
using FactuTrust.Application.Features.Studio.Workflows.Spec;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Entities.Studio.Workflows;
using FactuTrust.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Application.Features.Studio.Workflows;

// ---------------------------------------------------------------------------------------------
// Studio IA 4.1j1 — requêtes / commandes de conception des workflows (sans contrôleur : 4.1k).
// Une seule normalisation partagée (StudioWorkflowSaveSupport) ; StudioWorkflowStepsSpec reste
// l'unique source de vérité pour le contenu des étapes.
// ---------------------------------------------------------------------------------------------

/// <summary>Projection des entités workflow vers les DTO de l'API.</summary>
internal static class StudioWorkflowMapping
{
    public static WorkflowDefinitionDto ToDto(StudioWorkflowDefinition d, int openInstances)
    {
        var steps = ParseObject(d.StepsJson);
        return new WorkflowDefinitionDto(
            d.Id,
            d.EntityDefinitionId,
            d.Key,
            d.Name,
            d.Description,
            StudioWorkflowEnumNames.TriggerName(d.Trigger),
            ParseObject(d.TriggerConfigJson),
            steps,
            (steps["steps"] as JsonArray)?.Count ?? 0,
            d.Version,
            d.IsActive,
            openInstances,
            d.CreatedAt,
            d.UpdatedAt,
            RowVersionOf(d.RowVersion));
    }

    public static WorkflowInstanceDto ToDto(StudioWorkflowInstance i, StudioWorkflowDefinition? d) =>
        new(
            i.Id,
            i.WorkflowDefinitionId,
            d?.Key,
            d?.Name,
            i.DefinitionVersion,
            i.EntityDefinitionId,
            i.RecordId,
            StudioWorkflowEnumNames.TriggerName(i.TriggerKind),
            StudioWorkflowEnumNames.InstanceStatusName(i.Status),
            i.CurrentStepIndex,
            i.CurrentStepKey,
            i.DueAt,
            i.StartedBy,
            i.StartedAt,
            i.CompletedAt,
            i.Depth,
            i.OriginInstanceId,
            i.Error);

    public static WorkflowStepRunDto ToDto(StudioWorkflowStepRun r) =>
        new(
            r.StepIndex,
            r.StepKey,
            r.StepType,
            StudioWorkflowEnumNames.StepRunStatusName(r.Status),
            r.Outcome,
            TryParseObject(r.ResultJson),
            r.Error,
            r.StartedAt,
            r.FinishedAt);

    public static WorkflowApprovalDto ToDto(StudioWorkflowApproval a) =>
        new(
            a.Id,
            a.InstanceId,
            a.StepKey,
            a.AssigneeUserId,
            a.AssigneeRole,
            a.Title,
            a.Message,
            StudioWorkflowEnumNames.ApprovalStatusName(a.Status),
            a.DecidedBy,
            a.DecidedAt,
            a.Comment,
            a.DueAt,
            a.CreatedAt,
            RowVersionOf(a.RowVersion));

    /// <summary>Reparse une colonne JSON en objet ; objet vide si absente, illisible ou d'une autre forme.</summary>
    public static JsonObject ParseObject(string? json) => TryParseObject(json) ?? new JsonObject();

    private static JsonObject? TryParseObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    private static string RowVersionOf(byte[]? rowVersion) => Convert.ToBase64String(rowVersion ?? []);
}

/// <summary>Corps d'enregistrement normalisé (clé / nom / description nettoyés, déclencheur parsé, JSON sérialisés).</summary>
internal sealed record NormalizedWorkflowSave(
    string Key,
    string Name,
    string? Description,
    StudioWorkflowTriggerKind Trigger,
    string TriggerConfigJson,
    string StepsJson,
    bool IsActive);

/// <summary>
/// Aide commune à Create / Update / Validate : normalisation d'un <see cref="SaveWorkflowRequest"/>,
/// validation des étapes via <see cref="StudioWorkflowStepsSpec"/>, conversion en <see cref="Error"/>
/// et lecture du jeton de concurrence.
/// </summary>
internal static class StudioWorkflowSaveSupport
{
    public const string KeyShapeMessage =
        "La clé doit commencer par une lettre minuscule et ne contenir que des minuscules, chiffres et « _ » (2 à 64 caractères).";

    public const string RowVersionRequiredMessage = "Le jeton de concurrence (rowVersion) est obligatoire pour mettre à jour un workflow.";
    public const string RowVersionMalformedMessage = "Le jeton de concurrence (rowVersion) est mal formé.";
    public const string ConcurrencyConflictMessage = "Le workflow a été modifié entre-temps. Rechargez-le avant de réessayer.";
    public const string KeyImmutableMessage = "La clé d'un workflow ne peut pas être modifiée.";

    /// <summary>Normalisation (§1.4-A) : première erreur renvoyée, ordre fixe.</summary>
    public static Result<NormalizedWorkflowSave> Normalize(SaveWorkflowRequest request)
    {
        var issues = NormalizeAsIssues(request, out var normalized);
        if (normalized is null)
        {
            var first = issues[0];
            return Result.Failure<NormalizedWorkflowSave>(Error.Validation(first.Path, first.Message));
        }
        return Result.Success(normalized);
    }

    /// <summary>
    /// Mêmes règles que <see cref="Normalize"/>, exprimées en liste d'issues (pour la validation à blanc).
    /// <paramref name="normalized"/> est null dès qu'une règle bloquante échoue.
    /// </summary>
    public static IReadOnlyList<WorkflowValidationIssue> NormalizeAsIssues(SaveWorkflowRequest request, out NormalizedWorkflowSave? normalized)
    {
        normalized = null;
        var issues = new List<WorkflowValidationIssue>();
        if (request is null)
        {
            issues.Add(new WorkflowValidationIssue("request", "Le corps de la requête est obligatoire."));
            return issues;
        }

        var key = request.Key?.Trim() ?? string.Empty;
        if (!StudioKey.IsValidShape(key))
            issues.Add(new WorkflowValidationIssue("key", KeyShapeMessage));

        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
            issues.Add(new WorkflowValidationIssue("name", "Le nom du workflow est obligatoire."));
        else if (name.Length > StudioWorkflowDefinition.NameMaxLength)
            issues.Add(new WorkflowValidationIssue("name", $"Le nom du workflow ne peut pas dépasser {StudioWorkflowDefinition.NameMaxLength} caractères."));

        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        if (description is not null && description.Length > StudioWorkflowDefinition.DescriptionMaxLength)
            issues.Add(new WorkflowValidationIssue("description", $"La description ne peut pas dépasser {StudioWorkflowDefinition.DescriptionMaxLength} caractères."));

        if (!StudioWorkflowEnumNames.TryParseTrigger(request.Trigger?.Trim(), out var trigger))
            issues.Add(new WorkflowValidationIssue("trigger",
                $"Déclencheur inconnu : « {request.Trigger} ». Valeurs acceptées : on_create, on_update, field_changed, manual."));

        var triggerConfigJson = (request.TriggerConfig ?? new JsonObject()).ToJsonString();
        if (Encoding.UTF8.GetByteCount(triggerConfigJson) > StudioWorkflowStepsSpec.MaxTriggerConfigBytes)
            issues.Add(new WorkflowValidationIssue("triggerConfig", "La configuration du déclencheur dépasse 2 Ko."));

        if (request.Steps is null)
            issues.Add(new WorkflowValidationIssue("steps", "Le corps « steps » est obligatoire."));

        if (issues.Count > 0)
            return issues;

        normalized = new NormalizedWorkflowSave(key, name, description, trigger, triggerConfigJson, request.Steps!.ToJsonString(), request.IsActive);
        return issues;
    }

    /// <summary>
    /// Validation des étapes (§1.4-B) : résout les tables cibles des étapes <c>create_record</c>
    /// (inconnue, inactive ou jonction ⇒ null) puis délègue à <see cref="StudioWorkflowStepsSpec.Validate"/>.
    /// </summary>
    public static async Task<WorkflowValidationOutcome> ValidateAsync(
        NormalizedWorkflowSave save,
        CustomEntityDefinition entity,
        IReadOnlyList<CustomFieldDefinition> fields,
        ICustomEntityRepository entities,
        ICustomFieldRepository fieldsRepo,
        Guid tenantId,
        CancellationToken ct)
    {
        var resolved = new Dictionary<string, IReadOnlyList<CustomFieldDefinition>?>(StringComparer.Ordinal);

        var parsed = StudioWorkflowStepsSpec.Parse(save.StepsJson);
        if (parsed.IsSuccess)
        {
            foreach (var key in StudioWorkflowStepsSpec.ReferencedEntityKeys(parsed.Value))
            {
                if (resolved.ContainsKey(key))
                    continue;
                var target = await entities.GetByKeyAsync(tenantId, key, ct);
                if (target is null || !target.IsActive || target.Kind == CustomEntityKind.Junction)
                    resolved[key] = null;
                else
                    resolved[key] = await fieldsRepo.ListByEntityAsync(tenantId, target.Id, includeInactive: false, ct);
            }
        }

        return StudioWorkflowStepsSpec.Validate(
            save.StepsJson,
            save.Trigger,
            save.TriggerConfigJson,
            entity,
            fields,
            StudioBridgeActionCatalog.Resolve,
            key => resolved.TryGetValue(key, out var f) ? f : null);
    }

    /// <summary>Première issue ⇒ <c>Validation.&lt;chemin&gt;</c> ; suffixe si d'autres erreurs existent (D-41-03).</summary>
    public static Error ToError(WorkflowValidationOutcome outcome)
    {
        var first = outcome.Errors[0];
        var n = outcome.Errors.Count;
        return Error.Validation(
            first.Path,
            n == 1 ? first.Message : $"{first.Message} (+{n - 1} autre(s) erreur(s) — utilisez la validation pour la liste complète.)");
    }

    /// <summary>Jeton de concurrence (§1.4-D) : obligatoire et décodable en base64.</summary>
    public static Result<byte[]> ParseRowVersion(string? rowVersion)
    {
        if (string.IsNullOrWhiteSpace(rowVersion))
            return Result.Failure<byte[]>(Error.Validation("rowVersion", RowVersionRequiredMessage));
        try
        {
            return Result.Success(Convert.FromBase64String(rowVersion));
        }
        catch (FormatException)
        {
            return Result.Failure<byte[]>(Error.Validation("rowVersion", RowVersionMalformedMessage));
        }
    }
}

// ---- List ----

public sealed record ListWorkflowsQuery(Guid EntityId) : IRequest<Result<IReadOnlyList<WorkflowDefinitionDto>>>;

public sealed class ListWorkflowsQueryHandler : IRequestHandler<ListWorkflowsQuery, Result<IReadOnlyList<WorkflowDefinitionDto>>>
{
    private readonly IStudioWorkflowRepository _workflows;
    private readonly ICustomEntityRepository _entities;
    private readonly ICurrentUser _currentUser;

    public ListWorkflowsQueryHandler(IStudioWorkflowRepository workflows, ICustomEntityRepository entities, ICurrentUser currentUser)
    {
        _workflows = workflows;
        _entities = entities;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<WorkflowDefinitionDto>>> Handle(ListWorkflowsQuery query, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out _, out var err))
            return Result.Failure<IReadOnlyList<WorkflowDefinitionDto>>(err);

        var entity = await _entities.GetByIdAsync(tenantId, query.EntityId, cancellationToken);
        if (entity is null)
            return Result.Failure<IReadOnlyList<WorkflowDefinitionDto>>(Error.NotFound("CustomEntity", query.EntityId));

        var definitions = await _workflows.ListByEntityAsync(tenantId, entity.Id, includeInactive: true, cancellationToken);
        var result = new List<WorkflowDefinitionDto>(definitions.Count);
        foreach (var definition in definitions)
        {
            // Borné par le quota de workflows par table (≤ 20 requêtes).
            var open = await _workflows.CountOpenInstancesForDefinitionAsync(tenantId, definition.Id, cancellationToken);
            result.Add(StudioWorkflowMapping.ToDto(definition, open));
        }

        return Result.Success<IReadOnlyList<WorkflowDefinitionDto>>(result);
    }
}

// ---- Get ----

public sealed record GetWorkflowQuery(Guid Id) : IRequest<Result<WorkflowDefinitionDto>>;

public sealed class GetWorkflowQueryHandler : IRequestHandler<GetWorkflowQuery, Result<WorkflowDefinitionDto>>
{
    private readonly IStudioWorkflowRepository _workflows;
    private readonly ICurrentUser _currentUser;

    public GetWorkflowQueryHandler(IStudioWorkflowRepository workflows, ICurrentUser currentUser)
    {
        _workflows = workflows;
        _currentUser = currentUser;
    }

    public async Task<Result<WorkflowDefinitionDto>> Handle(GetWorkflowQuery query, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out _, out var err))
            return Result.Failure<WorkflowDefinitionDto>(err);

        var definition = await _workflows.GetDefinitionAsync(tenantId, query.Id, cancellationToken);
        if (definition is null)
            return Result.Failure<WorkflowDefinitionDto>(Error.NotFound("StudioWorkflowDefinition", query.Id));

        var open = await _workflows.CountOpenInstancesForDefinitionAsync(tenantId, definition.Id, cancellationToken);
        return Result.Success(StudioWorkflowMapping.ToDto(definition, open));
    }
}

// ---- Create ----

public sealed record CreateWorkflowCommand(Guid EntityId, SaveWorkflowRequest Request) : IRequest<Result<WorkflowDefinitionDto>>;

public sealed class CreateWorkflowCommandHandler : IRequestHandler<CreateWorkflowCommand, Result<WorkflowDefinitionDto>>
{
    private readonly IStudioWorkflowRepository _workflows;
    private readonly ICustomEntityRepository _entities;
    private readonly ICustomFieldRepository _fields;
    private readonly IStudioQuotaService _quota;
    private readonly IAuditService _audit;
    private readonly ICurrentUser _currentUser;

    public CreateWorkflowCommandHandler(
        IStudioWorkflowRepository workflows,
        ICustomEntityRepository entities,
        ICustomFieldRepository fields,
        IStudioQuotaService quota,
        IAuditService audit,
        ICurrentUser currentUser)
    {
        _workflows = workflows;
        _entities = entities;
        _fields = fields;
        _quota = quota;
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<Result<WorkflowDefinitionDto>> Handle(CreateWorkflowCommand command, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out var userId, out var err))
            return Result.Failure<WorkflowDefinitionDto>(err);

        var normalized = StudioWorkflowSaveSupport.Normalize(command.Request);
        if (normalized.IsFailure)
            return Result.Failure<WorkflowDefinitionDto>(normalized.Error);
        var save = normalized.Value;

        var entity = await _entities.GetByIdAsync(tenantId, command.EntityId, cancellationToken);
        if (entity is null)
            return Result.Failure<WorkflowDefinitionDto>(Error.NotFound("CustomEntity", command.EntityId));

        if (await _workflows.GetDefinitionByKeyAsync(tenantId, entity.Id, save.Key, cancellationToken) is not null)
            return Result.Failure<WorkflowDefinitionDto>(
                Error.Conflict($"Un workflow avec la clé « {save.Key} » existe déjà pour cette table."));

        var count = await _workflows.CountByEntityAsync(tenantId, entity.Id, cancellationToken);
        var quota = await _quota.EnsureUnderLimitAsync(
            tenantId, StudioQuotas.MaxWorkflowsKey, count, StudioQuotas.MaxWorkflowsFallback, "workflows par table", cancellationToken);
        if (quota.IsFailure)
            return Result.Failure<WorkflowDefinitionDto>(quota.Error);

        var fields = await _fields.ListByEntityAsync(tenantId, entity.Id, includeInactive: false, cancellationToken);
        var outcome = await StudioWorkflowSaveSupport.ValidateAsync(save, entity, fields, _entities, _fields, tenantId, cancellationToken);
        if (!outcome.IsValid)
            return Result.Failure<WorkflowDefinitionDto>(StudioWorkflowSaveSupport.ToError(outcome));

        var definition = StudioWorkflowDefinition.Create(
            tenantId, entity.Id, save.Key, save.Name, save.Description, save.Trigger, save.TriggerConfigJson, save.StepsJson, save.IsActive, userId);
        await _workflows.AddDefinitionAsync(definition, cancellationToken);

        await StudioAudit.SafeLogAsync(
            _audit, "Studio.Workflow.Created", "StudioWorkflowDefinition", definition.Id,
            null,
            new
            {
                definition.Key,
                definition.Name,
                Trigger = StudioWorkflowEnumNames.TriggerName(definition.Trigger),
                definition.IsActive,
                definition.Version,
                StepCount = outcome.Spec?.Steps.Count ?? 0
            },
            cancellationToken);

        return Result.Success(StudioWorkflowMapping.ToDto(definition, 0));
    }
}

// ---- Update ----

public sealed record UpdateWorkflowCommand(Guid Id, SaveWorkflowRequest Request) : IRequest<Result<WorkflowDefinitionDto>>;

public sealed class UpdateWorkflowCommandHandler : IRequestHandler<UpdateWorkflowCommand, Result<WorkflowDefinitionDto>>
{
    private readonly IStudioWorkflowRepository _workflows;
    private readonly ICustomEntityRepository _entities;
    private readonly ICustomFieldRepository _fields;
    private readonly IAuditService _audit;
    private readonly ICurrentUser _currentUser;

    public UpdateWorkflowCommandHandler(
        IStudioWorkflowRepository workflows,
        ICustomEntityRepository entities,
        ICustomFieldRepository fields,
        IAuditService audit,
        ICurrentUser currentUser)
    {
        _workflows = workflows;
        _entities = entities;
        _fields = fields;
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<Result<WorkflowDefinitionDto>> Handle(UpdateWorkflowCommand command, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out var userId, out var err))
            return Result.Failure<WorkflowDefinitionDto>(err);

        var definition = await _workflows.GetDefinitionAsync(tenantId, command.Id, cancellationToken);
        if (definition is null)
            return Result.Failure<WorkflowDefinitionDto>(Error.NotFound("StudioWorkflowDefinition", command.Id));

        // Concurrence optimiste : jeton obligatoire, pré-contrôle déterministe puis contrôle EF à l'écriture.
        var rowVersion = StudioWorkflowSaveSupport.ParseRowVersion(command.Request?.RowVersion);
        if (rowVersion.IsFailure)
            return Result.Failure<WorkflowDefinitionDto>(rowVersion.Error);
        var expected = rowVersion.Value;
        if (definition.RowVersion is { Length: > 0 } current && !current.AsSpan().SequenceEqual(expected))
            return Result.Failure<WorkflowDefinitionDto>(Error.Conflict(StudioWorkflowSaveSupport.ConcurrencyConflictMessage));

        var normalized = StudioWorkflowSaveSupport.Normalize(command.Request!);
        if (normalized.IsFailure)
            return Result.Failure<WorkflowDefinitionDto>(normalized.Error);
        var save = normalized.Value;

        if (!string.Equals(save.Key, definition.Key, StringComparison.Ordinal))
            return Result.Failure<WorkflowDefinitionDto>(Error.Validation("key", StudioWorkflowSaveSupport.KeyImmutableMessage));

        var entity = await _entities.GetByIdAsync(tenantId, definition.EntityDefinitionId, cancellationToken);
        if (entity is null)
            return Result.Failure<WorkflowDefinitionDto>(Error.NotFound("CustomEntity", definition.EntityDefinitionId));

        var fields = await _fields.ListByEntityAsync(tenantId, entity.Id, includeInactive: false, cancellationToken);
        var outcome = await StudioWorkflowSaveSupport.ValidateAsync(save, entity, fields, _entities, _fields, tenantId, cancellationToken);
        if (!outcome.IsValid)
            return Result.Failure<WorkflowDefinitionDto>(StudioWorkflowSaveSupport.ToError(outcome));

        var old = new
        {
            definition.Version,
            Trigger = StudioWorkflowEnumNames.TriggerName(definition.Trigger),
            definition.IsActive,
            definition.Name
        };

        definition.Update(save.Name, save.Description, save.Trigger, save.TriggerConfigJson, save.StepsJson, userId);
        if (definition.IsActive != save.IsActive)
            definition.SetActive(save.IsActive, userId);

        try
        {
            await _workflows.UpdateDefinitionWithConcurrencyAsync(definition, expected, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<WorkflowDefinitionDto>(Error.Conflict(StudioWorkflowSaveSupport.ConcurrencyConflictMessage));
        }

        await StudioAudit.SafeLogAsync(
            _audit, "Studio.Workflow.Updated", "StudioWorkflowDefinition", definition.Id,
            old,
            new
            {
                definition.Version,
                Trigger = StudioWorkflowEnumNames.TriggerName(definition.Trigger),
                definition.IsActive,
                definition.Name,
                StepCount = outcome.Spec?.Steps.Count ?? 0
            },
            cancellationToken);

        var open = await _workflows.CountOpenInstancesForDefinitionAsync(tenantId, definition.Id, cancellationToken);
        return Result.Success(StudioWorkflowMapping.ToDto(definition, open));
    }
}

// ---- Toggle ----

public sealed record ToggleWorkflowCommand(Guid Id, bool IsActive) : IRequest<Result<WorkflowDefinitionDto>>;

public sealed class ToggleWorkflowCommandHandler : IRequestHandler<ToggleWorkflowCommand, Result<WorkflowDefinitionDto>>
{
    private readonly IStudioWorkflowRepository _workflows;
    private readonly IAuditService _audit;
    private readonly ICurrentUser _currentUser;

    public ToggleWorkflowCommandHandler(IStudioWorkflowRepository workflows, IAuditService audit, ICurrentUser currentUser)
    {
        _workflows = workflows;
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<Result<WorkflowDefinitionDto>> Handle(ToggleWorkflowCommand command, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out var userId, out var err))
            return Result.Failure<WorkflowDefinitionDto>(err);

        var definition = await _workflows.GetDefinitionAsync(tenantId, command.Id, cancellationToken);
        if (definition is null)
            return Result.Failure<WorkflowDefinitionDto>(Error.NotFound("StudioWorkflowDefinition", command.Id));

        var open = await _workflows.CountOpenInstancesForDefinitionAsync(tenantId, definition.Id, cancellationToken);

        // Idempotent : même état ⇒ aucune écriture ni audit.
        if (definition.IsActive == command.IsActive)
            return Result.Success(StudioWorkflowMapping.ToDto(definition, open));

        definition.SetActive(command.IsActive, userId);
        // Sans jeton (D-41-05) : la bascule ne porte que sur IsActive.
        await _workflows.UpdateDefinitionWithConcurrencyAsync(definition, null, cancellationToken);

        await StudioAudit.SafeLogAsync(
            _audit, "Studio.Workflow.Toggled", "StudioWorkflowDefinition", definition.Id,
            new { IsActive = !command.IsActive },
            new { definition.IsActive },
            cancellationToken);

        return Result.Success(StudioWorkflowMapping.ToDto(definition, open));
    }
}

// ---- Delete (soft) ----

public sealed record DeleteWorkflowCommand(Guid Id) : IRequest<Result<WorkflowDeletionResultDto>>;

public sealed class DeleteWorkflowCommandHandler : IRequestHandler<DeleteWorkflowCommand, Result<WorkflowDeletionResultDto>>
{
    private readonly IStudioWorkflowRepository _workflows;
    private readonly IStudioWorkflowEngine _engine;
    private readonly IAuditService _audit;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<DeleteWorkflowCommandHandler> _logger;

    public DeleteWorkflowCommandHandler(
        IStudioWorkflowRepository workflows,
        IStudioWorkflowEngine engine,
        IAuditService audit,
        ICurrentUser currentUser,
        ILogger<DeleteWorkflowCommandHandler> logger)
    {
        _workflows = workflows;
        _engine = engine;
        _audit = audit;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<WorkflowDeletionResultDto>> Handle(DeleteWorkflowCommand command, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out var userId, out var err))
            return Result.Failure<WorkflowDeletionResultDto>(err);

        // Le filtre IsDeleted du contexte fait qu'une seconde suppression répond 404.
        var definition = await _workflows.GetDefinitionAsync(tenantId, command.Id, cancellationToken);
        if (definition is null)
            return Result.Failure<WorkflowDeletionResultDto>(Error.NotFound("StudioWorkflowDefinition", command.Id));

        var old = new { definition.Key, definition.Name, definition.Version, definition.IsActive };

        var openInstances = await _workflows.ListOpenInstancesForDefinitionAsync(tenantId, definition.Id, cancellationToken);

        definition.SoftDelete(userId);
        await _workflows.UpdateDefinitionWithConcurrencyAsync(definition, null, cancellationToken);

        var cancelled = 0;
        foreach (var instance in openInstances)
        {
            try
            {
                await _engine.CancelAsync(instance, "Workflow supprimé", userId, cancellationToken);
                cancelled++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // La reprise différée annulera de toute façon une instance dont la définition a disparu.
                _logger.LogWarning(ex,
                    "Studio workflow {WorkflowId} : annulation de l'instance {InstanceId} impossible lors de la suppression",
                    definition.Id, instance.Id);
            }
        }

        await StudioAudit.SafeLogAsync(
            _audit, "Studio.Workflow.Deleted", "StudioWorkflowDefinition", definition.Id,
            old,
            new { CancelledInstances = cancelled },
            cancellationToken);

        return Result.Success(new WorkflowDeletionResultDto(cancelled));
    }
}
