using System.Security.Claims;
using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Lot C4 — Lecture / mise à jour des paramètres fiscaux singleton plateforme.
/// </summary>
[ApiController]
[Route("api/platform/fiscal-settings")]
[Authorize(Policy = PlatformPolicies.PlatformAdmin)]
public sealed class PlatformFiscalSettingsController : ControllerBase
{
    private readonly IPlatformFiscalSettingsService _service;
    private readonly ILogger<PlatformFiscalSettingsController> _logger;

    public PlatformFiscalSettingsController(
        IPlatformFiscalSettingsService service,
        ILogger<PlatformFiscalSettingsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = "perm:" + PlatformPermissions.InvoiceRead)]
    [ProducesResponseType(typeof(ApiResponse<PlatformFiscalSettingsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var dto = await _service.GetAsync(cancellationToken);
        return Ok(ApiResponse<PlatformFiscalSettingsDto>.Ok(dto));
    }

    [HttpPut]
    [Authorize(Policy = "perm:" + PlatformPermissions.InvoiceIssue)]
    [ProducesResponseType(typeof(ApiResponse<PlatformFiscalSettingsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update([FromBody] UpdatePlatformFiscalSettingsRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<PlatformFiscalSettingsDto>.Fail("Requête invalide."));
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId))
            return Unauthorized(ApiResponse<PlatformFiscalSettingsDto>.Fail("Non authentifié."));

        var result = await _service.UpdateAsync(request, actorId, cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<PlatformFiscalSettingsDto>.Fail(result.Error.Description, result.Error.Code));

        _logger.LogInformation("Platform admin {ActorId} updated fiscal settings", actorId);
        return Ok(ApiResponse<PlatformFiscalSettingsDto>.Ok(result.Value, "Paramètres fiscaux mis à jour."));
    }
}
