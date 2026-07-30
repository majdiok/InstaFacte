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

    /// <summary>
    /// Résout le prix de plusieurs produits en une seule requête.
    ///
    /// Utilisé par la caisse pour retarifer un ticket entier quand le caissier le rattache à un
    /// client : ligne à ligne, ce serait autant d'allers-retours qu'il y a d'articles.
    /// </summary>
    [HttpPost("resolve-batch")]
    [Authorize(Policy = PermissionPolicies.QuotesRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ResolvedPriceLineDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResolveBatch(
        [FromBody] ResolvePricesBatchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ResolvePricesBatchQuery(
                request.ClientId,
                request.Items ?? new List<ResolvePriceItemDto>(),
                request.Date),
            cancellationToken);

        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<IReadOnlyList<ResolvedPriceLineDto>>.Ok(result.Value));
    }
}

/// <summary>Corps de requête de la résolution par lot.</summary>
public sealed class ResolvePricesBatchRequest
{
    public Guid? ClientId { get; init; }
    public List<ResolvePriceItemDto>? Items { get; init; }
    public DateTime? Date { get; init; }
}
