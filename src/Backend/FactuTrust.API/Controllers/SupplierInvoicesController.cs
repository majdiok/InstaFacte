using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.SupplierInvoices.Commands;
using FactuTrust.Application.Features.SupplierInvoices.Queries;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Controller for managing supplier invoices (factures fournisseurs).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SupplierInvoicesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<SupplierInvoicesController> _logger;
    private readonly ISupplierInvoiceNumberService _supplierInvoiceNumberService;

    public SupplierInvoicesController(
        IMediator mediator,
        ILogger<SupplierInvoicesController> logger,
        ISupplierInvoiceNumberService supplierInvoiceNumberService)
    {
        _mediator = mediator;
        _logger = logger;
        _supplierInvoiceNumberService = supplierInvoiceNumberService;
    }

    /// <summary>
    /// Get paginated list of supplier invoices with optional filters.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.SupplierInvoicesRead)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<SupplierInvoiceListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSupplierInvoices(
        [FromQuery] string? search,
        [FromQuery] SupplierInvoiceStatus? status,
        [FromQuery] Guid? supplierId,
        [FromQuery] Guid? purchaseOrderId,
        [FromQuery] Guid? purchaseReceiptId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool unpaidOnly = false,
        CancellationToken cancellationToken = default)
    {
        var query = new GetSupplierInvoicesQuery(
            search, status, supplierId, purchaseOrderId, purchaseReceiptId, fromDate, toDate, page, pageSize, unpaidOnly);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<SupplierInvoiceListDto>>.Ok(result));
    }

    /// <summary>
    /// Get aggregated totals for the supplier invoice list (same filters, computed over the
    /// entire filtered set — not just the current page) for the totals zone.
    /// </summary>
    [HttpGet("summary")]
    [Authorize(Policy = PermissionPolicies.SupplierInvoicesRead)]
    [ProducesResponseType(typeof(ApiResponse<SupplierInvoiceListSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSupplierInvoicesSummary(
        [FromQuery] string? search,
        [FromQuery] SupplierInvoiceStatus? status,
        [FromQuery] Guid? supplierId,
        [FromQuery] Guid? purchaseOrderId,
        [FromQuery] Guid? purchaseReceiptId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] bool unpaidOnly = false,
        CancellationToken cancellationToken = default)
    {
        var query = new GetSupplierInvoicesSummaryQuery(
            search, status, supplierId, purchaseOrderId, purchaseReceiptId, fromDate, toDate, unpaidOnly);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<SupplierInvoiceListSummaryDto>.Ok(result));
    }

    /// <summary>
    /// Preview the next available internal supplier invoice number (does not consume the sequence).
    /// </summary>
    [HttpGet("preview-number")]
    [Authorize(Policy = PermissionPolicies.SupplierInvoicesCreate)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewNumber(
        [FromQuery] DateTime? invoiceDate,
        CancellationToken cancellationToken)
    {
        var date = invoiceDate ?? DateTime.UtcNow;
        var number = await _supplierInvoiceNumberService.PreviewNextAsync(date, cancellationToken);
        return Ok(ApiResponse<string>.Ok(number));
    }

    /// <summary>
    /// Create a standalone supplier invoice (no purchase order / receipt). Stock is not updated.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = PermissionPolicies.SupplierInvoicesCreate)]
    [ProducesResponseType(typeof(ApiResponse<SupplierInvoiceCreationResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateStandalone(
        [FromBody] CreateStandaloneSupplierInvoiceDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateStandaloneSupplierInvoiceCommand(dto), cancellationToken);
        if (result.IsFailure)
            return MapFailure(result.Error);

        var payload = new SupplierInvoiceCreationResponse(result.Value.Id, result.Value.InvoiceNumber);
        _logger.LogInformation(
            "Standalone supplier invoice created: {InvoiceId} ({InvoiceNumber})",
            payload.Id, payload.InvoiceNumber);

        return CreatedAtAction(
            nameof(GetSupplierInvoice),
            new { id = payload.Id },
            ApiResponse<SupplierInvoiceCreationResponse>.Ok(payload, "Facture fournisseur créée avec succès"));
    }

    /// <summary>
    /// Get supplier invoice details by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.SupplierInvoicesRead)]
    [ProducesResponseType(typeof(ApiResponse<SupplierInvoiceDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSupplierInvoice(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetSupplierInvoiceByIdQuery(id);
        var result = await _mediator.Send(query, cancellationToken);

        if (result.IsFailure)
            return NotFound(ApiResponse<SupplierInvoiceDetailDto>.Fail(result.Error.Description));

        return Ok(ApiResponse<SupplierInvoiceDetailDto>.Ok(result.Value));
    }

    /// <summary>
    /// Get all payments for a supplier invoice.
    /// </summary>
    [HttpGet("{id:guid}/payments")]
    [Authorize(Policy = PermissionPolicies.SupplierInvoicesRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SupplierPaymentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPayments(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetSupplierPaymentsQuery(id);
        var result = await _mediator.Send(query, cancellationToken);

        if (result.IsFailure)
            return NotFound(ApiResponse<IReadOnlyList<SupplierPaymentDto>>.Fail(result.Error.Description));

        return Ok(ApiResponse<IReadOnlyList<SupplierPaymentDto>>.Ok(result.Value));
    }

    /// <summary>
    /// Export a supplier invoice (facture d'achat) as PDF.
    /// </summary>
    [HttpGet("{id:guid}/pdf")]
    [Authorize(Policy = PermissionPolicies.SupplierInvoicesRead)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportPdf(Guid id, [FromQuery] string? templateKey, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ExportSupplierInvoicePdfQuery(id, templateKey), cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description));

        var pdf = result.Value;
        return File(pdf.Content, pdf.ContentType, pdf.FileName);
    }

    /// <summary>
    /// Record a payment (tranche) on a supplier invoice.
    /// Supports multiple partial payments per invoice.
    /// </summary>
    [HttpPost("{id:guid}/record-payment")]
    [Authorize(Policy = PermissionPolicies.PaymentsCreate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordPayment(
        Guid id,
        [FromBody] RecordSupplierPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new RecordSupplierPaymentCommand(id, request);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "NotFound")
                return NotFound(ApiResponse<object>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        }

        _logger.LogInformation("Payment recorded for supplier invoice {SupplierInvoiceId}", id);
        return Ok(ApiResponse<object>.Ok(null!, "Paiement enregistré avec succès."));
    }

    /// <summary>
    /// Règle (paie) un effet de commerce (traite) fournisseur à échéance.
    /// </summary>
    [HttpPost("{id:guid}/payments/{paymentId:guid}/settle-effet")]
    [Authorize(Policy = PermissionPolicies.PaymentsCreate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SettleEffet(
        Guid id,
        Guid paymentId,
        [FromBody] SettleSupplierEffetRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new SettleSupplierEffetCommand(paymentId, request);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "NotFound")
                return NotFound(ApiResponse<object>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        }

        _logger.LogInformation("Effet settled for supplier payment {PaymentId}", paymentId);
        return Ok(ApiResponse<object>.Ok(null!, "Effet réglé avec succès."));
    }

    /// <summary>
    /// Cancel a supplier invoice.
    /// </summary>
    [HttpPatch("{id:guid}/cancel")]
    [Authorize(Policy = PermissionPolicies.SupplierInvoicesUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromBody] CancelSupplierInvoiceRequest body,
        CancellationToken cancellationToken = default)
    {
        var command = new CancelSupplierInvoiceCommand(id, body.Reason);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "NotFound")
                return NotFound(ApiResponse<object>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        }

        _logger.LogInformation("Supplier invoice {SupplierInvoiceId} cancelled", id);
        return Ok(ApiResponse<object>.Ok(null!, "Facture fournisseur annulée."));
    }

    private IActionResult MapFailure(Error error)
    {
        if (error.Code.EndsWith(".NotFound", StringComparison.Ordinal))
            return NotFound(ApiResponse<object>.Fail(error.Description, error.Code));
        if (string.Equals(error.Code, "Conflict", StringComparison.Ordinal))
        {
            var response = ApiResponse<object>.Fail(error.Description, error.Code);
            if (error.Metadata is { Count: > 0 })
                response = response with { Data = error.Metadata };
            return Conflict(response);
        }

        return BadRequest(ApiResponse<object>.Fail(error.Description, error.Code));
    }
}

/// <summary>
/// Request body for cancelling a supplier invoice.
/// </summary>
public sealed record CancelSupplierInvoiceRequest
{
    public string Reason { get; init; } = null!;
}
