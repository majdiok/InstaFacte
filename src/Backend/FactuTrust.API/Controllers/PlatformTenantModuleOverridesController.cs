using System.Security.Claims;
using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Lot C1 — Gestion des overrides de modules par tenant.
///
/// Routes :
/// <list type="bullet">
///   <item><c>GET /api/platform/tenants/{tenantId}/module-overrides</c></item>
///   <item><c>POST /api/platform/tenants/{tenantId}/module-overrides</c> (upsert : SetAsync)</item>
///   <item><c>DELETE /api/platform/tenants/{tenantId}/module-overrides/{overrideId}</c></item>
/// </list>
///
/// Permission requise : <see cref="PlatformPermissions.PlansManage"/> (PlatformAdmin / BillingAdmin).
/// </summary>
[ApiController]
[Route("api/platform/tenants/{tenantId:guid}/module-overrides")]
[Authorize(Policy = PlatformPolicies.PlatformAdmin)]
public sealed class PlatformTenantModuleOverridesController : ControllerBase
{
    private readonly ITenantModuleOverrideService _service;
    private readonly ILogger<PlatformTenantModuleOverridesController> _logger;

    public PlatformTenantModuleOverridesController(
        ITenantModuleOverrideService service,
        ILogger<PlatformTenantModuleOverridesController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = "perm:" + PlatformPermissions.PlansManage)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TenantModuleOverrideDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(Guid tenantId, CancellationToken cancellationToken)
    {
        var list = await _service.ListByTenantAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<TenantModuleOverrideDto>>.Ok(list));
    }

    [HttpPost]
    [Authorize(Policy = "perm:" + PlatformPermissions.PlansManage)]
    [ProducesResponseType(typeof(ApiResponse<TenantModuleOverrideDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Set(
        Guid tenantId,
        [FromBody] SetTenantModuleOverrideRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<TenantModuleOverrideDto>.Fail("Requête invalide."));
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId))
            return Unauthorized(ApiResponse<TenantModuleOverrideDto>.Fail("Non authentifié."));

        var result = await _service.SetAsync(tenantId, request, actorId, cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<TenantModuleOverrideDto>.Fail(result.Error.Description, result.Error.Code));

        _logger.LogInformation(
            "Platform admin {ActorId} set module override (tenant={TenantId}, module={Module}, enabled={Enabled})",
            actorId, tenantId, request.Module, request.IsEnabled);

        return CreatedAtAction(nameof(List), new { tenantId },
            ApiResponse<TenantModuleOverrideDto>.Ok(result.Value, "Override enregistré."));
    }

    [HttpDelete("{overrideId:guid}")]
    [Authorize(Policy = "perm:" + PlatformPermissions.PlansManage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid tenantId, Guid overrideId, CancellationToken cancellationToken)
    {
        var removed = await _service.RemoveAsync(overrideId, cancellationToken);
        if (!removed)
            return NotFound(ApiResponse<object>.Fail("Override introuvable.", "ModuleOverride.NotFound"));

        _logger.LogInformation(
            "Platform admin removed module override {OverrideId} (tenant={TenantId})",
            overrideId, tenantId);

        return Ok(ApiResponse<object>.Ok(null!, "Override supprimé."));
    }
}
