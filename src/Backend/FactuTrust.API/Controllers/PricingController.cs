using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Pricing.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Tarification — interrogation du point de résolution de prix unique.
/// </summary>
[ApiController]
[Route("api/pricing")]
[Authorize]
public sealed class PricingController : ControllerBase
{
    private readonly IMediator _mediator;

    public PricingController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Prix HT que le serveur appliquera pour ce client, ce produit, cette quantité et cette
    /// date — prix négocié, grille affectée, ou catalogue.
    ///
    /// L'écran de saisie doit l'appeler avant d'ajouter une ligne : sans cela il afficherait
    /// le prix catalogue, que le serveur remplacerait ensuite en silence par le prix résolu.
    /// </summary>
    [HttpGet("resolve")]
    [Authorize(Policy = PermissionPolicies.QuotesRead)]
    [ProducesResponseType(typeof(ApiResponse<ResolvedPriceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Resolve(
        [FromQuery] Guid productId,
        [FromQuery] Guid? clientId,
        [FromQuery] decimal quantity = 1m,
        [FromQuery] DateTime? date = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new ResolvePriceQuery(clientId, productId, quantity, date), cancellationToken);

        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<ResolvedPriceDto>.Ok(result.Value));
    }
}
