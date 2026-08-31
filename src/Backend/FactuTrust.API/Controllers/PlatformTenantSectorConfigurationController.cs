using System.Security.Claims;
using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Auth;
using FactuTrust.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Phase 2 — backoffice tenant sector re-configuration: preview and apply a segment/domain change
/// for an existing tenant (plan §WP-B7, D6).
///
/// Routes: <c>api/platform/tenants/{tenantId:guid}/sector-configuration/preview|apply</c>.
/// Entry gate: <see cref="PlatformPolicies.PlatformAdmin"/>. Fine-grained per-action policies:
/// <see cref="PlatformPermissions.SectorRulesRead"/> for preview, <see cref="PlatformPermissions.SectorRulesApply"/>
/// for apply. Preview is side-effect-free (templates evaluated as a dry run); apply is per-step
/// fault-isolated (fail-continue) and audit-logged in the tenant DB.
/// </summary>
[ApiController]
[Route("api/platform/tenants/{tenantId:guid}/sector-configuration")]
[Authorize(Policy = PlatformPolicies.PlatformAdmin)]
public sealed class PlatformTenantSectorConfigurationController : ControllerBase
{
    private readonly ITenantSectorReconfigurationService _service;
    private readonly ILogger<PlatformTenantSectorConfigurationController> _logger;

    public PlatformTenantSectorConfigurationController(
        ITenantSectorReconfigurationService service,
        ILogger<PlatformTenantSectorConfigurationController> logger)
    {
        _service = service;
        _logger = logger;
    }

    private bool TryGetActorId(out Guid actorId) => Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out actorId);

    [HttpPost("preview")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesRead)]
    [ProducesResponseType(typeof(ApiResponse<SectorReconfigurationPreviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SectorReconfigurationPreviewDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<SectorReconfigurationPreviewDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Preview(Guid tenantId, [FromBody] SectorReconfigurationRequestDto request, CancellationToken cancellationToken)
    {
        TryGetActorId(out var actorId);

        var result = await _service.PreviewAsync(tenantId, request, actorId, cancellationToken);
        if (result.IsSuccess)
            return Ok(ApiResponse<SectorReconfigurationPreviewDto>.Ok(result.Value));
        return MapFailure<SectorReconfigurationPreviewDto>(result.Error);
    }

    [HttpPost("apply")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesApply)]
    [ProducesResponseType(typeof(ApiResponse<SectorReconfigurationApplyResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SectorReconfigurationApplyResultDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<SectorReconfigurationApplyResultDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Apply(Guid tenantId, [FromBody] SectorReconfigurationRequestDto request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized(ApiResponse<SectorReconfigurationApplyResultDto>.Fail("Non authentifié."));

        var result = await _service.ApplyAsync(tenantId, request, actorId, cancellationToken);
        if (result.IsFailure)
            return MapFailure<SectorReconfigurationApplyResultDto>(result.Error);

        var successCount = result.Value.Steps.Count(s => s.Success);
        var failureCount = result.Value.Steps.Count - successCount;
        var message = $"Reconfiguration sectorielle appliquée ({successCount} étape(s) réussie(s), {failureCount} en échec).";

        _logger.LogInformation(
            "Platform admin {ActorId} applied sector reconfiguration for tenant {TenantId} (success={Success}, failed={Failed})",
            actorId, tenantId, successCount, failureCount);

        return Ok(ApiResponse<SectorReconfigurationApplyResultDto>.Ok(result.Value, message));
    }

    /// <summary>
    /// Maps a service <see cref="Error"/> to the plan's HTTP contract: a
    /// <see cref="ITenantSectorReconfigurationService.TenantNotFoundCode"/> ⇒ 404 with the French
    /// message; any other validation error (unknown/unlinked segment/domain) ⇒ 400.
    /// </summary>
    private IActionResult MapFailure<T>(Error error)
    {
        if (string.Equals(error.Code, ITenantSectorReconfigurationService.TenantNotFoundCode, StringComparison.Ordinal))
            return NotFound(ApiResponse<T>.Fail(error.Description, error.Code));
        return BadRequest(ApiResponse<T>.Fail(error.Description, error.Code));
    }
}
