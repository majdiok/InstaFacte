using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll.Commands;
using FactuTrust.Application.Features.Payroll.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>Paramètres de paie versionnés par exercice (barème IRPP, taux CNSS/CSS/TFP/FOPROLOS).</summary>
[ApiController]
[Route("api/payroll/settings")]
[Authorize]
public class PayrollSettingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PayrollSettingsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("feature-flags")]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<PayrollFeatureFlagsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFeatureFlags(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPayrollFeatureFlagsQuery(), cancellationToken);
        return Ok(ApiResponse<PayrollFeatureFlagsDto>.Ok(result));
    }

    // ── Profil d'imputation comptable de la paie (par dossier) ──
    // Les écritures de paie n'ont qu'une cartographie de comptes à la fois : la modifier engage
    // toutes les OD à venir. Mêmes politiques que la mise à jour des paramètres d'exercice —
    // PayrollSettings (droit métier) ET PayrollFirmOperation (contexte cabinet délégué actif).

    [HttpGet("accounting")]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<PayrollAccountingSettingsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAccountingSettings(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPayrollAccountingSettingsQuery(), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<PayrollAccountingSettingsDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<PayrollAccountingSettingsDto>.Ok(result.Value));
    }

    [HttpPut("accounting")]
    [Authorize(Policy = PermissionPolicies.PayrollSettings)]
    [Authorize(Policy = PermissionPolicies.PayrollFirmOperation)]
    [ProducesResponseType(typeof(ApiResponse<PayrollAccountingSettingsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAccountingSettings(
        [FromBody] UpdatePayrollAccountingSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdatePayrollAccountingSettingsCommand(request), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<PayrollAccountingSettingsDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<PayrollAccountingSettingsDto>.Ok(
            result.Value, "Profil d'imputation comptable de la paie mis à jour."));
    }

    [HttpGet("parameters")]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PayrollParametersDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListParameters(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListPayrollParametersQuery(), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PayrollParametersDto>>.Ok(result));
    }

    [HttpGet("parameters/{fiscalYear:int}/preset")]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<PayrollLegalPresetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLegalPreset(int fiscalYear, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPayrollLegalPresetQuery(fiscalYear), cancellationToken);
        return Ok(ApiResponse<PayrollLegalPresetDto>.Ok(result));
    }

    [HttpGet("parameters/{fiscalYear:int}")]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<PayrollParametersDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetParameters(int fiscalYear, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPayrollParametersQuery(fiscalYear), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<PayrollParametersDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<PayrollParametersDto>.Ok(result.Value));
    }

    [HttpPut("parameters/{fiscalYear:int}")]
    [Authorize(Policy = PermissionPolicies.PayrollSettings)]
    [Authorize(Policy = PermissionPolicies.PayrollFirmOperation)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateParameters(int fiscalYear, [FromBody] UpdatePayrollParametersDto dto, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdatePayrollParametersCommand(fiscalYear, dto), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Paramètres de paie mis à jour."));
    }

    [HttpGet("parameters/{fiscalYear:int}/garnishment-brackets")]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PayrollGarnishmentBracketDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGarnishmentBrackets(int fiscalYear, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPayrollGarnishmentBracketsQuery(fiscalYear), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<IReadOnlyList<PayrollGarnishmentBracketDto>>.Fail(result.Error.Description));
        return Ok(ApiResponse<IReadOnlyList<PayrollGarnishmentBracketDto>>.Ok(result.Value));
    }

    [HttpPut("parameters/{fiscalYear:int}/garnishment-brackets")]
    [Authorize(Policy = PermissionPolicies.PayrollSettings)]
    [Authorize(Policy = PermissionPolicies.PayrollFirmOperation)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateGarnishmentBrackets(
        int fiscalYear,
        [FromBody] UpdatePayrollGarnishmentBracketsDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdatePayrollGarnishmentBracketsCommand(fiscalYear, dto), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Barème de saisie mis à jour."));
    }
}
