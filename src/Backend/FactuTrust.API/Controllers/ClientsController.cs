using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Clients.Commands;
using FactuTrust.Application.Features.Clients.Queries;
using FactuTrust.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Controller for managing clients (CRM).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ClientsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ClientsController> _logger;

    public ClientsController(IMediator mediator, ILogger<ClientsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Get paginated list of clients with optional search and filters.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.ClientsRead)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ClientListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClients(
        [FromQuery] string? search,
        [FromQuery] ClientType? type,
        [FromQuery] bool? isActive,
        [FromQuery] string? governorate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetClientsQuery(search, type, isActive, governorate, page, pageSize);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<ClientListDto>>.Ok(result));
    }

    /// <summary>
    /// Get aggregated totals for the client list (same filters, computed over the entire
    /// filtered set — not just the current page) for the totals zone.
    /// </summary>
    [HttpGet("summary")]
    [Authorize(Policy = PermissionPolicies.ClientsRead)]
    [ProducesResponseType(typeof(ApiResponse<ClientListSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClientsSummary(
        [FromQuery] string? search,
        [FromQuery] ClientType? type,
        [FromQuery] bool? isActive,
        [FromQuery] string? governorate,
        CancellationToken cancellationToken = default)
    {
        var query = new GetClientsSummaryQuery(search, type, isActive, governorate);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<ClientListSummaryDto>.Ok(result));
    }

    /// <summary>
    /// Get client details by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.ClientsRead)]
    [ProducesResponseType(typeof(ApiResponse<ClientDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClient(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetClientByIdQuery(id);
        var result = await _mediator.Send(query, cancellationToken);

        if (result.IsFailure)
            return NotFound(ApiResponse<ClientDetailDto>.Fail(result.Error.Description));

        return Ok(ApiResponse<ClientDetailDto>.Ok(result.Value));
    }

    /// <summary>
    /// Get client statistics (invoices, quotes, revenue).
    /// </summary>
    [HttpGet("{id:guid}/stats")]
    [Authorize(Policy = PermissionPolicies.ClientsRead)]
    [ProducesResponseType(typeof(ApiResponse<ClientStatsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClientStats(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetClientStatsQuery(id);
        var result = await _mediator.Send(query, cancellationToken);

        if (result.IsFailure)
            return NotFound(ApiResponse<ClientStatsDto>.Fail(result.Error.Description));

        return Ok(ApiResponse<ClientStatsDto>.Ok(result.Value));
    }

    /// <summary>
    /// Create a new client.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = PermissionPolicies.ClientsCreate)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateClient(
        [FromBody] CreateClientDto dto,
        CancellationToken cancellationToken)
    {
        var command = new CreateClientCommand(dto);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "Conflict")
                return Conflict(ApiResponse<Guid>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        }

        _logger.LogInformation("Client created with ID {ClientId}", result.Value);

        return CreatedAtAction(
            nameof(GetClient),
            new { id = result.Value },
            ApiResponse<Guid>.Ok(result.Value, "Client créé. Vous pouvez maintenant créer un devis ou une facture."));
    }

    /// <summary>
    /// Update an existing client.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.ClientsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<ClientDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateClient(
        Guid id,
        [FromBody] UpdateClientDto dto,
        CancellationToken cancellationToken)
    {
        var command = new UpdateClientCommand(id, dto);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "NotFound" || result.Error.Code == "Client.NotFound")
                return NotFound(ApiResponse<ClientDetailDto>.Fail(result.Error.Description));
            if (result.Error.Code == "Conflict")
                return Conflict(ApiResponse<ClientDetailDto>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<ClientDetailDto>.Fail(result.Error.Description));
        }

        return Ok(ApiResponse<ClientDetailDto>.Ok(result.Value, "Client mis à jour."));
    }

    /// <summary>
    /// Delete a client. Fails if client has invoices or quotes.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.ClientsDelete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteClient(Guid id, CancellationToken cancellationToken)
    {
        var command = new DeleteClientCommand(id);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "NotFound" || result.Error.Code == "Client.NotFound")
                return NotFound(ApiResponse<object>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        }

        return Ok(ApiResponse<object>.Ok(null!, "Client supprimé."));
    }

    /// <summary>
    /// Toggle client active status.
    /// </summary>
    [HttpPatch("{id:guid}/toggle-active")]
    [Authorize(Policy = PermissionPolicies.ClientsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<ClientDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleActive(Guid id, CancellationToken cancellationToken)
    {
        var command = new ToggleClientActiveCommand(id);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
            return NotFound(ApiResponse<ClientDetailDto>.Fail(result.Error.Description));

        return Ok(ApiResponse<ClientDetailDto>.Ok(result.Value,
            result.Value.IsActive ? "Client activé." : "Client désactivé."));
    }
}
