using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Suppliers.Commands;
using FactuTrust.Application.Features.Suppliers.Queries;
using FactuTrust.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Controller for managing suppliers (fournisseurs).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SuppliersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<SuppliersController> _logger;

    public SuppliersController(IMediator mediator, ILogger<SuppliersController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Get paginated list of suppliers with optional search and filters.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.SuppliersRead)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<SupplierListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSuppliers(
        [FromQuery] string? search,
        [FromQuery] SupplierType? type,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetSuppliersQuery(search, type, isActive, page, pageSize);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<SupplierListDto>>.Ok(result));
    }

    /// <summary>
    /// Get aggregated totals for the supplier list (same filters, computed over the entire
    /// filtered set — not just the current page) for the totals zone.
    /// </summary>
    [HttpGet("summary")]
    [Authorize(Policy = PermissionPolicies.SuppliersRead)]
    [ProducesResponseType(typeof(ApiResponse<SupplierListSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSuppliersSummary(
        [FromQuery] string? search,
        [FromQuery] SupplierType? type,
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var query = new GetSuppliersSummaryQuery(search, type, isActive);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<SupplierListSummaryDto>.Ok(result));
    }

    /// <summary>
    /// Get supplier details by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.SuppliersRead)]
    [ProducesResponseType(typeof(ApiResponse<SupplierDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSupplier(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetSupplierByIdQuery(id);
        var result = await _mediator.Send(query, cancellationToken);

        if (result.IsFailure)
            return NotFound(ApiResponse<SupplierDetailDto>.Fail(result.Error.Description));

        return Ok(ApiResponse<SupplierDetailDto>.Ok(result.Value));
    }

    /// <summary>
    /// Create a new supplier.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = PermissionPolicies.SuppliersCreate)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateSupplier(
        [FromBody] CreateSupplierDto dto,
        CancellationToken cancellationToken)
    {
        var command = new CreateSupplierCommand(dto);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "Conflict")
                return Conflict(ApiResponse<Guid>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        }

        _logger.LogInformation("Supplier created with ID {SupplierId}", result.Value);

        return CreatedAtAction(
            nameof(GetSupplier),
            new { id = result.Value },
            ApiResponse<Guid>.Ok(result.Value, "Fournisseur créé avec succès."));
    }

    /// <summary>
    /// Update an existing supplier.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.SuppliersUpdate)]
    [ProducesResponseType(typeof(ApiResponse<SupplierDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSupplier(
        Guid id,
        [FromBody] UpdateSupplierDto dto,
        CancellationToken cancellationToken)
    {
        var command = new UpdateSupplierCommand(id, dto);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "NotFound")
                return NotFound(ApiResponse<SupplierDetailDto>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<SupplierDetailDto>.Fail(result.Error.Description));
        }

        return Ok(ApiResponse<SupplierDetailDto>.Ok(result.Value, "Fournisseur mis à jour."));
    }

    /// <summary>
    /// Toggle supplier active status.
    /// </summary>
    [HttpPatch("{id:guid}/toggle-active")]
    [Authorize(Policy = PermissionPolicies.SuppliersUpdate)]
    [ProducesResponseType(typeof(ApiResponse<SupplierDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleActive(Guid id, CancellationToken cancellationToken)
    {
        var command = new ToggleSupplierCommand(id);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
            return NotFound(ApiResponse<SupplierDetailDto>.Fail(result.Error.Description));

        return Ok(ApiResponse<SupplierDetailDto>.Ok(result.Value,
            result.Value.IsActive ? "Fournisseur activé." : "Fournisseur désactivé."));
    }

    /// <summary>
    /// Delete a supplier (only if no purchase orders exist).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.SuppliersDelete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSupplier(Guid id, CancellationToken cancellationToken)
    {
        var command = new DeleteSupplierCommand(id);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "NotFound")
                return NotFound(ApiResponse<object>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        }

        _logger.LogInformation("Supplier {SupplierId} deleted", id);

        return Ok(ApiResponse<object>.Ok(null!, "Fournisseur supprimé."));
    }
}
