using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.Workflows.Engine;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Entities.Studio.Workflows;
using FactuTrust.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Application.Features.Studio.Workflows;

/// <summary>Corps de <c>POST studio/approvals/{id}/decide</c> (4.2e).</summary>
public sealed record ApprovalDecisionRequest(string? Comment);

/// <summary>Badge « Mes approbations » (A20) : nombre de demandes en attente pour l'utilisateur courant.</summary>
public sealed record ApprovalCountDto(int Count);

/// <summary>
/// Élément de la boîte de réception des approbations (format master-B2 §3.4, consommé par 4.4g) :
/// la demande, son instance, le workflow (« — » si la définition a été supprimée), l'entité, un
/// libellé d'enregistrement (premier champ texte, <see langword="null"/> si indisponible) et le nom du
/// lanceur (<c>StartedByName</c>, 4.5a2 / D-44-79, <see langword="null"/> si inconnu).
/// </summary>
public sealed record WorkflowApprovalInboxItemDto(
    WorkflowApprovalDto Approval,
    Guid InstanceId,
    string WorkflowKey,
    string WorkflowName,
    string EntityKey,
    string EntityName,
    Guid RecordId,
    string? RecordLabel,
    Guid? StartedBy,
    DateTime StartedAt,
    string? StartedByName = null);

/// <summary>Approbations en attente de l'utilisateur courant (directes ou via son rôle).</summary>
public sealed record ListMyApprovalsQuery(int Max = 100) : IRequest<Result<IReadOnlyList<WorkflowApprovalInboxItemDto>>>;

/// <summary>Compteur pour le badge (appel fréquent : une seule requête SQL).</summary>
public sealed record CountMyApprovalsQuery : IRequest<Result<ApprovalCountDto>>;

/// <summary>Décision (approuver / refuser) puis reprise inline du workflow via le runner.</summary>
public sealed record DecideApprovalCommand(Guid ApprovalId, bool Approve, string? Comment) : IRequest<Result<WorkflowInstanceDto>>;

public sealed class ListMyApprovalsQueryHandler
    : IRequestHandler<ListMyApprovalsQuery, Result<IReadOnlyList<WorkflowApprovalInboxItemDto>>>
{
    private readonly IStudioWorkflowRepository _workflows;
    private readonly ICustomEntityRepository _entities;
    private readonly ICustomFieldRepository _fields;
    private readonly ICustomRecordRepository _records;
    private readonly ICurrentUser _currentUser;
    private readonly IStudioUserNameResolver _userNames;

    public ListMyApprovalsQueryHandler(
        IStudioWorkflowRepository workflows,
        ICustomEntityRepository entities,
        ICustomFieldRepository fields,
        ICustomRecordRepository records,
        ICurrentUser currentUser,
        IStudioUserNameResolver userNames)
    {
        _workflows = workflows;
        _entities = entities;
        _fields = fields;
        _records = records;
        _currentUser = currentUser;
        _userNames = userNames;
    }

    public async Task<Result<IReadOnlyList<WorkflowApprovalInboxItemDto>>> Handle(
        ListMyApprovalsQuery query, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out var userId, out var err))
            return Result.Failure<IReadOnlyList<WorkflowApprovalInboxItemDto>>(err);
        if (!_currentUser.HasPermission(Permissions.CustomData.RecordsRead))
            return Result.Failure<IReadOnlyList<WorkflowApprovalInboxItemDto>>(
                Error.Unauthorized("Lecture des enregistrements requise."));

        var approvals = await _workflows.ListPendingApprovalsForUserAsync(
            tenantId, userId ?? Guid.Empty, _currentUser.Role?.ToString(), Math.Clamp(query.Max, 1, 200), cancellationToken);

        // Caches par identifiant (revue 4.2e : N+1) — définitions/entités/champs sont partagés entre éléments.
        var definitions = new Dictionary<Guid, StudioWorkflowDefinition?>();
        var entities = new Dictionary<Guid, CustomEntityDefinition?>();
        var labelKeys = new Dictionary<Guid, string?>();
        var items = new List<WorkflowApprovalInboxItemDto>(approvals.Count);
        foreach (var approval in approvals)
        {
            var instance = await _workflows.GetInstanceAsync(tenantId, approval.InstanceId, cancellationToken);
            if (instance is null || instance.IsTerminal)
                continue; // décision en cours ou instance refermée : l'élément n'est plus actionnable

            // Définition possiblement supprimée : clé et nom « — » plutôt qu'un 500.
            if (!definitions.TryGetValue(instance.WorkflowDefinitionId, out var definition))
            {
                definition = await _workflows.GetDefinitionAsync(tenantId, instance.WorkflowDefinitionId, cancellationToken);
                definitions[instance.WorkflowDefinitionId] = definition;
            }
            if (!entities.TryGetValue(instance.EntityDefinitionId, out var entity))
            {
                entity = await _entities.GetByIdAsync(tenantId, instance.EntityDefinitionId, cancellationToken);
                entities[instance.EntityDefinitionId] = entity;
            }

            string? recordLabel = null;
            if (entity is not null)
            {
                var record = await _records.GetAsync(tenantId, entity.Id, instance.RecordId, cancellationToken);
                if (record is not null)
                {
                    // Libellé = premier champ texte (motif des options de relation, CustomFieldFeatures).
                    if (!labelKeys.TryGetValue(entity.Id, out var labelKey))
                    {
                        var fields = await _fields.ListByEntityAsync(tenantId, entity.Id, includeInactive: false, cancellationToken);
                        labelKey = fields
                            .FirstOrDefault(f => f.FieldType is CustomFieldType.Text or CustomFieldType.MultilineText)?.Key;
                        labelKeys[entity.Id] = labelKey;
                    }
                    recordLabel = ExtractDisplay(record.DataJson, labelKey);
                }
            }

            items.Add(new WorkflowApprovalInboxItemDto(
                StudioWorkflowMapping.ToDto(approval),
                instance.Id,
                definition?.Key ?? "—",
                definition?.Name ?? "—",
                entity?.Key ?? "—",
                entity?.DisplayName ?? "—",
                instance.RecordId,
                recordLabel,
                instance.StartedBy,
                instance.StartedAt));
        }

        // 4.5a2 — « Demandé par » (D-44-79) : une seule requête master pour les lanceurs distincts ; absent ⇒ null (D-45-02).
        var starterIds = items.Where(i => i.StartedBy is not null).Select(i => i.StartedBy!.Value).Distinct().ToList();
        if (starterIds.Count > 0)
        {
            var names = await _userNames.GetDisplayNamesAsync(tenantId, starterIds, cancellationToken);
            for (var k = 0; k < items.Count; k++)
            {
                if (items[k].StartedBy is { } starter && names.TryGetValue(starter, out var starterName))
                    items[k] = items[k] with { StartedByName = starterName };
            }
        }

        return Result.Success<IReadOnlyList<WorkflowApprovalInboxItemDto>>(items);
    }

    private static string? ExtractDisplay(string dataJson, string? key)
    {
        if (string.IsNullOrEmpty(key))
            return null;
        try
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(dataJson);
            var value = node?[key];
            return value is null ? null : value.ToString();
        }
        catch (Exception)
        {
            // JsonException (JSON illisible) ou InvalidOperationException (nœud non-objet) : pas de libellé.
            return null;
        }
    }
}

public sealed class CountMyApprovalsQueryHandler : IRequestHandler<CountMyApprovalsQuery, Result<ApprovalCountDto>>
{
    private readonly IStudioWorkflowRepository _workflows;
    private readonly ICurrentUser _currentUser;

    public CountMyApprovalsQueryHandler(IStudioWorkflowRepository workflows, ICurrentUser currentUser)
    {
        _workflows = workflows;
        _currentUser = currentUser;
    }

    public async Task<Result<ApprovalCountDto>> Handle(CountMyApprovalsQuery query, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out var userId, out var err))
            return Result.Failure<ApprovalCountDto>(err);
        if (!_currentUser.HasPermission(Permissions.CustomData.RecordsRead))
            return Result.Failure<ApprovalCountDto>(Error.Unauthorized("Lecture des enregistrements requise."));

        var count = await _workflows.CountPendingApprovalsForUserAsync(
            tenantId, userId ?? Guid.Empty, _currentUser.Role?.ToString(), cancellationToken);
        return Result.Success(new ApprovalCountDto(count));
    }
}

public sealed class DecideApprovalCommandHandler : IRequestHandler<DecideApprovalCommand, Result<WorkflowInstanceDto>>
{
    private const int MaxNotificationBodyLength = 1000;

    private readonly IStudioWorkflowRepository _workflows;
    private readonly IStudioWorkflowRunner _runner;
    private readonly INotificationService _notifications;
    private readonly ICustomEntityRepository _entities;
    private readonly IAuditService _audit;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _time;

    public DecideApprovalCommandHandler(
        IStudioWorkflowRepository workflows,
        IStudioWorkflowRunner runner,
        INotificationService notifications,
        ICustomEntityRepository entities,
        IAuditService audit,
        ICurrentUser currentUser,
        TimeProvider? time = null)
    {
        _workflows = workflows;
        _runner = runner;
        _notifications = notifications;
        _entities = entities;
        _audit = audit;
        _currentUser = currentUser;
        _time = time ?? TimeProvider.System;
    }

    public async Task<Result<WorkflowInstanceDto>> Handle(DecideApprovalCommand command, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out var userId, out var err))
            return Result.Failure<WorkflowInstanceDto>(err);
        if (userId is null)
            return Result.Failure<WorkflowInstanceDto>(Error.Unauthorized("Aucun utilisateur authentifié."));
        if (!_currentUser.HasPermission(Permissions.CustomData.RecordsWrite))
            return Result.Failure<WorkflowInstanceDto>(Error.Unauthorized("Écriture des enregistrements requise."));

        var approval = await _workflows.GetApprovalAsync(tenantId, command.ApprovalId, cancellationToken);
        // 404 (et non 403) : ne pas révéler l'existence d'une approbation assignée à quelqu'un d'autre.
        if (approval is null || !approval.CanBeDecidedBy(userId.Value, _currentUser.Role?.ToString()))
            return Result.Failure<WorkflowInstanceDto>(Error.NotFound("StudioWorkflowApproval", command.ApprovalId));
        if (approval.Status != StudioWorkflowApprovalStatus.Pending)
            return Result.Failure<WorkflowInstanceDto>(Error.Conflict("Cette approbation a déjà été traitée."));
        if (!command.Approve && string.IsNullOrWhiteSpace(command.Comment))
            return Result.Failure<WorkflowInstanceDto>(Error.Validation("comment", "Un commentaire est requis pour refuser."));
        if (command.Comment is { Length: > StudioWorkflowApproval.CommentMaxLength })
            return Result.Failure<WorkflowInstanceDto>(
                Error.Validation("comment", $"Le commentaire dépasse {StudioWorkflowApproval.CommentMaxLength} caractères."));

        var now = _time.GetUtcNow().UtcDateTime;
        var comment = string.IsNullOrWhiteSpace(command.Comment) ? null : command.Comment.Trim();
        approval.Decide(
            command.Approve ? StudioWorkflowApprovalStatus.Approved : StudioWorkflowApprovalStatus.Rejected,
            userId.Value, comment, now);
        try
        {
            await _workflows.UpdateApprovalAsync(approval, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Course perdue sur le RowVersion : quelqu'un a décidé entre-temps (D-41-16, même 409).
            return Result.Failure<WorkflowInstanceDto>(Error.Conflict("Cette approbation a déjà été traitée."));
        }

        var instance = await _workflows.GetInstanceAsync(tenantId, approval.InstanceId, cancellationToken);
        if (instance is null)
            return Result.Failure<WorkflowInstanceDto>(Error.NotFound("StudioWorkflowInstance", approval.InstanceId));

        // Définition (possiblement supprimée) : lien de la notification et clé/nom du DTO.
        var definition = await _workflows.GetDefinitionAsync(tenantId, instance.WorkflowDefinitionId, cancellationToken);

        if (!instance.IsTerminal)
        {
            // La décision est mémorisée au contexte et l'instance devient due sans changer d'étape (D-05 ;
            // contrat de repli JSON documenté sur StudioWorkflowDecisionSync — la décision, elle, est
            // déjà persistée sur l'approbation).
            StudioWorkflowDecisionSync.Apply(
                instance, approval.StepKey, StudioWorkflowEnumNames.ApprovalStatusName(approval.Status), comment, userId.Value, now);
            // Non atomique avec UpdateApprovalAsync (revue 4.2e) : si cette écriture échoue, l'approbation
            // est décidée mais l'instance n'est pas due — le job studio-workflow-resume réconcilie au tick
            // suivant (ListDueAsync), une nouvelle tentative utilisateur renvoie 409.
            await _workflows.UpdateInstanceAsync(instance, cancellationToken);

            if (instance.StartedBy is { } recipient)
                await SafeNotifyDecisionAsync(tenantId, approval, definition, instance, recipient, command.Approve, comment, cancellationToken);

            // Reprise inline ; LeaseBusy ⇒ l'instance est due, le job la reprendra au prochain tick.
            await _runner.ResumeUnderStarterAsync(instance, cancellationToken);
        }

        // Audit sans le commentaire (S-base) : statut + instance seulement.
        await StudioAudit.SafeLogAsync(
            _audit, "Studio.Workflow.ApprovalDecided", "StudioWorkflowApproval", approval.Id, null,
            new { status = StudioWorkflowEnumNames.ApprovalStatusName(approval.Status), instanceId = approval.InstanceId },
            cancellationToken);

        var reloaded = await _workflows.GetInstanceAsync(tenantId, instance.Id, cancellationToken);
        return Result.Success(StudioWorkflowMapping.ToDto(reloaded ?? instance, definition));
    }

    /// <summary>Notification 16 au lanceur — best-effort, tronquée ; ne fait jamais échouer la décision.</summary>
    private async Task SafeNotifyDecisionAsync(
        Guid tenantId, StudioWorkflowApproval approval, StudioWorkflowDefinition? definition,
        StudioWorkflowInstance instance, Guid recipient, bool approve, string? comment, CancellationToken ct)
    {
        string? link = null;
        if (definition is not null)
        {
            var entity = await _entities.GetByIdAsync(tenantId, definition.EntityDefinitionId, ct);
            if (entity is not null)
                link = $"/studio/d/{entity.Key}/{instance.RecordId}/edit";
        }

        try
        {
            await _notifications.CreateAsync(
                tenantId,
                null,
                NotificationType.StudioWorkflowApprovalDecided,
                $"Approbation « {approval.Title} » {(approve ? "accordée" : "refusée")}",
                Truncate(comment, MaxNotificationBodyLength) ?? string.Empty,
                link,
                recipient,
                ct);
        }
        catch (Exception)
        {
            // Notification best-effort : la décision est déjà persistée (pas de log du commentaire).
        }
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is not null && value.Length > maxLength ? value[..maxLength] : value;
}
