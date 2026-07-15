using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Stock.Commands;
using FactuTrust.Application.Features.Stock.Queries;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StockController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantMigrationGuard _migrationGuard;

    public StockController(
        IMediator mediator,
        ICurrentUser currentUser,
        ITenantContext tenantContext,
        ITenantMigrationGuard migrationGuard)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _migrationGuard = migrationGuard;
    }

    #region Stock Items

    [HttpGet("items")]
    [Authorize(Policy = PermissionPolicies.StockRead)]
    public async Task<ActionResult<StockItemsResult>> GetStockItems(
        [FromQuery] Guid? warehouseId,
        [FromQuery] bool? lowStockOnly,
        [FromQuery] bool? outOfStockOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var guardResult = await EnsureStockSchemaAsync(cancellationToken);
        if (guardResult != null)
            return guardResult;

        var query = new GetStockItemsQuery(
            warehouseId,
            lowStockOnly,
            outOfStockOnly,
            page,
            pageSize);

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<StockItemsResult>.Ok(result));
    }

    [HttpGet("alerts")]
    [Authorize(Policy = PermissionPolicies.StockRead)]
    public async Task<ActionResult<StockAlertsResult>> GetStockAlerts(
        [FromQuery] Guid? warehouseId,
        CancellationToken cancellationToken = default)
    {
        var guardResult = await EnsureStockSchemaAsync(cancellationToken);
        if (guardResult != null)
            return guardResult;

        var query = new GetStockAlertsQuery(warehouseId);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<StockAlertsResult>.Ok(result));
    }

    [HttpGet("{stockItemId:guid}/movements")]
    [Authorize(Policy = PermissionPolicies.StockRead)]
    public async Task<ActionResult<StockMovementsResult>> GetStockMovements(
        Guid stockItemId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var guardResult = await EnsureStockSchemaAsync(cancellationToken);
        if (guardResult != null)
            return guardResult;

        var query = new GetStockMovementsQuery(stockItemId, page, pageSize);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<StockMovementsResult>.Ok(result));
    }

    #endregion

    #region Vue Simplifiée (pour utilisateurs non techniciens)

    /// <summary>
    /// Obtient la vue d'ensemble simplifiée du stock avec statuts visuels.
    /// </summary>
    [HttpGet("overview")]
    [Authorize(Policy = PermissionPolicies.StockRead)]
    public async Task<ActionResult<SimpleStockOverviewDto>> GetSimpleOverview(
        [FromQuery] string? search = null,
        [FromQuery] bool alertsOnly = false,
        [FromQuery] Guid? warehouseId = null,
        CancellationToken cancellationToken = default)
    {
        var guardResult = await EnsureStockSchemaAsync(cancellationToken);
        if (guardResult != null)
            return guardResult;

        var query = new GetSimpleStockOverviewQuery(search, alertsOnly, warehouseId);
        var result = await _mediator.Send(query, cancellationToken);
        
        if (result.IsFailure)
            return BadRequest(ApiResponse<SimpleStockOverviewDto>.Fail(result.Error.Description));

        return Ok(ApiResponse<SimpleStockOverviewDto>.Ok(result.Value));
    }

    /// <summary>
    /// "Compter mon stock" - Comptage rapide ultra-simplifié.
    /// L'utilisateur indique combien il a, le système fait le reste.
    /// </summary>
    [HttpPost("quick-count")]
    [Authorize(Policy = PermissionPolicies.StockUpdate)]
    public async Task<ActionResult<QuickStockCountResult>> QuickCount(
        [FromBody] QuickStockCountCommand command,
        CancellationToken cancellationToken = default)
    {
        var guardResult = await EnsureStockSchemaAsync(cancellationToken);
        if (guardResult != null)
            return guardResult;

        var result = await _mediator.Send(command, cancellationToken);
        
        if (result.IsFailure)
            return BadRequest(ApiResponse<QuickStockCountResult>.Fail(result.Error.Description));

        return Ok(ApiResponse<QuickStockCountResult>.Ok(result.Value, result.Value.HumanMessage));
    }

    /// <summary>
    /// Vérifie la disponibilité du stock avant validation de facture.
    /// Retourne des messages pédagogiques.
    /// </summary>
    [HttpPost("check-availability")]
    [Authorize(Policy = PermissionPolicies.StockRead)]
    public async Task<ActionResult<StockAvailabilityResult>> CheckAvailability(
        [FromBody] CheckStockAvailabilityQuery query,
        CancellationToken cancellationToken = default)
    {
        var guardResult = await EnsureStockSchemaAsync(cancellationToken);
        if (guardResult != null)
            return guardResult;

        var result = await _mediator.Send(query, cancellationToken);
        
        if (result.IsFailure)
            return BadRequest(ApiResponse<StockAvailabilityResult>.Fail(result.Error.Description));

        return Ok(ApiResponse<StockAvailabilityResult>.Ok(result.Value, result.Value.SummaryMessage));
    }

    #endregion

    #region Warehouses

    [HttpGet("warehouses")]
    [Authorize(Policy = PermissionPolicies.StockRead)]
    public async Task<ActionResult<IReadOnlyList<WarehouseDto>>> GetWarehouses(
        [FromQuery] bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var guardResult = await EnsureStockSchemaAsync(cancellationToken);
        if (guardResult != null)
            return guardResult;

        var query = new GetWarehousesQuery(activeOnly);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<WarehouseDto>>.Ok(result));
    }

    [HttpPost("warehouses")]
    [Authorize(Policy = PermissionPolicies.StockCreate)]
    public async Task<ActionResult<Guid>> CreateWarehouse(
        [FromBody] CreateWarehouseCommand command,
        CancellationToken cancellationToken = default)
    {
        var guardResult = await EnsureStockSchemaAsync(cancellationToken);
        if (guardResult != null)
            return guardResult;

        var result = await _mediator.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        }

        return Ok(ApiResponse<Guid>.Ok(result.Value, "Entrepôt créé avec succès"));
    }

    [HttpPut("warehouses/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.StockUpdate)]
    public async Task<ActionResult> UpdateWarehouse(
        Guid id,
        [FromBody] UpdateWarehouseCommand command,
        CancellationToken cancellationToken = default)
    {
        var guardResult = await EnsureStockSchemaAsync(cancellationToken);
        if (guardResult != null)
            return guardResult;

        if (id != command.Id)
            return BadRequest("L'ID de l'URL ne correspond pas à l'ID de la commande.");

        var result = await _mediator.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return Ok();
    }

    #endregion

    #region Operations

    [HttpPost("entry")]
    [Authorize(Policy = PermissionPolicies.StockCreate)]
    public async Task<ActionResult<Guid>> RecordEntry(
        [FromBody] RecordStockEntryCommand command,
        CancellationToken cancellationToken = default)
    {
        var guardResult = await EnsureStockSchemaAsync(cancellationToken);
        if (guardResult != null)
            return guardResult;

        var result = await _mediator.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        }

        return Ok(ApiResponse<Guid>.Ok(result.Value, "Entrée de stock enregistrée"));
    }

    [HttpPost("exit")]
    [Authorize(Policy = PermissionPolicies.StockUpdate)]
    public async Task<ActionResult> RecordExit(
        [FromBody] RecordStockExitCommand command,
        CancellationToken cancellationToken = default)
    {
        var guardResult = await EnsureStockSchemaAsync(cancellationToken);
        if (guardResult != null)
            return guardResult;

        var result = await _mediator.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        }

        return Ok(ApiResponse<object>.Ok(null!, "Sortie de stock enregistrée"));
    }

    [HttpPost("adjust")]
    [Authorize(Policy = PermissionPolicies.StockUpdate)]
    public async Task<ActionResult> AdjustStock(
        [FromBody] AdjustStockCommand command,
        CancellationToken cancellationToken = default)
    {
        var guardResult = await EnsureStockSchemaAsync(cancellationToken);
        if (guardResult != null)
            return guardResult;

        var result = await _mediator.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        }

        return Ok(ApiResponse<object>.Ok(null!, "Stock ajusté"));
    }

    #endregion

    private async Task<ActionResult?> EnsureStockSchemaAsync(CancellationToken cancellationToken)
    {
        if (_tenantContext.TenantId is null)
        {
            return Unauthorized(ApiResponse<object>.Fail("Aucun contexte d'entreprise disponible."));
        }

        var result = await _migrationGuard.EnsureMigrationsAppliedAsync(_tenantContext.TenantId.Value, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        }

        return null;
    }
}
