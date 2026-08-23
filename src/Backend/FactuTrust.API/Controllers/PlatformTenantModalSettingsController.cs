using System.Security.Claims;
using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Per-tenant Modal (Kimi) endpoint override. Absence of a row means the tenant inherits
/// the platform singleton configured at <c>/api/platform/ai-settings</c>.
/// </summary>
[ApiController]
[Route("api/platform/tenants/{tenantId:guid}/modal-settings")]
[Authorize(Policy = PlatformPolicies.PlatformAdmin)]
public sealed class PlatformTenantModalSettingsController : ControllerBase
{
    private readonly ITenantModalSettingsService _settings;
    private readonly ILogger<PlatformTenantModalSettingsController> _logger;

    public PlatformTenantModalSettingsController(
        ITenantModalSettingsService settings,
        ILogger<PlatformTenantModalSettingsController> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = "perm:" + PlatformPermissions.AiManage)]
    [ProducesResponseType(typeof(ApiResponse<TenantModalSettingsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid tenantId, CancellationToken cancellationToken)
    {
        var result = await _settings.GetAsync(tenantId, cancellationToken);
        if (result.IsFailure)
            return MapFailure(result.Error);

        return Ok(ApiResponse<TenantModalSettingsDto>.Ok(result.Value));
    }

    [HttpPut]
    [Authorize(Policy = "perm:" + PlatformPermissions.AiManage)]
    [ProducesResponseType(typeof(ApiResponse<TenantModalSettingsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid tenantId,
        [FromBody] UpdateTenantModalSettingsRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId))
            return Unauthorized(ApiResponse<TenantModalSettingsDto>.Fail("Non authentifié."));

        var result = await _settings.SetAsync(tenantId, request, actorId, cancellationToken);
        if (result.IsFailure)
            return MapFailure(result.Error);

        _logger.LogInformation(
            "Platform admin {ActorId} updated tenant Modal settings (tenant={TenantId}, enabled={Enabled})",
            actorId,
            tenantId,
            request.IsEnabled);

        return Ok(ApiResponse<TenantModalSettingsDto>.Ok(result.Value, "Configuration Modal enregistrée."));
    }

    [HttpDelete]
    [Authorize(Policy = "perm:" + PlatformPermissions.AiManage)]
    [ProducesResponseType(typeof(ApiResponse<TenantModalSettingsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId))
            return Unauthorized(ApiResponse<TenantModalSettingsDto>.Fail("Non authentifié."));

        var result = await _settings.DeleteAsync(tenantId, actorId, cancellationToken);
        if (result.IsFailure)
            return MapFailure(result.Error);

        return Ok(ApiResponse<TenantModalSettingsDto>.Ok(result.Value, "Configuration plateforme rétablie."));
    }

    private IActionResult MapFailure(FactuTrust.Domain.Common.Error error)
    {
        if (error.Code.EndsWith(".NotFound", StringComparison.Ordinal))
            return NotFound(ApiResponse<TenantModalSettingsDto>.Fail(error.Description, error.Code));

        return BadRequest(ApiResponse<TenantModalSettingsDto>.Fail(error.Description, error.Code));
    }
}
