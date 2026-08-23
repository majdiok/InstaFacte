using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.SalesReturnNotes.Commands;
using FactuTrust.Application.Features.SalesReturnNotes.Queries;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

[ApiController]
[Route("api/sales-return-notes")]
[Authorize]
public class SalesReturnNotesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<SalesReturnNotesController> _logger;

    public SalesReturnNotesController(IMediator mediator, ILogger<SalesReturnNotesController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.SalesReturnNotesRead)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<SalesReturnNoteListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSalesReturnNotes(
        [FromQuery] string? search,
        [FromQuery] SalesReturnNoteStatus? status,
        [FromQuery] Guid? clientId,
        [FromQuery] Guid? deliveryNoteId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetSalesReturnNotesQuery(search, status, clientId, deliveryNoteId, fromDate, toDate, page, pageSize),
            cancellationToken);
        return Ok(ApiResponse<PagedResult<SalesReturnNoteListDto>>.Ok(result));
    }

    [HttpGet("summary")]
    [Authorize(Policy = PermissionPolicies.SalesReturnNotesRead)]
    [ProducesResponseType(typeof(ApiResponse<SalesReturnNoteListSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(
        [FromQuery] string? search,
        [FromQuery] SalesReturnNoteStatus? status,
        [FromQuery] Guid? clientId,
        [FromQuery] Guid? deliveryNoteId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetSalesReturnNotesSummaryQuery(search, status, clientId, deliveryNoteId, fromDate, toDate),
            cancellationToken);
        return Ok(ApiResponse<SalesReturnNoteListSummaryDto>.Ok(result));
    }

    [HttpGet("eligible-delivery-notes")]
    [Authorize(Policy = PermissionPolicies.SalesReturnNotesRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EligibleDeliveryNoteDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEligibleDeliveryNotes(
        [FromQuery] Guid? clientId,
        [FromQuery] string? search,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetEligibleDeliveryNotesForReturnQuery(clientId, search),
            cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<EligibleDeliveryNoteDto>>.Ok(result));
    }

    [HttpGet("from-delivery-note/{deliveryNoteId:guid}")]
    [Authorize(Policy = PermissionPolicies.SalesReturnNotesCreate)]
    [ProducesResponseType(typeof(ApiResponse<SalesReturnNotePrefillDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PrefillFromDeliveryNote(
        Guid deliveryNoteId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetSalesReturnNotePrefillFromDeliveryNoteQuery(deliveryNoteId),
            cancellationToken);
        if (result.IsFailure)
            return MapFailure(result.Error);

        return Ok(ApiResponse<SalesReturnNotePrefillDto>.Ok(result.Value));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.SalesReturnNotesRead)]
    [ProducesResponseType(typeof(ApiResponse<SalesReturnNoteDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSalesReturnNote(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetSalesReturnNoteByIdQuery(id), cancellationToken);
        if (result.IsFailure)
            return MapFailure(result.Error);

        return Ok(ApiResponse<SalesReturnNoteDetailDto>.Ok(result.Value));
    }

    [HttpGet("{id:guid}/pdf")]
    [Authorize(Policy = PermissionPolicies.SalesReturnNotesRead)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportPdf(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ExportSalesReturnNotePdfQuery(id), cancellationToken);
        if (result.IsFailure)
            return MapFailure(result.Error);

        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.SalesReturnNotesCreate)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateSalesReturnNote(
        [FromBody] CreateSalesReturnNoteDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateSalesReturnNoteCommand(dto), cancellationToken);
        if (result.IsFailure)
            return MapFailure(result.Error);

        _logger.LogInformation("Sales return note created with ID {Id}", result.Value);
        return CreatedAtAction(
            nameof(GetSalesReturnNote),
            new { id = result.Value },
            ApiResponse<Guid>.Ok(result.Value, "Bon de retour créé avec succès."));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.SalesReturnNotesUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSalesReturnNote(
        Guid id,
        [FromBody] UpdateSalesReturnNoteDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdateSalesReturnNoteCommand(id, dto), cancellationToken);
        if (result.IsFailure)
            return MapFailure(result.Error);

        return Ok(ApiResponse<object>.Ok(null!, "Bon de retour mis à jour."));
    }

    [HttpPost("{id:guid}/confirm")]
    [Authorize(Policy = PermissionPolicies.SalesReturnNotesUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmSalesReturnNote(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ConfirmSalesReturnNoteCommand(id), cancellationToken);
        if (result.IsFailure)
            return MapFailure(result.Error);

        return Ok(ApiResponse<object>.Ok(null!, "Bon de retour confirmé. Le stock a été réintégré."));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.SalesReturnNotesDelete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSalesReturnNote(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteSalesReturnNoteCommand(id), cancellationToken);
        if (result.IsFailure)
            return MapFailure(result.Error);

        return Ok(ApiResponse<object>.Ok(null!, "Bon de retour supprimé."));
    }

    private IActionResult MapFailure(Error error)
    {
        if (error.Code.EndsWith(".NotFound", StringComparison.Ordinal))
            return NotFound(ApiResponse<object>.Fail(error.Description, error.Code));
        if (string.Equals(error.Code, "Conflict", StringComparison.Ordinal))
            return Conflict(ApiResponse<object>.Fail(error.Description, error.Code));

        return BadRequest(ApiResponse<object>.Fail(error.Description, error.Code));
    }
}
