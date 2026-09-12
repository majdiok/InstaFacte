using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Studio.Ai;

/// <summary>
/// « Workbench » du plan Studio IA (flag <c>EnableStudioAiWorkbench</c>) : lecture de la spec
/// canonique, réécriture côté serveur (re-parse + résumé recalculé, jamais de résumé client),
/// liste paginée des plans de l'utilisateur et purge des plans en attente. Mêmes conventions que
/// <c>StudioAiPlanFeatures.cs</c> : <see cref="StudioContext.TryGet"/>,
/// <see cref="StudioAiPlanDefaults.LoadAuthorizedAsync"/> (tenant → propriétaire → permission),
/// <see cref="StudioAudit.SafeLogAsync"/>. AUCUN appel LLM : tout est déterministe.
/// </summary>
public static class StudioAiPlanWorkbench
{
    /// <summary>Taille maximale d'une spec éditée (256 Ko de caractères JSON).</summary>
    public const int MaxSpecJsonLength = 256 * 1024;

    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;

    /// <summary>Spec exposée à l'éditeur : la forme canonique ; un plan ancien illisible garde sa forme brute.</summary>
    public static StudioAiPlanSpecDto ToSpecDto(StudioAiBuildPlan plan)
    {
        var status = plan.IsExpired(DateTime.UtcNow) ? StudioAiPlanStatus.Expired : plan.Status;
        var canonical = StudioAiSpecCanonical.CanonicalFor(plan.Kind, plan.SpecJson, out _);
        var spec = JsonNode.Parse(canonical ?? plan.SpecJson)!;
        var rowVersion = plan.RowVersion is { Length: > 0 } ? Convert.ToBase64String(plan.RowVersion) : string.Empty;
        return new StudioAiPlanSpecDto(plan.Id, plan.Kind.ToString(), status.ToString(), plan.ExpiresAt, rowVersion, spec);
    }

    /// <summary>Empreinte d'audit d'une spec : jamais le contenu complet (potentiellement volumineux).</summary>
    public static object SpecFingerprint(string specJson) => new
    {
        Length = specJson.Length,
        Sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(specJson)))[..16],
        Preview = specJson.Length <= 200 ? specJson : specJson[..200] + "…"
    };
}

public sealed record StudioAiPlanSpecDto(Guid Id, string Kind, string Status, DateTime ExpiresAt, string RowVersion, JsonNode Spec);

public sealed record StudioAiPlanListItemDto(Guid Id, string Kind, string Status, string Title, int EntityCount,
    DateTime CreatedAt, DateTime ExpiresAt, DateTime? ExecutedAt, string? SystemKey);

public sealed record UpdateStudioAiPlanSpecRequest(string SpecJson, string RowVersion);

public sealed record UpdateStudioAiPlanSpecResponse(StudioAiPlanDto Plan, StudioAiPlanSpecDto Spec);

// ---- GetSpec (lecture de la spec canonique d'un plan possédé) ----

public sealed record GetStudioAiPlanSpecQuery(Guid Id) : IRequest<Result<StudioAiPlanSpecDto>>;

public sealed class GetStudioAiPlanSpecQueryHandler : IRequestHandler<GetStudioAiPlanSpecQuery, Result<StudioAiPlanSpecDto>>
{
    private readonly IStudioAiBuildPlanRepository _plans;
    private readonly ICurrentUser _currentUser;

    public GetStudioAiPlanSpecQueryHandler(IStudioAiBuildPlanRepository plans, ICurrentUser currentUser)
    {
        _plans = plans;
        _currentUser = currentUser;
    }

    public async Task<Result<StudioAiPlanSpecDto>> Handle(GetStudioAiPlanSpecQuery request, CancellationToken cancellationToken)
    {
        // Mêmes gardes que les actions : tenant → propriétaire → permission par nature.
        var loaded = await StudioAiPlanDefaults.LoadAuthorizedAsync(_plans, _currentUser, request.Id, cancellationToken);
        if (loaded.IsFailure)
            return Result.Failure<StudioAiPlanSpecDto>(loaded.Error);

        try
        {
            return Result.Success(StudioAiPlanWorkbench.ToSpecDto(loaded.Value));
        }
        catch (JsonException)
        {
            // Spec stockée ni canonicalisable ni lisible (plan ancien corrompu) : refus explicite, jamais de 500.
            return Result.Failure<StudioAiPlanSpecDto>(
                Error.Validation("specJson", "La spec stockée du plan n'est pas relisible."));
        }
    }
}

// ---- UpdateSpec (réécriture de la spec d'un plan Pending) ----

public sealed record UpdateStudioAiPlanSpecCommand(Guid Id, string SpecJson, string RowVersion)
    : IRequest<Result<UpdateStudioAiPlanSpecResponse>>;

public sealed class UpdateStudioAiPlanSpecCommandHandler
    : IRequestHandler<UpdateStudioAiPlanSpecCommand, Result<UpdateStudioAiPlanSpecResponse>>
{
    private readonly IStudioAiBuildPlanRepository _plans;
    private readonly IAuditService _audit;
    private readonly ICurrentUser _currentUser;

    public UpdateStudioAiPlanSpecCommandHandler(
        IStudioAiBuildPlanRepository plans, IAuditService audit, ICurrentUser currentUser)
    {
        _plans = plans;
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<Result<UpdateStudioAiPlanSpecResponse>> Handle(
        UpdateStudioAiPlanSpecCommand command, CancellationToken cancellationToken)
    {
        var loaded = await StudioAiPlanDefaults.LoadAuthorizedAsync(_plans, _currentUser, command.Id, cancellationToken);
        if (loaded.IsFailure)
            return Result.Failure<UpdateStudioAiPlanSpecResponse>(loaded.Error);
        var plan = loaded.Value;

        if (plan.Status != StudioAiPlanStatus.Pending)
            return Result.Failure<UpdateStudioAiPlanSpecResponse>(
                Error.Conflict("Seul un plan en attente est modifiable."));
        if (plan.IsExpired(DateTime.UtcNow))
            return Result.Failure<UpdateStudioAiPlanSpecResponse>(
                Error.Conflict("Le plan a expiré. Redemandez la génération à l'assistant."));

        if (string.IsNullOrWhiteSpace(command.SpecJson))
            return Result.Failure<UpdateStudioAiPlanSpecResponse>(
                Error.Validation("specJson", "La spécification du plan est vide."));
        if (command.SpecJson.Length > StudioAiPlanWorkbench.MaxSpecJsonLength)
            return Result.Failure<UpdateStudioAiPlanSpecResponse>(
                Error.Validation("specJson", "La spec dépasse 256 Ko."));

        // Re-parse systématique côté serveur (bornes des parseurs : 8 entités, 40 champs/entité,
        // 200 fiches de seed) puis canonicalisation — tout SummaryJson client est ignoré et le
        // résumé est RECALCULÉ ici. Seules les créations ont un résumé recalculable sans base :
        // l'aperçu d'une modification/fenêtre/état se résout contre le schéma réel ou des données
        // vivantes, ces natures ne sont donc pas éditables en P0.
        if (plan.Kind is not (StudioAiPlanKind.CreateSystem or StudioAiPlanKind.CreateApp))
            return Result.Failure<UpdateStudioAiPlanSpecResponse>(
                Error.Validation("kind", "Seuls les plans de création (système ou table) sont modifiables."));

        // Même chaîne parse → canonicalisation → résumé recalculé que la création « from-spec »
        // (pour une création, le résumé est toujours recalculé : la forme canonique se re-parse).
        if (!StudioAiPlanCreation.TryCanonicalize(
                plan.Kind, command.SpecJson, out var canonical, out var summary, out var parseError))
            return Result.Failure<UpdateStudioAiPlanSpecResponse>(
                Error.Validation("specJson", parseError ?? "La spec est invalide."));

        var previousSpec = plan.SpecJson;
        plan.UpdateSpec(canonical!, summary!); // garde domaine : jette si le plan n'est plus Pending

        // Jeton de concurrence fourni par le client (base64, comme SaveCustomRecordRequest) ;
        // illisible ⇒ repli sur le jeton rechargé, la concurrence reste protégée côté persistance.
        byte[]? expectedRowVersion = null;
        if (!string.IsNullOrWhiteSpace(command.RowVersion))
        {
            try { expectedRowVersion = Convert.FromBase64String(command.RowVersion); }
            catch (FormatException) { expectedRowVersion = null; }
        }

        if (!await _plans.TryUpdateAsync(plan, cancellationToken, expectedRowVersion))
            return Result.Failure<UpdateStudioAiPlanSpecResponse>(
                Error.Conflict("Le plan a été modifié entre-temps. Rechargez-le."));

        await StudioAudit.SafeLogAsync(_audit, "Studio.AiPlan.Updated", "StudioAiBuildPlan", plan.Id,
            StudioAiPlanWorkbench.SpecFingerprint(previousSpec), StudioAiPlanWorkbench.SpecFingerprint(canonical!), cancellationToken);

        return Result.Success(new UpdateStudioAiPlanSpecResponse(
            StudioAiPlanDefaults.ToDto(plan), StudioAiPlanWorkbench.ToSpecDto(plan)));
    }
}

// ---- List (historique paginé des générations de l'utilisateur) ----

public sealed record ListStudioAiPlansQuery(string? Status, string? Kind, int Page, int PageSize)
    : IRequest<Result<PagedResult<StudioAiPlanListItemDto>>>;

public sealed class ListStudioAiPlansQueryHandler
    : IRequestHandler<ListStudioAiPlansQuery, Result<PagedResult<StudioAiPlanListItemDto>>>
{
    private readonly IStudioAiBuildPlanRepository _plans;
    private readonly ICurrentUser _currentUser;

    public ListStudioAiPlansQueryHandler(IStudioAiBuildPlanRepository plans, ICurrentUser currentUser)
    {
        _plans = plans;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<StudioAiPlanListItemDto>>> Handle(
        ListStudioAiPlansQuery request, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out var userId, out var err))
            return Result.Failure<PagedResult<StudioAiPlanListItemDto>>(err);

        if (!TryParseEnumName<StudioAiPlanStatus>(request.Status, out var status))
            return Result.Failure<PagedResult<StudioAiPlanListItemDto>>(
                Error.Validation("status", $"Statut de plan inconnu : « {request.Status} »."));
        if (!TryParseEnumName<StudioAiPlanKind>(request.Kind, out var kind))
            return Result.Failure<PagedResult<StudioAiPlanListItemDto>>(
                Error.Validation("kind", $"Nature de plan inconnue : « {request.Kind} »."));

        var page = Math.Max(1, request.Page);
        var pageSize = request.PageSize <= 0
            ? StudioAiPlanWorkbench.DefaultPageSize
            : Math.Min(request.PageSize, StudioAiPlanWorkbench.MaxPageSize);

        var (items, total) = await _plans.ListByOwnerAsync(
            tenantId, userId?.ToString() ?? string.Empty, status, kind, page, pageSize, cancellationToken);

        var utcNow = DateTime.UtcNow;
        var dtos = items.Select(plan => ToListItemDto(plan, utcNow)).ToList();
        return Result.Success(PagedResult<StudioAiPlanListItemDto>.Create(dtos, page, pageSize, total));
    }

    /// <summary>Filtre enum optionnel parsé par NOM exact (insensible à la casse), jamais par valeur numérique.</summary>
    private static bool TryParseEnumName<TEnum>(string? raw, out TEnum? value) where TEnum : struct, Enum
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw)) return true;
        var name = Enum.GetNames<TEnum>()
            .FirstOrDefault(n => string.Equals(n, raw.Trim(), StringComparison.OrdinalIgnoreCase));
        if (name is null) return false;
        value = Enum.Parse<TEnum>(name);
        return true;
    }

    /// <summary>Un plan Pending échu est PRÉSENTÉ « Expired » sans écriture (comme <see cref="StudioAiPlanDefaults.ToDto"/>).</summary>
    private static StudioAiPlanListItemDto ToListItemDto(StudioAiBuildPlan plan, DateTime utcNow)
    {
        var status = plan.IsExpired(utcNow) ? StudioAiPlanStatus.Expired : plan.Status;
        var (title, entityCount) = ReadSummaryHeader(plan.SummaryJson);
        return new StudioAiPlanListItemDto(
            plan.Id, plan.Kind.ToString(), status.ToString(), title, entityCount,
            plan.CreatedAt, plan.ExpiresAt, plan.ExecutedAt, ReadSystemKey(plan.ResultJson));
    }

    /// <summary>Titre et nombre de tables extraits du résumé (<c>title</c>, <c>entities.Count</c>) — jamais de SpecJson ici.</summary>
    private static (string Title, int EntityCount) ReadSummaryHeader(string? summaryJson)
    {
        if (!string.IsNullOrWhiteSpace(summaryJson))
        {
            try
            {
                var node = JsonNode.Parse(summaryJson);
                var title = node?["title"] is JsonValue value && value.TryGetValue<string>(out var text) ? text : string.Empty;
                var entityCount = node?["entities"] is JsonArray entities ? entities.Count : 0;
                return (title ?? string.Empty, entityCount);
            }
            catch (JsonException)
            {
                // Résumé illisible : la ligne de liste reste rendue, sans titre.
            }
        }
        return (string.Empty, 0);
    }

    private static string? ReadSystemKey(string? resultJson)
    {
        if (string.IsNullOrWhiteSpace(resultJson)) return null;
        try
        {
            if (JsonNode.Parse(resultJson) is not JsonObject obj) return null;
            var property = obj.FirstOrDefault(p => string.Equals(p.Key, "systemKey", StringComparison.OrdinalIgnoreCase));
            return property.Value is JsonValue value && value.TryGetValue<string>(out var key) ? key : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

// ---- CancelPending (« Réinitialiser la conversation » : purge des plans en attente) ----

public sealed record CancelPendingStudioAiPlansCommand() : IRequest<Result<int>>;

public sealed class CancelPendingStudioAiPlansCommandHandler : IRequestHandler<CancelPendingStudioAiPlansCommand, Result<int>>
{
    private readonly IStudioAiBuildPlanRepository _plans;
    private readonly IAuditService _audit;
    private readonly ICurrentUser _currentUser;

    public CancelPendingStudioAiPlansCommandHandler(
        IStudioAiBuildPlanRepository plans, IAuditService audit, ICurrentUser currentUser)
    {
        _plans = plans;
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<Result<int>> Handle(CancelPendingStudioAiPlansCommand command, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out var userId, out var err))
            return Result.Failure<int>(err);

        // Permission de conception de base (la politique du contrôleur est StudioDesignEntities) :
        // les plans purgés sont de natures mélangées, le contrôle fin par nature n'a pas de sens ici.
        if (!_currentUser.HasPermission(Permissions.Studio.DesignEntities))
            return Result.Failure<int>(Error.Unauthorized("Permission de conception Studio requise."));

        // Un utilisateur inconnu ne possède aucun plan (deny-by-default, comme IsOwnedBy).
        if (userId is null)
            return Result.Success(0);

        // Le dépôt ne remonte que les plans Pending NON expirés du couple (tenant, utilisateur) :
        // jamais ceux d'un collègue, jamais un plan d'un autre statut.
        var pending = await _plans.ListPendingByOwnerAsync(tenantId, userId.Value.ToString(), cancellationToken);

        var cancelled = 0;
        foreach (var plan in pending)
        {
            plan.MarkCancelled();
            // Le perdant d'une éventuelle course (confirmation simultanée) n'est pas compté.
            if (!await _plans.TryUpdateAsync(plan, cancellationToken))
                continue;
            cancelled++;
            await StudioAudit.SafeLogAsync(_audit, "Studio.AiPlan.Cancelled", "StudioAiBuildPlan", plan.Id,
                null, new { plan.Kind, PlanCreatedBy = plan.CreatedBy, CancelledBy = userId, Bulk = true }, cancellationToken);
        }

        return Result.Success(cancelled);
    }
}
