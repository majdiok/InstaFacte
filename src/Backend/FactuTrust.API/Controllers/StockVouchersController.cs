using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.StockVouchers.Commands;
using FactuTrust.Application.Features.StockVouchers.Queries;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StockVouchersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<StockVouchersController> _logger;

    public StockVouchersController(IMediator mediator, ILogger<StockVouchersController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.StockVouchersRead)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<StockVoucherListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStockVouchers(
        [FromQuery] StockVoucherKind? kind,
        [FromQuery] string? search,
        [FromQuery] StockVoucherStatus? status,
        [FromQuery] Guid? warehouseId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetStockVouchersQuery(kind, search, status, warehouseId, fromDate, toDate, page, pageSize),
            cancellationToken);
        return Ok(ApiResponse<PagedResult<StockVoucherListDto>>.Ok(result));
    }

    [HttpGet("summary")]
    [Authorize(Policy = PermissionPolicies.StockVouchersRead)]
    [ProducesResponseType(typeof(ApiResponse<StockVoucherListSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(
        [FromQuery] StockVoucherKind? kind,
        [FromQuery] string? search,
        [FromQuery] StockVoucherStatus? status,
        [FromQuery] Guid? warehouseId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetStockVouchersSummaryQuery(kind, search, status, warehouseId, fromDate, toDate),
            cancellationToken);
        return Ok(ApiResponse<StockVoucherListSummaryDto>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.StockVouchersRead)]
    [ProducesResponseType(typeof(ApiResponse<StockVoucherDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStockVoucher(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetStockVoucherByIdQuery(id), cancellationToken);
        if (result.IsFailure)
            return MapFailure(result.Error);

        return Ok(ApiResponse<StockVoucherDetailDto>.Ok(result.Value));
    }

    [HttpGet("{id:guid}/pdf")]
    [Authorize(Policy = PermissionPolicies.StockVouchersRead)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportPdf(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ExportStockVoucherPdfQuery(id), cancellationToken);
        if (result.IsFailure)
            return MapFailure(result.Error);

        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.StockVouchersCreate)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateStockVoucher(
        [FromBody] CreateStockVoucherDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateStockVoucherCommand(dto), cancellationToken);
        if (result.IsFailure)
            return MapFailure(result.Error);

        _logger.LogInformation("Stock voucher created {StockVoucherId}", result.Value);
        return CreatedAtAction(
            nameof(GetStockVoucher),
            new { id = result.Value },
            ApiResponse<Guid>.Ok(result.Value, "Bon de stock créé avec succès."));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.StockVouchersUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateStockVoucher(
        Guid id,
        [FromBody] UpdateStockVoucherDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdateStockVoucherCommand(id, dto), cancellationToken);
        if (result.IsFailure)
            return MapFailure(result.Error);

        return Ok(ApiResponse<object>.Ok(null!, "Bon de stock mis à jour."));
    }

    [HttpPatch("{id:guid}/validate")]
    [Authorize(Policy = PermissionPolicies.StockVouchersUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ValidateStockVoucher(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ValidateStockVoucherCommand(id), cancellationToken);
        if (result.IsFailure)
            return MapFailure(result.Error);

        return Ok(ApiResponse<object>.Ok(null!, "Bon de stock validé. Le stock a été mis à jour."));
    }

    [HttpPost("{id:guid}/validate")]
    [Authorize(Policy = PermissionPolicies.StockVouchersUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ValidateStockVoucherWithAllocations(
        Guid id,
        [FromBody] IReadOnlyList<StockVoucherLineAllocationsDto>? lineAllocations,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ValidateStockVoucherCommand(id, lineAllocations), cancellationToken);
        if (result.IsFailure)
            return MapFailure(result.Error);

        return Ok(ApiResponse<object>.Ok(null!, "Bon de stock validé. Le stock a été mis à jour."));
    }

    [HttpPatch("{id:guid}/cancel")]
    [Authorize(Policy = PermissionPolicies.StockVouchersUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelStockVoucher(
        Guid id,
        [FromBody] CancelStockVoucherDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CancelStockVoucherCommand(id, dto.Reason), cancellationToken);
        if (result.IsFailure)
            return MapFailure(result.Error);

        return Ok(ApiResponse<object>.Ok(null!, "Bon de stock annulé."));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.StockVouchersDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteStockVoucher(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteStockVoucherCommand(id), cancellationToken);
        if (result.IsFailure)
            return MapFailure(result.Error);

        return NoContent();
    }

    private IActionResult MapFailure(Error error)
    {
        if (error.Code.EndsWith(".NotFound", StringComparison.Ordinal) ||
            error.Code.Contains("NotFound", StringComparison.Ordinal))
            return NotFound(ApiResponse<object>.Fail(error.Description, error.Code));

        if (string.Equals(error.Code, "Conflict", StringComparison.Ordinal))
            return Conflict(ApiResponse<object>.Fail(error.Description, error.Code));

        return BadRequest(ApiResponse<object>.Fail(error.Description, error.Code));
    }
}
