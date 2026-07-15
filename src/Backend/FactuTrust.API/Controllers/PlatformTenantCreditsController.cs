using System.Security.Claims;
using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Lot C3 — Gestion des crédits tenant côté admin.
///
/// Routes :
/// <list type="bullet">
///   <item><c>GET /api/platform/credits</c> — liste tous les crédits (filtre activeOnly).</item>
///   <item><c>GET /api/platform/tenants/{tenantId}/credits</c> — liste les crédits d'un tenant.</item>
///   <item><c>POST /api/platform/tenants/{tenantId}/credits</c> — accorde un crédit.</item>
///   <item><c>POST /api/platform/credits/{id}/revoke</c> — révoque un crédit.</item>
/// </list>
/// </summary>
[ApiController]
[Route("api/platform")]
[Authorize(Policy = PlatformPolicies.PlatformAdmin)]
public sealed class PlatformTenantCreditsController : ControllerBase
{
    private readonly ITenantCreditAdminService _service;
    private readonly ILogger<PlatformTenantCreditsController> _logger;

    public PlatformTenantCreditsController(
        ITenantCreditAdminService service,
        ILogger<PlatformTenantCreditsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("credits")]
    [Authorize(Policy = "perm:" + PlatformPermissions.CreditsManage)]
    [ProducesResponseType(typeof(ApiResponse<TenantCreditsPageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAll(
        [FromQuery] bool? activeOnly = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var dto = await _service.ListAllAsync(activeOnly, page, pageSize, cancellationToken);
        return Ok(ApiResponse<TenantCreditsPageDto>.Ok(dto));
    }

    [HttpGet("tenants/{tenantId:guid}/credits")]
    [Authorize(Policy = "perm:" + PlatformPermissions.CreditsManage)]
    [ProducesResponseType(typeof(ApiResponse<TenantCreditsPageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListByTenant(
        Guid tenantId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var dto = await _service.ListByTenantAsync(tenantId, page, pageSize, cancellationToken);
        return Ok(ApiResponse<TenantCreditsPageDto>.Ok(dto));
    }

    [HttpPost("tenants/{tenantId:guid}/credits")]
    [Authorize(Policy = "perm:" + PlatformPermissions.CreditsManage)]
    [ProducesResponseType(typeof(ApiResponse<TenantCreditDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Grant(
        Guid tenantId,
        [FromBody] GrantTenantCreditRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<TenantCreditDto>.Fail("Requête invalide."));

        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId))
            return Unauthorized(ApiResponse<TenantCreditDto>.Fail("Non authentifié."));

        var result = await _service.GrantAsync(tenantId, request, actorId, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Code.EndsWith(".NotFound", StringComparison.Ordinal)
                ? NotFound(ApiResponse<TenantCreditDto>.Fail(result.Error.Description, result.Error.Code))
                : BadRequest(ApiResponse<TenantCreditDto>.Fail(result.Error.Description, result.Error.Code));
        }

        _logger.LogInformation(
            "Platform admin {ActorId} granted credit of {Amount} TND to tenant {TenantId}: {Reason}",
            actorId, request.AmountTND, tenantId, request.Reason);

        return Ok(ApiResponse<TenantCreditDto>.Ok(result.Value, "Crédit accordé."));
    }

    [HttpPost("credits/{creditId:guid}/revoke")]
    [Authorize(Policy = "perm:" + PlatformPermissions.CreditsManage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(
        Guid creditId,
        [FromBody] RevokeTenantCreditRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("Motif requis (≥ 3 caractères)."));

        var result = await _service.RevokeAsync(creditId, request, cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code));

        return Ok(ApiResponse<object>.Ok(null!, "Crédit révoqué."));
    }
}
