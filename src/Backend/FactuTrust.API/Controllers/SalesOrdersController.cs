using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Pricing.Commands;
using FactuTrust.Application.Features.SalesOrders.Commands;
using FactuTrust.Application.Features.SalesOrders.Queries;
using FactuTrust.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Commandes clients — l'engagement contractuel entre le devis et le bon de livraison.
/// </summary>
[ApiController]
[Route("api/sales-orders")]
[Authorize]
public sealed class SalesOrdersController : ControllerBase
{
    private readonly IMediator _mediator;

    public SalesOrdersController(IMediator mediator) => _mediator = mediator;

    /// <summary>Liste paginée et filtrée des commandes clients.</summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.SalesOrdersRead)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<SalesOrderListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] SalesOrderStatus? status,
        [FromQuery] Guid? clientId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] bool openOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetSalesOrdersQuery(search, status, clientId, fromDate, toDate, openOnly, page, pageSize),
            cancellationToken);

        return Ok(ApiResponse<PagedResult<SalesOrderListDto>>.Ok(result));
    }

    /// <summary>
    /// Totaux agrégés sur l'ensemble du jeu filtré — dont la valeur du carnet de commandes.
    /// </summary>
    [HttpGet("summary")]
    [Authorize(Policy = PermissionPolicies.SalesOrdersRead)]
    [ProducesResponseType(typeof(ApiResponse<SalesOrderListSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(
        [FromQuery] string? search,
        [FromQuery] SalesOrderStatus? status,
        [FromQuery] Guid? clientId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] bool openOnly = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetSalesOrdersSummaryQuery(search, status, clientId, fromDate, toDate, openOnly),
            cancellationToken);

        return Ok(ApiResponse<SalesOrderListSummaryDto>.Ok(result));
    }

    /// <summary>
    /// Carnet de commandes : ce qui reste à livrer, ligne par ligne, avec sa valeur HT et un
    /// indicateur de retard sur la date de livraison prévue.
    /// </summary>
    [HttpGet("backlog")]
    [Authorize(Policy = PermissionPolicies.SalesOrdersRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SalesOrderBacklogRowDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBacklog(
        [FromQuery] Guid? clientId,
        [FromQuery] DateTime? dueBefore,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetSalesOrderBacklogQuery(clientId, dueBefore), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<SalesOrderBacklogRowDto>>.Ok(result));
    }

    /// <summary>Détail d'une commande, lignes et reliquats compris.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.SalesOrdersRead)]
    [ProducesResponseType(typeof(ApiResponse<SalesOrderDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetSalesOrderByIdQuery(id), cancellationToken);

        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<SalesOrderDetailDto>.Ok(result.Value));
    }

    /// <summary>Crée une commande en brouillon.</summary>
    [HttpPost]
    [Authorize(Policy = PermissionPolicies.SalesOrdersCreate)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateSalesOrderDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateSalesOrderCommand(dto), cancellationToken);

        if (result.IsFailure)
            return MapFailure(result.Error);

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Value },
            ApiResponse<Guid>.Ok(result.Value, "Commande créée avec succès"));
    }

    /// <summary>Met à jour l'en-tête d'un brouillon.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.SalesOrdersUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateSalesOrderDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdateSalesOrderCommand(id, dto), cancellationToken);
        return result.IsFailure
            ? MapFailure(result.Error)
            : Ok(ApiResponse<object>.Ok(null!, "Commande mise à jour"));
    }

    /// <summary>
    /// Pose ou retire la remise de pied de document. Pourcentage et montant sont exclusifs ;
    /// les deux absents retirent la remise.
    ///
    /// La remise est répartie sur les lignes au prorata de leur base HT : le FODEC et la base
    /// de TVA portent donc sur ce qui est réellement facturé.
    /// </summary>
    [HttpPut("{id:guid}/global-discount")]
    [Authorize(Policy = PermissionPolicies.SalesOrdersUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetGlobalDiscount(
        Guid id,
        [FromBody] SetGlobalDiscountRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new SetSalesOrderGlobalDiscountCommand(id, request.Percent, request.Amount),
            cancellationToken);

        return result.IsFailure
            ? MapFailure(result.Error)
            : Ok(ApiResponse<object>.Ok(null!, "Remise de pied enregistrée"));
    }

    /// <summary>
    /// Confirme la commande : engagement ferme, entrée au carnet de commandes.
    /// </summary>
    [HttpPost("{id:guid}/confirm")]
    [Authorize(Policy = PermissionPolicies.SalesOrdersUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ConfirmSalesOrderCommand(id), cancellationToken);
        return result.IsFailure
            ? MapFailure(result.Error)
            : Ok(ApiResponse<object>.Ok(null!, "Commande confirmée"));
    }

    /// <summary>
    /// Annule une commande n'ayant donné lieu à aucun mouvement. Au-delà, employer la clôture.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = PermissionPolicies.SalesOrdersUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromBody] CancelSalesOrderDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CancelSalesOrderCommand(id, dto), cancellationToken);
        return result.IsFailure
            ? MapFailure(result.Error)
            : Ok(ApiResponse<object>.Ok(null!, "Commande annulée"));
    }

    /// <summary>
    /// Solde une commande entamée en abandonnant le reste à livrer. Les livraisons déjà
    /// effectuées sont conservées.
    /// </summary>
    [HttpPost("{id:guid}/close")]
    [Authorize(Policy = PermissionPolicies.SalesOrdersUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Close(
        Guid id,
        [FromBody] CloseSalesOrderDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CloseSalesOrderCommand(id, dto), cancellationToken);
        return result.IsFailure
            ? MapFailure(result.Error)
            : Ok(ApiResponse<object>.Ok(null!, "Commande clôturée"));
    }

    /// <summary>
    /// Émet un bon de livraison sur le reste à livrer de la commande (livraison partielle
    /// possible en précisant les quantités par ligne).
    /// </summary>
    [HttpPost("{id:guid}/generate-delivery-note")]
    [Authorize(Policy = PermissionPolicies.DeliveryNotesCreate)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateDeliveryNote(
        Guid id,
        [FromBody] GenerateDeliveryNoteFromSalesOrderDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GenerateDeliveryNoteFromSalesOrderCommand(id, dto.Lines, dto.IssueDate, dto.DeliveryAddress),
            cancellationToken);

        return result.IsFailure
            ? MapFailure(result.Error)
            : Ok(ApiResponse<Guid>.Ok(result.Value, "Bon de livraison généré"));
    }

    /// <summary>
    /// Émet une facture directement depuis la commande : sur le livré-non-facturé par
    /// défaut, ou sur le reste à facturer en facturation d'avance.
    /// </summary>
    [HttpPost("{id:guid}/generate-invoice")]
    [Authorize(Policy = PermissionPolicies.InvoicesCreate)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateInvoice(
        Guid id,
        [FromBody] GenerateInvoiceFromSalesOrderDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GenerateInvoiceFromSalesOrderCommand(id, dto.Lines, dto.IssueDate, dto.DueDate, dto.AdvanceBilling),
            cancellationToken);

        return result.IsFailure
            ? MapFailure(result.Error)
            : Ok(ApiResponse<Guid>.Ok(result.Value, "Facture générée"));
    }

    private IActionResult MapFailure(Domain.Common.Error error) =>
        error.Code == "NotFound"
            ? NotFound(ApiResponse<object>.Fail(error.Description))
            : BadRequest(ApiResponse<object>.Fail(error.Description));
}

/// <summary>Corps de requête de la remise de pied : pourcentage OU montant, jamais les deux.</summary>
public sealed class SetGlobalDiscountRequest
{
    public decimal? Percent { get; init; }
    public decimal? Amount { get; init; }
}
