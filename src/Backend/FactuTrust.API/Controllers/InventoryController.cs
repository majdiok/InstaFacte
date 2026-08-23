using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Inventory.Commands;
using FactuTrust.Application.Features.Inventory.Queries;
using FactuTrust.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Controller pour la gestion des inventaires physiques.
/// API simple et pédagogique pour les utilisateurs non techniciens.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantContext _tenantContext;

    public InventoryController(
        IMediator mediator,
        ICurrentUser currentUser,
        ITenantContext tenantContext)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Démarre un nouvel inventaire physique.
    /// </summary>
    /// <param name="request">Type d'inventaire (complet ou partiel) et produits optionnels</param>
    [HttpPost("start")]
    [Authorize(Policy = PermissionPolicies.InventoryCreate)]
    [ProducesResponseType(typeof(ApiResponse<StartInventoryResult>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StartInventory([FromBody] StartInventoryRequest request)
    {
        var command = new StartInventoryCommand(
            request.Type,
            request.WarehouseId,
            request.ProductIds,
            request.Notes);

        var result = await _mediator.Send(command);

        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return CreatedAtAction(
            nameof(GetActiveInventory),
            new { },
            ApiResponse<StartInventoryResult>.Ok(result.Value, result.Value.HumanMessage));
    }

    /// <summary>
    /// Liste paginée des inventaires avec filtres optionnels.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.InventoryRead)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PhysicalInventoryListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInventories(
        [FromQuery] string? search = null,
        [FromQuery] InventoryStatus? status = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetPhysicalInventoriesQuery(search, status, fromDate, toDate, warehouseId, page, pageSize);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<PhysicalInventoryListDto>>.Ok(result));
    }

    /// <summary>
    /// Totaux agrégés de la liste des inventaires (mêmes filtres, sur l'ensemble filtré complet).
    /// </summary>
    [HttpGet("list-summary")]
    [Authorize(Policy = PermissionPolicies.InventoryRead)]
    [ProducesResponseType(typeof(ApiResponse<InventoryListSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInventoriesSummary(
        [FromQuery] string? search = null,
        [FromQuery] InventoryStatus? status = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] Guid? warehouseId = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetInventoriesSummaryQuery(search, status, fromDate, toDate, warehouseId);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<InventoryListSummaryDto>.Ok(result));
    }

    /// <summary>
    /// Récupère un inventaire par id (consultation).
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.InventoryRead)]
    [ProducesResponseType(typeof(ApiResponse<PhysicalInventoryDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInventoryById([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var query = new GetPhysicalInventoryByIdQuery(id);
        var result = await _mediator.Send(query, cancellationToken);

        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<PhysicalInventoryDetailDto>.Ok(result.Value));
    }

    /// <summary>
    /// Récupère l'inventaire actif (en cours) s'il existe.
    /// </summary>
    [HttpGet("active")]
    [Authorize(Policy = PermissionPolicies.InventoryRead)]
    [ProducesResponseType(typeof(ApiResponse<ActiveInventoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveInventory([FromQuery] Guid? warehouseId = null)
    {
        var query = new GetActiveInventoryQuery(warehouseId);
        var result = await _mediator.Send(query);

        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        if (result.Value == null)
            return Ok(ApiResponse<ActiveInventoryDto?>.Ok(null, "Aucun inventaire en cours."));

        return Ok(ApiResponse<ActiveInventoryDto>.Ok(result.Value, result.Value.ProgressMessage));
    }

    /// <summary>
    /// Enregistre le comptage d'un produit.
    /// </summary>
    [HttpPost("{inventoryId:guid}/count")]
    [Authorize(Policy = PermissionPolicies.InventoryUpdate)]
    [ProducesResponseType(typeof(ApiResponse<RecordCountResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RecordCount(
        [FromRoute] Guid inventoryId,
        [FromBody] RecordCountRequest request)
    {
        var command = new RecordCountCommand(inventoryId, request.ProductId, request.CountedQuantity, request.ProductLotId);
        var result = await _mediator.Send(command);

        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<RecordCountResult>.Ok(result.Value, result.Value.HumanMessage));
    }

    /// <summary>
    /// Récupère le résumé pédagogique de l'inventaire avant validation.
    /// </summary>
    [HttpGet("{inventoryId:guid}/summary")]
    [Authorize(Policy = PermissionPolicies.InventoryRead)]
    [ProducesResponseType(typeof(ApiResponse<InventorySummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSummary([FromRoute] Guid inventoryId)
    {
        var query = new GetInventorySummaryQuery(inventoryId);
        var result = await _mediator.Send(query);

        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<InventorySummaryDto>.Ok(result.Value, result.Value.StatusMessage));
    }

    /// <summary>
    /// Valide l'inventaire et applique les ajustements de stock.
    /// </summary>
    [HttpPost("{inventoryId:guid}/validate")]
    [Authorize(Policy = PermissionPolicies.InventoryUpdate)]
    [ProducesResponseType(typeof(ApiResponse<ValidateInventoryResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ValidateInventory(
        [FromRoute] Guid inventoryId,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)]
        ValidateInventoryRequest? request = null)
    {
        var pendingCounts = request?.PendingCounts?
            .Select(c => new InventoryPendingCount(c.ProductId, c.CountedQuantity, c.ProductLotId))
            .ToList();
        var command = new ValidateInventoryCommand(inventoryId, pendingCounts);
        var result = await _mediator.Send(command);

        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<ValidateInventoryResult>.Ok(result.Value, result.Value.HumanMessage));
    }

    /// <summary>
    /// Annule l'inventaire en cours sans modifier le stock.
    /// </summary>
    [HttpPost("{inventoryId:guid}/cancel")]
    [Authorize(Policy = PermissionPolicies.InventoryUpdate)]
    [ProducesResponseType(typeof(ApiResponse<CancelInventoryResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CancelInventory([FromRoute] Guid inventoryId)
    {
        var command = new CancelInventoryCommand(inventoryId);
        var result = await _mediator.Send(command);

        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<CancelInventoryResult>.Ok(result.Value, result.Value.HumanMessage));
    }
}

/// <summary>
/// Request DTO pour démarrer un inventaire.
/// </summary>
public sealed record StartInventoryRequest
{
    public InventoryType Type { get; init; } = InventoryType.Complete;
    public Guid? WarehouseId { get; init; }
    public List<Guid>? ProductIds { get; init; }
    public string? Notes { get; init; }
}

/// <summary>
/// Request DTO pour enregistrer un comptage.
/// </summary>
public sealed record RecordCountRequest
{
    public Guid ProductId { get; init; }
    public decimal CountedQuantity { get; init; }
    public Guid? ProductLotId { get; init; }
}

/// <summary>
/// Request DTO pour valider un inventaire, avec comptages non encore persistés.
/// </summary>
public sealed record ValidateInventoryRequest
{
    public List<InventoryPendingCountRequest>? PendingCounts { get; init; }
}

public sealed record InventoryPendingCountRequest
{
    public Guid ProductId { get; init; }
    public decimal CountedQuantity { get; init; }
    public Guid? ProductLotId { get; init; }
}

/// <summary>
/// Response wrapper standard avec message pédagogique.
/// </summary>
public sealed record ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? Message { get; init; }
    public string? Error { get; init; }

    public static ApiResponse<T> Ok(T data, string? message = null) => new()
    {
        Success = true,
        Data = data,
        Message = message
    };

    public static ApiResponse<T> Fail(string error, string? code= null) => new()
    {
        Success = false,
        Error = error
    };
}
