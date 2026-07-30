using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Pricing.Commands;
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
    [Authorize(Policy = PermissionPolicies.PricingRead)]
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
    [Authorize(Policy = PermissionPolicies.PricingRead)]
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

    // ───────────────────────── Grilles tarifaires ─────────────────────────

    /// <summary>Liste des grilles tarifaires.</summary>
    [HttpGet("price-lists")]
    [Authorize(Policy = PermissionPolicies.PricingRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PriceListListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPriceLists(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPriceListsQuery(), cancellationToken);

        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<IReadOnlyList<PriceListListItemDto>>.Ok(result.Value));
    }

    /// <summary>Détail d'une grille, avec ses prix et l'écart au catalogue.</summary>
    [HttpGet("price-lists/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PricingRead)]
    [ProducesResponseType(typeof(ApiResponse<PriceListDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPriceList(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPriceListByIdQuery(id), cancellationToken);

        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<PriceListDetailDto>.Ok(result.Value));
    }

    /// <summary>Crée une grille tarifaire.</summary>
    [HttpPost("price-lists")]
    [Authorize(Policy = PermissionPolicies.PricingCreate)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePriceList(
        [FromBody] CreatePriceListCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return CreatedAtAction(
            nameof(GetPriceList),
            new { id = result.Value },
            ApiResponse<Guid>.Ok(result.Value, "Grille tarifaire créée"));
    }

    /// <summary>Renomme une grille, ajuste sa validité et son activation.</summary>
    [HttpPut("price-lists/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PricingUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePriceList(
        Guid id,
        [FromBody] UpdatePriceListRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new UpdatePriceListCommand(id, request.Name, request.ValidFrom, request.ValidUntil, request.IsActive),
            cancellationToken);

        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<object>.Ok(new { }, "Grille tarifaire mise à jour"));
    }

    /// <summary>Supprime une grille. Refusé si elle est encore affectée à des clients.</summary>
    [HttpDelete("price-lists/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PricingDelete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeletePriceList(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeletePriceListCommand(id), cancellationToken);

        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<object>.Ok(new { }, "Grille tarifaire supprimée"));
    }

    /// <summary>Fixe (ou remplace) le prix d'un produit dans une grille.</summary>
    [HttpPut("price-lists/{id:guid}/items/{productId:guid}")]
    [Authorize(Policy = PermissionPolicies.PricingUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetPriceListItem(
        Guid id,
        Guid productId,
        [FromBody] SetPriceListItemRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new SetPriceListItemCommand(id, productId, request.UnitPriceHT), cancellationToken);

        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<object>.Ok(new { }, "Prix enregistré"));
    }

    /// <summary>Retire un produit d'une grille : il revient au prix catalogue.</summary>
    [HttpDelete("price-lists/{id:guid}/items/{productId:guid}")]
    [Authorize(Policy = PermissionPolicies.PricingUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemovePriceListItem(
        Guid id, Guid productId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new RemovePriceListItemCommand(id, productId), cancellationToken);

        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<object>.Ok(new { }, "Prix retiré"));
    }

    /// <summary>
    /// Fixe un palier quantitatif sur un produit déjà tarifé dans la grille : « à partir de
    /// <c>minQuantity</c>, le prix unitaire devient <c>unitPriceHT</c> ».
    /// </summary>
    [HttpPut("price-lists/{id:guid}/items/{productId:guid}/tiers/{minQuantity:decimal}")]
    [Authorize(Policy = PermissionPolicies.PricingUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetPriceListTier(
        Guid id,
        Guid productId,
        decimal minQuantity,
        [FromBody] SetPriceListItemRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new SetPriceListTierCommand(id, productId, minQuantity, request.UnitPriceHT),
            cancellationToken);

        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<object>.Ok(new { }, "Palier enregistré"));
    }

    /// <summary>Retire un palier : la quantité concernée retombe sur le palier inférieur.</summary>
    [HttpDelete("price-lists/{id:guid}/items/{productId:guid}/tiers/{minQuantity:decimal}")]
    [Authorize(Policy = PermissionPolicies.PricingUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemovePriceListTier(
        Guid id,
        Guid productId,
        decimal minQuantity,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new RemovePriceListTierCommand(id, productId, minQuantity), cancellationToken);

        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<object>.Ok(new { }, "Palier retiré"));
    }

    // ───────────────────── Tarification d'un client ─────────────────────

    /// <summary>Grille affectée et prix négociés d'un client.</summary>
    [HttpGet("clients/{clientId:guid}")]
    [Authorize(Policy = PermissionPolicies.PricingRead)]
    [ProducesResponseType(typeof(ApiResponse<ClientPricingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClientPricing(Guid clientId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetClientPricingQuery(clientId), cancellationToken);

        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<ClientPricingDto>.Ok(result.Value));
    }

    /// <summary>Affecte une grille au client, ou la retire (corps avec priceListId nul).</summary>
    [HttpPut("clients/{clientId:guid}/price-list")]
    [Authorize(Policy = PermissionPolicies.PricingUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AssignClientPriceList(
        Guid clientId,
        [FromBody] AssignClientPriceListRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new AssignClientPriceListCommand(clientId, request.PriceListId), cancellationToken);

        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<object>.Ok(new { }, "Grille affectée au client"));
    }

    /// <summary>Crée ou met à jour un prix négocié pour un couple client / produit.</summary>
    [HttpPut("clients/{clientId:guid}/products/{productId:guid}")]
    [Authorize(Policy = PermissionPolicies.PricingUpdate)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpsertClientProductPrice(
        Guid clientId,
        Guid productId,
        [FromBody] UpsertClientProductPriceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new UpsertClientProductPriceCommand(
                clientId, productId, request.UnitPriceHT,
                request.ValidFrom, request.ValidUntil, request.IsActive),
            cancellationToken);

        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<Guid>.Ok(result.Value, "Prix négocié enregistré"));
    }

    /// <summary>Supprime un prix négocié : le client repasse à sa grille, ou au catalogue.</summary>
    [HttpDelete("client-prices/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PricingDelete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteClientProductPrice(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteClientProductPriceCommand(id), cancellationToken);

        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<object>.Ok(new { }, "Prix négocié supprimé"));
    }

    // ───────────────────────── Promotions ─────────────────────────

    /// <summary>Liste des promotions, avec leur portée et si elles courent aujourd'hui.</summary>
    [HttpGet("promotions")]
    [Authorize(Policy = PermissionPolicies.PricingRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PromotionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPromotions(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPromotionsQuery(), cancellationToken);

        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<IReadOnlyList<PromotionDto>>.Ok(result.Value));
    }

    /// <summary>Crée une promotion datée.</summary>
    [HttpPost("promotions")]
    [Authorize(Policy = PermissionPolicies.PricingCreate)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePromotion(
        [FromBody] CreatePromotionCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<Guid>.Ok(result.Value, "Promotion créée"));
    }

    /// <summary>Met à jour une promotion. La désactiver n'affecte aucun document émis.</summary>
    [HttpPut("promotions/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PricingUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePromotion(
        Guid id,
        [FromBody] UpdatePromotionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new UpdatePromotionCommand(
                id, request.Name, request.StartsOn, request.EndsOn, request.DiscountType,
                request.DiscountPercent, request.DiscountAmount, request.MinQuantity,
                request.Priority, request.IsActive),
            cancellationToken);

        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<object>.Ok(new { }, "Promotion mise à jour"));
    }

    /// <summary>Supprime une promotion.</summary>
    [HttpDelete("promotions/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PricingDelete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeletePromotion(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeletePromotionCommand(id), cancellationToken);

        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<object>.Ok(new { }, "Promotion supprimée"));
    }

    // ─────────────────── Conditions de règlement ───────────────────

    /// <summary>
    /// Conditions de règlement, avec le libellé qui s'imprimera et l'échéance qu'aurait un
    /// document émis aujourd'hui — la règle devient tangible au lieu de rester abstraite.
    /// </summary>
    [HttpGet("payment-terms")]
    [Authorize(Policy = PermissionPolicies.PricingRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PaymentTermTemplateDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaymentTerms(
        [FromQuery] bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetPaymentTermTemplatesQuery(activeOnly), cancellationToken);

        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<IReadOnlyList<PaymentTermTemplateDto>>.Ok(result.Value));
    }

    /// <summary>Crée ou met à jour une condition de règlement.</summary>
    [HttpPut("payment-terms")]
    [Authorize(Policy = PermissionPolicies.PricingUpdate)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpsertPaymentTerm(
        [FromBody] UpsertPaymentTermTemplateCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<Guid>.Ok(result.Value, "Condition de règlement enregistrée"));
    }

    /// <summary>Supprime une condition de règlement. Les documents émis ne sont pas touchés.</summary>
    [HttpDelete("payment-terms/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PricingDelete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeletePaymentTerm(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeletePaymentTermTemplateCommand(id), cancellationToken);

        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<object>.Ok(new { }, "Condition de règlement supprimée"));
    }
}

/// <summary>Corps de requête de la mise à jour d'une promotion.</summary>
public sealed class UpdatePromotionRequest
{
    public string Name { get; init; } = string.Empty;
    public DateTime StartsOn { get; init; }
    public DateTime EndsOn { get; init; }
    public FactuTrust.Domain.Enums.PromotionDiscountType DiscountType { get; init; }
    public decimal? DiscountPercent { get; init; }
    public decimal? DiscountAmount { get; init; }
    public decimal MinQuantity { get; init; } = 1m;
    public int Priority { get; init; }
    public bool IsActive { get; init; } = true;
}

/// <summary>Corps de requête de la résolution par lot.</summary>
public sealed class ResolvePricesBatchRequest
{
    public Guid? ClientId { get; init; }
    public List<ResolvePriceItemDto>? Items { get; init; }
    public DateTime? Date { get; init; }
}

public sealed class UpdatePriceListRequest
{
    public string Name { get; init; } = string.Empty;
    public DateTime? ValidFrom { get; init; }
    public DateTime? ValidUntil { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class SetPriceListItemRequest
{
    public decimal UnitPriceHT { get; init; }
}

public sealed class AssignClientPriceListRequest
{
    public Guid? PriceListId { get; init; }
}

public sealed class UpsertClientProductPriceRequest
{
    public decimal UnitPriceHT { get; init; }
    public DateTime? ValidFrom { get; init; }
    public DateTime? ValidUntil { get; init; }
    public bool IsActive { get; init; } = true;
}
