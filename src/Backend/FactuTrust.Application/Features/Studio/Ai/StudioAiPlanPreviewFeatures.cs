using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.Fields;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Studio.Ai;

/// <summary>
/// Aperçu structuré d'un plan Studio IA persisté (PR 3.2c, flag <c>EnableStudioAiPlanPreview</c>) :
/// la spec stockée est re-projetée en <see cref="StudioAiPlanPreviewDto"/> par le constructeur PUR
/// <see cref="StudioAiPlanPreviewBuilder"/> (3.2b) — aucune écriture, aucun appel LLM. Le flag est
/// vérifié DANS le handler (contrôleur fin) : fonctionnalité coupée = introuvable, avec la même
/// chaîne figée que la création « from-spec ». Pour un amendement, le schéma RÉEL de la table cible
/// est relu via MediatR ; s'il est introuvable, l'aperçu reste rendu en mode dégradé (200 + warning,
/// C-B8) plutôt qu'en erreur.
/// </summary>
public sealed record GetStudioAiPlanPreviewQuery(Guid PlanId) : IRequest<Result<StudioAiPlanPreviewDto>>;

public sealed class GetStudioAiPlanPreviewQueryHandler
    : IRequestHandler<GetStudioAiPlanPreviewQuery, Result<StudioAiPlanPreviewDto>>
{
    private readonly IStudioAiBuildPlanRepository _plans;
    private readonly ICurrentUser _currentUser;
    private readonly IMediator _mediator;
    private readonly OllamaSettings _settings;

    public GetStudioAiPlanPreviewQueryHandler(
        IStudioAiBuildPlanRepository plans,
        ICurrentUser currentUser,
        IMediator mediator,
        IOptions<OllamaSettings> settings)
    {
        _plans = plans;
        _currentUser = currentUser;
        _mediator = mediator;
        _settings = settings.Value;
    }

    public async Task<Result<StudioAiPlanPreviewDto>> Handle(
        GetStudioAiPlanPreviewQuery request, CancellationToken cancellationToken)
    {
        // 1. Flag coupé = introuvable (chaîne figée, partagée avec les autres gardes Studio IA).
        if (!_settings.EnableStudioAiPlanPreview)
            return Result.Failure<StudioAiPlanPreviewDto>(Error.NotFound("Fonctionnalité non disponible."));

        // 2. Tenant → propriétaire → permission par nature (mêmes gardes que GET / spec / actions).
        var loaded = await StudioAiPlanDefaults.LoadAuthorizedAsync(
            _plans, _currentUser, request.PlanId, cancellationToken);
        if (loaded.IsFailure)
            return Result.Failure<StudioAiPlanPreviewDto>(loaded.Error);
        var plan = loaded.Value;

        // 3. Amendement : diff résolu contre le schéma RÉEL de la table cible, relu via MediatR.
        //    Échec de lecture (table supprimée, tenant, permission) ⇒ schema null : le constructeur
        //    rend alors l'aperçu DÉGRADÉ (200 + warning), jamais une erreur (B-Q4).
        CustomEntitySchemaDto? schema = null;
        if (plan.Kind == StudioAiPlanKind.Amendment
            && StudioAiAmendmentSpec.TryParse(plan.SpecJson, out var amendment, out _) && amendment is not null)
        {
            var resolved = await _mediator.Send(
                new GetCustomEntitySchemaQuery(amendment.TargetEntityRef), cancellationToken);
            if (resolved.IsSuccess)
                schema = resolved.Value;
        }

        // 4. Projection pure et déterministe ; les deux flags bornent relations N-N et vues.
        return StudioAiPlanPreviewBuilder.Build(
            plan.Id, plan.Kind, plan.Status.ToString(), plan.SpecJson, plan.SummaryJson, schema,
            _settings.EnableStudioManyToMany, _settings.EnableStudioRecordViews);
    }
}
