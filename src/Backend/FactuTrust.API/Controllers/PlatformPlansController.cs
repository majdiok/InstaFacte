using System.Security.Claims;
using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Lot C1 — CRUD des plans tarifaires plateforme.
///
/// Sécurité :
/// <list type="bullet">
///   <item>Lecture (<see cref="List"/>, <see cref="GetById"/>) : <see cref="PlatformPermissions.PlansManage"/>
///         (tous les rôles ayant accès aux plans peuvent lire).</item>
///   <item>Écriture (<see cref="Create"/>, <see cref="Update"/>, <see cref="Archive"/>, <see cref="Reactivate"/>,
///         <see cref="Clone"/>) : <see cref="PlatformPermissions.PlansManage"/>.</item>
/// </list>
///
/// Selon <see cref="RolePermissionMatrix"/>, seuls <c>PlatformAdmin</c> et <c>BillingAdmin</c>
/// possèdent <c>plans:manage</c>.
/// </summary>
[ApiController]
[Route("api/platform/plans")]
[Authorize(Policy = PlatformPolicies.PlatformAdmin)]
public sealed class PlatformPlansController : ControllerBase
{
    private readonly IPlanAdminService _plans;
    private readonly ILogger<PlatformPlansController> _logger;

    public PlatformPlansController(IPlanAdminService plans, ILogger<PlatformPlansController> logger)
    {
        _plans = plans;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = "perm:" + PlatformPermissions.PlansManage)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PlanDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var list = await _plans.ListAsync(includeArchived, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PlanDto>>.Ok(list));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:" + PlatformPermissions.PlansManage)]
    [ProducesResponseType(typeof(ApiResponse<PlanDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _plans.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<PlanDto>.Fail(result.Error.Description, result.Error.Code));
        return Ok(ApiResponse<PlanDto>.Ok(result.Value));
    }

    [HttpPost]
    [Authorize(Policy = "perm:" + PlatformPermissions.PlansManage)]
    [ProducesResponseType(typeof(ApiResponse<PlanDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePlanRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<PlanDto>.Fail("Requête invalide."));

        var result = await _plans.CreateAsync(request, cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<PlanDto>.Fail(result.Error.Description, result.Error.Code));

        _logger.LogInformation(
            "Platform admin {ActorId} created plan {Code}",
            CurrentUserId(), result.Value.Code);

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id },
            ApiResponse<PlanDto>.Ok(result.Value, "Plan créé."));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:" + PlatformPermissions.PlansManage)]
    [ProducesResponseType(typeof(ApiResponse<PlanDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdatePlanRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<PlanDto>.Fail("Requête invalide."));

        var result = await _plans.UpdateAsync(id, request, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Code.EndsWith(".NotFound", StringComparison.Ordinal)
                ? NotFound(ApiResponse<PlanDto>.Fail(result.Error.Description, result.Error.Code))
                : BadRequest(ApiResponse<PlanDto>.Fail(result.Error.Description, result.Error.Code));
        }

        _logger.LogInformation(
            "Platform admin {ActorId} updated plan {PlanId}",
            CurrentUserId(), id);

        return Ok(ApiResponse<PlanDto>.Ok(result.Value, "Plan mis à jour."));
    }

    [HttpPost("{id:guid}/archive")]
    [Authorize(Policy = "perm:" + PlatformPermissions.PlansManage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var result = await _plans.ArchiveAsync(id, cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code));

        _logger.LogInformation("Platform admin {ActorId} archived plan {PlanId}", CurrentUserId(), id);
        return Ok(ApiResponse<object>.Ok(null!, "Plan archivé."));
    }

    [HttpPost("{id:guid}/reactivate")]
    [Authorize(Policy = "perm:" + PlatformPermissions.PlansManage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _plans.ReactivateAsync(id, cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code));

        _logger.LogInformation("Platform admin {ActorId} reactivated plan {PlanId}", CurrentUserId(), id);
        return Ok(ApiResponse<object>.Ok(null!, "Plan réactivé."));
    }

    [HttpPost("{id:guid}/clone")]
    [Authorize(Policy = "perm:" + PlatformPermissions.PlansManage)]
    [ProducesResponseType(typeof(ApiResponse<PlanDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Clone(
        Guid id,
        [FromBody] ClonePlanRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<PlanDto>.Fail("Requête invalide."));

        var result = await _plans.CloneAsync(id, request, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Code.EndsWith(".NotFound", StringComparison.Ordinal)
                ? NotFound(ApiResponse<PlanDto>.Fail(result.Error.Description, result.Error.Code))
                : BadRequest(ApiResponse<PlanDto>.Fail(result.Error.Description, result.Error.Code));
        }

        _logger.LogInformation(
            "Platform admin {ActorId} cloned plan {SourceId} → {NewCode}",
            CurrentUserId(), id, request.NewCode);

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id },
            ApiResponse<PlanDto>.Ok(result.Value, "Plan cloné."));
    }

    private string? CurrentUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}
