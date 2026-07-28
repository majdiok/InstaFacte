using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Studio.Ai;

/// <summary>
/// Exécute un plan Studio IA confirmé (routage par <see cref="StudioAiPlanKind"/> vers les
/// orchestrateurs Infrastructure). Ne contient AUCUNE logique métier : tout passe par les
/// commandes CQRS Studio existantes (validation/quotas/permissions/audit inclus).
/// </summary>
public interface IStudioAiPlanExecutor
{
    Task<(bool Success, string? Error, object? Payload)> ExecuteAsync(
        StudioAiBuildPlan plan, IStudioBuildProgress? progress, CancellationToken cancellationToken);
}

public sealed record StudioAiPlanDto(
    Guid Id,
    string Kind,
    string Status,
    string SummaryJson,
    string? ResultJson,
    string? ErrorMessage,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? ExecutedAt);

public static class StudioAiPlanDefaults
{
    /// <summary>Durée de vie d'un plan en attente : au-delà, la confirmation est refusée.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(60);

    public static StudioAiPlanDto ToDto(StudioAiBuildPlan plan)
    {
        // Un plan Pending dont l'échéance est passée est présenté « Expired » sans écriture
        // (l'état persistant n'est corrigé qu'au moment d'une confirmation/annulation).
        var status = plan.IsExpired(DateTime.UtcNow) ? StudioAiPlanStatus.Expired : plan.Status;
        return new StudioAiPlanDto(
            plan.Id, plan.Kind.ToString(), status.ToString(), plan.SummaryJson,
            plan.ResultJson, plan.ErrorMessage, plan.CreatedAt, plan.ExpiresAt, plan.ExecutedAt);
    }

    /// <summary>Permission de design requise pour confirmer/annuler un plan selon sa nature.</summary>
    public static string RequiredPermission(StudioAiPlanKind kind) => kind switch
    {
        StudioAiPlanKind.View => Permissions.Studio.DesignForms,
        _ => Permissions.Studio.DesignEntities
    };
}

// ---- Create ----

public sealed record CreateStudioAiPlanCommand(StudioAiPlanKind Kind, string SpecJson, string SummaryJson)
    : IRequest<Result<StudioAiPlanDto>>;

public sealed class CreateStudioAiPlanCommandHandler
    : IRequestHandler<CreateStudioAiPlanCommand, Result<StudioAiPlanDto>>
{
    private readonly IStudioAiBuildPlanRepository _plans;
    private readonly IAuditService _audit;
    private readonly ICurrentUser _currentUser;

    public CreateStudioAiPlanCommandHandler(
        IStudioAiBuildPlanRepository plans, IAuditService audit, ICurrentUser currentUser)
    {
        _plans = plans;
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<Result<StudioAiPlanDto>> Handle(CreateStudioAiPlanCommand command, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out var userId, out var err))
            return Result.Failure<StudioAiPlanDto>(err);

        if (string.IsNullOrWhiteSpace(command.SpecJson))
            return Result.Failure<StudioAiPlanDto>(Error.Validation("specJson", "La spécification du plan est vide."));
        if (string.IsNullOrWhiteSpace(command.SummaryJson))
            return Result.Failure<StudioAiPlanDto>(Error.Validation("summaryJson", "L'aperçu du plan est vide."));

        if (!_currentUser.HasPermission(StudioAiPlanDefaults.RequiredPermission(command.Kind)))
            return Result.Failure<StudioAiPlanDto>(Error.Unauthorized("Permission de conception Studio requise."));

        var plan = StudioAiBuildPlan.Create(
            tenantId, command.Kind, command.SpecJson, command.SummaryJson, userId, StudioAiPlanDefaults.Lifetime);

        await _plans.AddAsync(plan, cancellationToken);
        await StudioAudit.SafeLogAsync(_audit, "Studio.AiPlan.Created", "StudioAiBuildPlan", plan.Id,
            null, new { plan.Kind, plan.ExpiresAt }, cancellationToken);

        return Result.Success(StudioAiPlanDefaults.ToDto(plan));
    }
}

// ---- Get ----

public sealed record GetStudioAiPlanQuery(Guid Id) : IRequest<Result<StudioAiPlanDto>>;

public sealed class GetStudioAiPlanQueryHandler : IRequestHandler<GetStudioAiPlanQuery, Result<StudioAiPlanDto>>
{
    private readonly IStudioAiBuildPlanRepository _plans;
    private readonly ICurrentUser _currentUser;

    public GetStudioAiPlanQueryHandler(IStudioAiBuildPlanRepository plans, ICurrentUser currentUser)
    {
        _plans = plans;
        _currentUser = currentUser;
    }

    public async Task<Result<StudioAiPlanDto>> Handle(GetStudioAiPlanQuery request, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out _, out var err))
            return Result.Failure<StudioAiPlanDto>(err);

        var plan = await _plans.GetByIdAsync(tenantId, request.Id, cancellationToken);
        return plan is null
            ? Result.Failure<StudioAiPlanDto>(Error.NotFound("StudioAiBuildPlan", request.Id))
            : Result.Success(StudioAiPlanDefaults.ToDto(plan));
    }
}

// ---- Confirm (exécution déterministe, jamais déclenchée par le LLM) ----

public sealed record ConfirmStudioAiPlanCommand(Guid Id, IStudioBuildProgress? Progress = null)
    : IRequest<Result<StudioAiPlanDto>>;

public sealed class ConfirmStudioAiPlanCommandHandler
    : IRequestHandler<ConfirmStudioAiPlanCommand, Result<StudioAiPlanDto>>
{
    private readonly IStudioAiBuildPlanRepository _plans;
    private readonly IStudioAiPlanExecutor _executor;
    private readonly IAuditService _audit;
    private readonly ICurrentUser _currentUser;

    public ConfirmStudioAiPlanCommandHandler(
        IStudioAiBuildPlanRepository plans,
        IStudioAiPlanExecutor executor,
        IAuditService audit,
        ICurrentUser currentUser)
    {
        _plans = plans;
        _executor = executor;
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<Result<StudioAiPlanDto>> Handle(ConfirmStudioAiPlanCommand command, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out _, out var err))
            return Result.Failure<StudioAiPlanDto>(err);

        var plan = await _plans.GetByIdAsync(tenantId, command.Id, cancellationToken);
        if (plan is null)
            return Result.Failure<StudioAiPlanDto>(Error.NotFound("StudioAiBuildPlan", command.Id));

        // Défense en profondeur : la politique du contrôleur exige déjà une permission de design.
        if (!_currentUser.HasPermission(StudioAiPlanDefaults.RequiredPermission(plan.Kind)))
            return Result.Failure<StudioAiPlanDto>(Error.Unauthorized("Permission de conception Studio requise."));

        if (plan.IsExpired(DateTime.UtcNow))
        {
            plan.MarkExpired();
            await _plans.TryUpdateAsync(plan, cancellationToken);
            return Result.Failure<StudioAiPlanDto>(
                Error.Validation("plan", "Ce plan a expiré. Redemandez la génération à l'assistant."));
        }

        if (plan.Status != StudioAiPlanStatus.Pending)
            return Result.Failure<StudioAiPlanDto>(
                Error.Conflict("Ce plan a déjà été traité (exécuté, annulé ou expiré)."));

        // Verrou anti double-confirmation : le perdant du RowVersion renonce sans rien exécuter.
        plan.MarkExecuting();
        if (!await _plans.TryUpdateAsync(plan, cancellationToken))
            return Result.Failure<StudioAiPlanDto>(Error.Conflict("Ce plan est déjà en cours d'exécution."));

        var (success, error, payload) = await _executor.ExecuteAsync(plan, command.Progress, cancellationToken);
        if (success)
            plan.MarkCompleted(payload is null ? null : System.Text.Json.JsonSerializer.Serialize(payload));
        else
            plan.MarkFailed(error ?? "Échec de l'exécution du plan.");

        await _plans.TryUpdateAsync(plan, cancellationToken);
        await StudioAudit.SafeLogAsync(_audit,
            success ? "Studio.AiPlan.Executed" : "Studio.AiPlan.Failed", "StudioAiBuildPlan", plan.Id,
            null, new { plan.Kind, plan.Status }, cancellationToken);

        return success
            ? Result.Success(StudioAiPlanDefaults.ToDto(plan))
            : Result.Failure<StudioAiPlanDto>(Error.Validation("plan", plan.ErrorMessage ?? "Échec de l'exécution du plan."));
    }
}

// ---- Cancel ----

public sealed record CancelStudioAiPlanCommand(Guid Id) : IRequest<Result<StudioAiPlanDto>>;

public sealed class CancelStudioAiPlanCommandHandler
    : IRequestHandler<CancelStudioAiPlanCommand, Result<StudioAiPlanDto>>
{
    private readonly IStudioAiBuildPlanRepository _plans;
    private readonly IAuditService _audit;
    private readonly ICurrentUser _currentUser;

    public CancelStudioAiPlanCommandHandler(
        IStudioAiBuildPlanRepository plans, IAuditService audit, ICurrentUser currentUser)
    {
        _plans = plans;
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<Result<StudioAiPlanDto>> Handle(CancelStudioAiPlanCommand command, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out _, out var err))
            return Result.Failure<StudioAiPlanDto>(err);

        var plan = await _plans.GetByIdAsync(tenantId, command.Id, cancellationToken);
        if (plan is null)
            return Result.Failure<StudioAiPlanDto>(Error.NotFound("StudioAiBuildPlan", command.Id));

        if (plan.Status != StudioAiPlanStatus.Pending)
            return Result.Failure<StudioAiPlanDto>(Error.Conflict("Seul un plan en attente peut être annulé."));

        plan.MarkCancelled();
        if (!await _plans.TryUpdateAsync(plan, cancellationToken))
            return Result.Failure<StudioAiPlanDto>(Error.Conflict("Ce plan est déjà en cours d'exécution."));

        await StudioAudit.SafeLogAsync(_audit, "Studio.AiPlan.Cancelled", "StudioAiBuildPlan", plan.Id,
            null, new { plan.Kind }, cancellationToken);
        return Result.Success(StudioAiPlanDefaults.ToDto(plan));
    }
}
