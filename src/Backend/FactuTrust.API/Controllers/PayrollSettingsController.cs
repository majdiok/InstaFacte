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
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateParameters(int fiscalYear, [FromBody] UpdatePayrollParametersDto dto, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdatePayrollParametersCommand(fiscalYear, dto), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Paramètres de paie mis à jour."));
    }
}
