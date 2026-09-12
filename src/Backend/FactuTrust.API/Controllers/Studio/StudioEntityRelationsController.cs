using FactuTrust.API.Authorization;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.Relations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FactuTrust.API.Controllers.Studio;

/// <summary>
/// Relations d'une table Studio (PR 2.1) : lecture des relations (<c>many_to_one</c>, <c>one_to_many</c>,
/// <c>many_to_many</c>) et création d'une relation plusieurs‑à‑plusieurs par table de jonction.
/// Design-time : politique <c>studio:design_entities</c>. Gardé par <c>Ollama:EnableStudioManyToMany</c>
/// (drapeau coupé ⇒ 404 avant tout appel MediatR, même patron que le workbench des plans IA).
/// </summary>
[ApiController]
[Route("api/studio/entities/{id:guid}/relations")]
[Authorize]
[Authorize(Policy = PermissionPolicies.StudioDesignEntities)]
public sealed class StudioEntityRelationsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly OllamaSettings _ollamaSettings;

    public StudioEntityRelationsController(IMediator mediator, IOptions<OllamaSettings> ollamaSettings)
    {
        _mediator = mediator;
        _ollamaSettings = ollamaSettings.Value;
    }

    /// <summary>Toutes les relations actives dans lesquelles la table intervient.</summary>
    [HttpGet]
    public async Task<IActionResult> List(Guid id, CancellationToken cancellationToken)
    {
        if (ManyToManyUnavailableOrNull() is { } unavailable)
            return unavailable;

        var result = await _mediator.Send(new ListEntityRelationsQuery(id), cancellationToken);
        return StudioErrorMapping.ToActionResult(this, result,
            value => Ok(ApiResponse<IReadOnlyList<EntityRelationDto>>.Ok(value)));
    }

    /// <summary>
    /// Crée une relation plusieurs‑à‑plusieurs entre la table <paramref name="id"/> (source) et
    /// <c>request.TargetEntityId</c> : table de jonction <c>Kind = Junction</c> + deux champs
    /// <c>RelationCustom</c> requis. 404 table introuvable, 400 cible invalide / quota, 409 clé de jonction prise.
    /// </summary>
    [HttpPost("many-to-many")]
    public async Task<IActionResult> CreateManyToMany(
        Guid id, [FromBody] CreateManyToManyRelationRequest request, CancellationToken cancellationToken)
    {
        if (ManyToManyUnavailableOrNull() is { } unavailable)
            return unavailable;

        var result = await _mediator.Send(new CreateManyToManyRelationCommand(id, request), cancellationToken);
        return StudioErrorMapping.ToActionResult(this, result,
            value => Ok(ApiResponse<ManyToManyRelationDto>.Ok(value, "Relation plusieurs-à-plusieurs créée.")));
    }

    private IActionResult? ManyToManyUnavailableOrNull() =>
        _ollamaSettings.EnableStudioManyToMany
            ? null
            : NotFound(ApiResponse<object>.Fail("Les relations plusieurs-à-plusieurs du Studio ne sont pas activées."));
}
