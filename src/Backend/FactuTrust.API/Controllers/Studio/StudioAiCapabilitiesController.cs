using FactuTrust.API.Authorization;
using FactuTrust.Application.Features.Studio.Ai;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers.Studio;

/// <summary>
/// Capacités du Studio IA (tâche B-P0-08) : le frontend les lit pour piloter l'affichage de la
/// page « Studio IA » (cartes de création, interrupteur « Modèle avancé », bannières). Endpoint
/// toujours actif — c'est lui qui ANNONCE les flags, il ne peut pas être gardé par eux. Ne
/// renvoie jamais la référence d'un modèle, seulement des libellés humains.
/// </summary>
[ApiController]
[Route("api/ai/studio/capabilities")]
[Authorize(Policy = PermissionPolicies.StudioDesignEntities)]
public sealed class StudioAiCapabilitiesController : ControllerBase
{
    private readonly IMediator _mediator;

    public StudioAiCapabilitiesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new StudioAiCapabilitiesQuery(), cancellationToken);
        return StudioErrorMapping.ToActionResult(this, result,
            value => Ok(ApiResponse<StudioAiCapabilitiesDto>.Ok(value)));
    }
}
