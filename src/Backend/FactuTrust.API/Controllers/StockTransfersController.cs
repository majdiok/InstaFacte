using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.StockTransfers.Commands;
using FactuTrust.Application.Features.StockTransfers.Queries;
using FactuTrust.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StockTransfersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<StockTransfersController> _logger;

    public StockTransfersController(IMediator mediator, ILogger<StockTransfersController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.StockTransfersRead)]
    public async Task<IActionResult> GetStockTransfers(
        [FromQuery] StockTransferStatus? status,
        [FromQuery] Guid? warehouseId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetStockTransfersQuery(status, warehouseId, fromDate, toDate), cancellationToken);

        if (result.IsFailure)
            return BadRequest(new { error = result.Error.Description });

        return Ok(new { success = true, data = result.Value });
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.StockTransfersRead)]
    public async Task<IActionResult> GetStockTransfer(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetStockTransferDetailQuery(id), cancellationToken);

        if (result.IsFailure)
            return result.Error.Code.Contains("NotFound")
                ? NotFound(new { error = result.Error.Description })
                : BadRequest(new { error = result.Error.Description });

        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>
    /// Export stock transfer as PDF.
    /// </summary>
    [HttpGet("{id:guid}/pdf")]
    [Authorize(Policy = PermissionPolicies.StockTransfersRead)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportPdf(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ExportStockTransferPdfQuery(id), cancellationToken);

        if (result.IsFailure)
            return NotFound(new { error = result.Error.Description });

        var pdf = result.Value;
        return File(pdf.Content, pdf.ContentType, pdf.FileName);
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.StockTransfersCreate)]
    public async Task<IActionResult> CreateStockTransfer(
        [FromBody] CreateStockTransferDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateStockTransferCommand(dto), cancellationToken);

        if (result.IsFailure)
            return BadRequest(new { error = result.Error.Description });

        return CreatedAtAction(nameof(GetStockTransfer), new { id = result.Value },
            new { success = true, data = new { id = result.Value } });
    }

    [HttpPatch("{id:guid}/confirm")]
    [Authorize(Policy = PermissionPolicies.StockTransfersUpdate)]
    public async Task<IActionResult> ConfirmStockTransfer(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ConfirmStockTransferCommand(id), cancellationToken);

        if (result.IsFailure)
            return BadRequest(new { error = result.Error.Description });

        return Ok(new { success = true });
    }

    [HttpPatch("{id:guid}/start-transit")]
    [Authorize(Policy = PermissionPolicies.StockTransfersUpdate)]
    public async Task<IActionResult> StartTransitStockTransfer(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new StartTransitStockTransferCommand(id), cancellationToken);

        if (result.IsFailure)
            return BadRequest(new { error = result.Error.Description });

        return Ok(new { success = true });
    }

    [HttpPatch("{id:guid}/complete")]
    [Authorize(Policy = PermissionPolicies.StockTransfersUpdate)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CompleteStockTransfer(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CompleteStockTransferCommand(id), cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code.Equals("Conflict", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { error = result.Error.Description });
            return BadRequest(new { error = result.Error.Description });
        }

        return Ok(new { success = true });
    }

    [HttpPatch("{id:guid}/cancel")]
    [Authorize(Policy = PermissionPolicies.StockTransfersUpdate)]
    public async Task<IActionResult> CancelStockTransfer(
        Guid id,
        [FromBody] CancelStockTransferRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CancelStockTransferCommand(id, request.Reason), cancellationToken);

        if (result.IsFailure)
            return BadRequest(new { error = result.Error.Description });

        return Ok(new { success = true });
    }
}

public sealed record CancelStockTransferRequest
{
    public string Reason { get; init; } = null!;
}
