using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.PurchaseOrders.Commands;
using FactuTrust.Application.Features.PurchaseOrders.Queries;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Controller for managing purchase orders (bons de commande fournisseur).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PurchaseOrdersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<PurchaseOrdersController> _logger;

    public PurchaseOrdersController(IMediator mediator, ILogger<PurchaseOrdersController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Get paginated list of purchase orders with optional search and filters.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.PurchaseOrdersRead)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PurchaseOrderListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPurchaseOrders(
        [FromQuery] string? search,
        [FromQuery] PurchaseOrderStatus? status,
        [FromQuery] Guid? supplierId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetPurchaseOrdersQuery(search, status, supplierId, fromDate, toDate, page, pageSize);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<PurchaseOrderListDto>>.Ok(result));
    }

    /// <summary>
    /// Get aggregated totals for the purchase order list (same filters, computed over the entire
    /// filtered set — not just the current page) for the totals zone.
    /// </summary>
    [HttpGet("summary")]
    [Authorize(Policy = PermissionPolicies.PurchaseOrdersRead)]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderListSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPurchaseOrdersSummary(
        [FromQuery] string? search,
        [FromQuery] PurchaseOrderStatus? status,
        [FromQuery] Guid? supplierId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken = default)
    {
        var query = new GetPurchaseOrdersSummaryQuery(search, status, supplierId, fromDate, toDate);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<PurchaseOrderListSummaryDto>.Ok(result));
    }

    /// <summary>
    /// Get purchase order details by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PurchaseOrdersRead)]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPurchaseOrder(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetPurchaseOrderByIdQuery(id);
        var result = await _mediator.Send(query, cancellationToken);

        if (result.IsFailure)
            return NotFound(ApiResponse<PurchaseOrderDetailDto>.Fail(result.Error.Description));

        return Ok(ApiResponse<PurchaseOrderDetailDto>.Ok(result.Value));
    }

    /// <summary>
    /// Create a new purchase order.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = PermissionPolicies.PurchaseOrdersCreate)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePurchaseOrder(
        [FromBody] CreatePurchaseOrderDto dto,
        CancellationToken cancellationToken)
    {
        var command = new CreatePurchaseOrderCommand(dto);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
            return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));

        _logger.LogInformation("Purchase order created with ID {PurchaseOrderId}", result.Value);

        return CreatedAtAction(
            nameof(GetPurchaseOrder),
            new { id = result.Value },
            ApiResponse<Guid>.Ok(result.Value, "Bon de commande créé avec succès."));
    }

    /// <summary>
    /// Confirm a purchase order (Draft → Confirmed).
    /// </summary>
    [HttpPatch("{id:guid}/confirm")]
    [Authorize(Policy = PermissionPolicies.PurchaseOrdersUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmPurchaseOrder(Guid id, CancellationToken cancellationToken)
    {
        var command = new ConfirmPurchaseOrderCommand(id);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
            return MapFailure(result.Error);

        return Ok(ApiResponse<object>.Ok(null!, "Bon de commande confirmé."));
    }

    /// <summary>
    /// Receive goods for a purchase order.
    /// </summary>
    [HttpPatch("{id:guid}/receive")]
    [Authorize(Policy = PermissionPolicies.PurchaseOrdersUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReceiveGoods(
        Guid id,
        [FromBody] ReceiveGoodsDto dto,
        CancellationToken cancellationToken)
    {
        var command = new ReceiveGoodsCommand(id, dto);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
            return MapFailure(result.Error);

        return Ok(ApiResponse<object>.Ok(null!, "Réception enregistrée."));
    }

    /// <summary>
    /// Cancel a purchase order.
    /// </summary>
    [HttpPatch("{id:guid}/cancel")]
    [Authorize(Policy = PermissionPolicies.PurchaseOrdersUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelPurchaseOrder(
        Guid id,
        [FromBody] CancelPurchaseOrderRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CancelPurchaseOrderCommand(id, request.Reason);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
            return MapFailure(result.Error);

        return Ok(ApiResponse<object>.Ok(null!, "Bon de commande annulé."));
    }

    /// <summary>
    /// Update a draft purchase order.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PurchaseOrdersUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePurchaseOrder(
        Guid id,
        [FromBody] UpdatePurchaseOrderDto dto,
        CancellationToken cancellationToken)
    {
        var command = new UpdatePurchaseOrderCommand(id, dto);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
            return MapFailure(result.Error);

        return Ok(ApiResponse<object>.Ok(null!, "Bon de commande mis à jour."));
    }

    /// <summary>
    /// Delete a draft purchase order.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PurchaseOrdersDelete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePurchaseOrder(Guid id, CancellationToken cancellationToken)
    {
        var command = new DeletePurchaseOrderCommand(id);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
            return MapFailure(result.Error);

        _logger.LogInformation("Purchase order {PurchaseOrderId} deleted", id);

        return Ok(ApiResponse<object>.Ok(null!, "Bon de commande supprimé."));
    }

    /// <summary>
    /// Export purchase order as PDF.
    /// </summary>
    [HttpGet("{id:guid}/pdf")]
    [Authorize(Policy = PermissionPolicies.PurchaseOrdersRead)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportPdf(Guid id, [FromQuery] string? templateKey, CancellationToken cancellationToken = default)
    {
        var query = new ExportPurchaseOrderPdfQuery(id, templateKey);
        var result = await _mediator.Send(query, cancellationToken);

        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description));

        var pdf = result.Value;
        return File(pdf.Content, pdf.ContentType, pdf.FileName);
    }

    /// <summary>
    /// Send purchase order by email to supplier.
    /// </summary>
    [HttpPost("{id:guid}/send-email")]
    [Authorize(Policy = PermissionPolicies.PurchaseOrdersUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendByEmail(Guid id, CancellationToken cancellationToken)
    {
        var command = new SendPurchaseOrderEmailCommand(id);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
            return MapFailure(result.Error);

        return Ok(ApiResponse<object>.Ok(null!, "Bon de commande envoyé par email."));
    }

    /// <summary>
    /// Create a supplier invoice from a received purchase order.
    /// </summary>
    [HttpPost("{id:guid}/create-supplier-invoice")]
    [Authorize(Policy = PermissionPolicies.SupplierInvoicesCreate)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateSupplierInvoice(
        Guid id,
        [FromBody] CreateSupplierInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateSupplierInvoiceFromPOCommand(
            id,
            request.InvoiceNumber,
            request.InvoiceDate,
            request.PaymentTermDays,
            request.ExternalReference,
            request.Notes,
            request.SendEmail ?? false,
            request.LineAssetClassifications,
            request.PaymentMethod);

        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
            return MapFailure<Guid>(result.Error);

        _logger.LogInformation("Supplier invoice created from PO {PurchaseOrderId}: {InvoiceId}", id, result.Value);

        return CreatedAtAction(
            nameof(GetPurchaseOrder),
            new { id },
            ApiResponse<Guid>.Ok(result.Value, "Facture fournisseur créée avec succès"));
    }

    private IActionResult MapFailure(Error error)
    {
        if (error.Code.EndsWith(".NotFound", StringComparison.Ordinal))
            return NotFound(ApiResponse<object>.Fail(error.Description, error.Code));
        if (string.Equals(error.Code, "Conflict", StringComparison.Ordinal))
            return Conflict(ApiResponse<object>.Fail(error.Description, error.Code));

        return BadRequest(ApiResponse<object>.Fail(error.Description, error.Code));
    }

    private IActionResult MapFailure<T>(Error error)
    {
        if (error.Code.EndsWith(".NotFound", StringComparison.Ordinal))
            return NotFound(ApiResponse<T>.Fail(error.Description, error.Code));
        if (string.Equals(error.Code, "Conflict", StringComparison.Ordinal))
            return Conflict(ApiResponse<T>.Fail(error.Description, error.Code));

        return BadRequest(ApiResponse<T>.Fail(error.Description, error.Code));
    }
}

/// <summary>
/// Request body for cancelling a purchase order.
/// </summary>
public sealed record CancelPurchaseOrderRequest
{
    public string Reason { get; init; } = null!;
}

/// <summary>
/// Request body for creating a supplier invoice from a purchase order.
/// </summary>
public sealed record CreateSupplierInvoiceRequest
{
    public string InvoiceNumber { get; init; } = null!;
    public DateTime InvoiceDate { get; init; }
    public int PaymentTermDays { get; init; } = 30;
    public string? ExternalReference { get; init; }
    public string? Notes { get; init; }
    public bool? SendEmail { get; init; }
    public IReadOnlyList<SupplierInvoiceLineAssetRequest>? LineAssetClassifications { get; init; }
    /// <summary>Mode de paiement prévu (informatif), ex. « Effet de commerce ».</summary>
    public string? PaymentMethod { get; init; }
}
