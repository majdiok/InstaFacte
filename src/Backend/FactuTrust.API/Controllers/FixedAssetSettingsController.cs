using FactuTrust.API.Authorization;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.FixedAssets;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Paramètres du module Immobilisations du dossier (tenant) — support des exercices comptables
/// décalés (plan « Exercices décalés », P1). Permission Accounting.
/// </summary>
[ApiController]
[Route("api/accounting/fixed-assets/settings")]
[Authorize]
public sealed class FixedAssetSettingsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly FixedAssetsOptions _options;

    public FixedAssetSettingsController(IMediator mediator, IOptions<FixedAssetsOptions> options)
    {
        _mediator = mediator;
        _options = options.Value;
    }

    private IActionResult? GuardEnabled()
    {
        if (!_options.Enabled)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse<object>.Fail(
                "Le sous-module Immobilisations est désactivé. Activez Features:FixedAssets:Enabled."));
        return null;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AccountingRead)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled) return disabled;
        var r = await _mediator.Send(new GetFixedAssetSettingsQuery(), cancellationToken);
        if (r.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<FixedAssetSettingsDto>.Ok(r.Value));
    }

    [HttpPut]
    [Authorize(Policy = PermissionPolicies.AccountingCreate)]
    public async Task<IActionResult> Update([FromBody] UpdateFixedAssetSettingsRequest request, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled) return disabled;
        var r = await _mediator.Send(new UpdateFixedAssetSettingsCommand(request), cancellationToken);
        if (r.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<FixedAssetSettingsDto>.Ok(r.Value));
    }
}
