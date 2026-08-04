using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Quotes.Commands;
using FactuTrust.Application.Features.Quotes.Queries;
using FactuTrust.Application.Features.SalesOrders.Commands;
using FactuTrust.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Controller for managing quotes (devis).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class QuotesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<QuotesController> _logger;

    public QuotesController(IMediator mediator, ILogger<QuotesController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Get paginated list of quotes with optional filtering.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.QuotesRead)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<QuoteListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQuotes(
        [FromQuery] string? search,
        [FromQuery] QuoteStatus? status,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] Guid? clientId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetQuotesQuery(search, status, fromDate, toDate, clientId, page, pageSize);
        var result = await _mediator.Send(query, cancellationToken);

        return Ok(ApiResponse<PagedResult<QuoteListDto>>.Ok(result));
    }

    /// <summary>
    /// Get aggregated totals for the quote list (same filters, computed over the entire
    /// filtered set — not just the current page) for the totals zone.
    /// </summary>
    [HttpGet("summary")]
    [Authorize(Policy = PermissionPolicies.QuotesRead)]
    [ProducesResponseType(typeof(ApiResponse<QuoteListSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQuotesSummary(
        [FromQuery] string? search,
        [FromQuery] QuoteStatus? status,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] Guid? clientId,
        CancellationToken cancellationToken = default)
    {
        var query = new GetQuotesSummaryQuery(search, status, fromDate, toDate, clientId);
        var result = await _mediator.Send(query, cancellationToken);

        return Ok(ApiResponse<QuoteListSummaryDto>.Ok(result));
    }

    /// <summary>
    /// Get quote details by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.QuotesRead)]
    [ProducesResponseType(typeof(ApiResponse<QuoteDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetQuote(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetQuoteByIdQuery(id);
        var result = await _mediator.Send(query, cancellationToken);

        if (result.IsFailure)
            return NotFound(ApiResponse<QuoteDetailDto>.Fail(result.Error.Description));

        return Ok(ApiResponse<QuoteDetailDto>.Ok(result.Value));
    }

    /// <summary>
    /// Export quote as PDF.
    /// </summary>
    [HttpGet("{id:guid}/pdf")]
    [Authorize(Policy = PermissionPolicies.QuotesRead)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportPdf(Guid id, [FromQuery] string? templateKey, CancellationToken cancellationToken = default)
    {
        var query = new ExportQuotePdfQuery(id, templateKey);
        var result = await _mediator.Send(query, cancellationToken);

        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description));

        var pdf = result.Value;
        return File(pdf.Content, pdf.ContentType, pdf.FileName);
    }

    /// <summary>
    /// Create a new quote.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = PermissionPolicies.QuotesCreate)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateQuote(
        [FromBody] CreateQuoteDto dto,
        CancellationToken cancellationToken)
    {
        var command = new CreateQuoteCommand(dto);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
            return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));

        _logger.LogInformation("Quote created with ID {QuoteId}", result.Value);
        
        return CreatedAtAction(
            nameof(GetQuote), 
            new { id = result.Value }, 
            ApiResponse<Guid>.Ok(result.Value, "Devis créé avec succès"));
    }

    /// <summary>
    /// Send a quote to the client.
    /// </summary>
    [HttpPost("{id:guid}/send")]
    [Authorize(Policy = PermissionPolicies.QuotesUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendQuote(Guid id, CancellationToken cancellationToken)
    {
        var command = new SendQuoteCommand(id);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<object>.Ok(null!, "Devis envoyé avec succès"));
    }

    /// <summary>
    /// Accept a quote (mark as accepted).
    /// </summary>
    [HttpPost("{id:guid}/accept")]
    [Authorize(Policy = PermissionPolicies.QuotesUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AcceptQuote(Guid id, CancellationToken cancellationToken)
    {
        var command = new AcceptQuoteCommand(id);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<object>.Ok(null!, "Devis accepté avec succès"));
    }

    /// <summary>
    /// Reject a quote.
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = PermissionPolicies.QuotesUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RejectQuote(
        Guid id,
        [FromBody] RejectQuoteDto? dto,
        CancellationToken cancellationToken)
    {
        var command = new RejectQuoteCommand(id, dto?.Reason);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<object>.Ok(null!, "Devis refusé"));
    }

    /// <summary>
    /// Convert an accepted quote to an invoice.
    /// This is a critical operation that creates a new invoice from the quote.
    /// </summary>
    [HttpPost("{id:guid}/convert-to-invoice")]
    [Authorize(Policy = PermissionPolicies.InvoicesCreate)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConvertToInvoice(
        Guid id,
        [FromBody] ConvertQuoteToInvoiceDto? options,
        CancellationToken cancellationToken)
    {
        var command = new ConvertQuoteToInvoiceCommand(id, options);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "Conflict")
                return Conflict(ApiResponse<Guid>.Fail(result.Error.Description));
            
            return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        }

        _logger.LogInformation("Quote {QuoteId} converted to invoice {InvoiceId}", id, result.Value);
        
        return Ok(ApiResponse<Guid>.Ok(result.Value, "Facture créée à partir du devis avec succès"));
    }

    /// <summary>
    /// Transforme un devis accepté en commande client — miroir de la conversion en facture.
    /// Le devis est verrouillé au passage (statut Converted) : plus aucune autre conversion
    /// n'est possible ensuite.
    /// </summary>
    [HttpPost("{id:guid}/convert-to-sales-order")]
    [Authorize(Policy = PermissionPolicies.SalesOrdersCreate)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConvertToSalesOrder(
        Guid id,
        [FromBody] ConvertQuoteToSalesOrderDto? options,
        CancellationToken cancellationToken)
    {
        var command = new ConvertQuoteToSalesOrderCommand(
            id,
            options?.OrderDate,
            options?.ExpectedDeliveryDate,
            options?.WarehouseId);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "Conflict")
                return Conflict(ApiResponse<Guid>.Fail(result.Error.Description));

            return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        }

        _logger.LogInformation("Quote {QuoteId} converted to sales order {SalesOrderId}", id, result.Value);

        return Ok(ApiResponse<Guid>.Ok(result.Value, "Commande créée à partir du devis avec succès"));
    }

    /// <summary>
    /// Cancel a quote.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = PermissionPolicies.QuotesUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CancelQuote(
        Guid id,
        [FromBody] CancelQuoteDto dto,
        CancellationToken cancellationToken)
    {
        var command = new CancelQuoteCommand(id, dto.Reason);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<object>.Ok(null!, "Devis annulé"));
    }

    /// <summary>
    /// Duplicate a quote. Creates a new draft with same client and lines.
    /// </summary>
    [HttpPost("{id:guid}/duplicate")]
    [Authorize(Policy = PermissionPolicies.QuotesCreate)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DuplicateQuote(Guid id, CancellationToken cancellationToken = default)
    {
        var command = new DuplicateQuoteCommand(id);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "NotFound")
                return NotFound(ApiResponse<Guid>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        }

        _logger.LogInformation("Quote {SourceId} duplicated as {NewId}", id, result.Value);
        return CreatedAtAction(
            nameof(GetQuote),
            new { id = result.Value },
            ApiResponse<Guid>.Ok(result.Value, "Devis dupliqué avec succès"));
    }
}

/// <summary>
/// DTO for rejecting a quote.
/// </summary>
public sealed record RejectQuoteDto(string? Reason);

/// <summary>
/// DTO for cancelling a quote.
/// </summary>
public sealed record CancelQuoteDto(string Reason);
