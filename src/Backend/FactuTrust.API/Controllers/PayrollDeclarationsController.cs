using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll.Advances;
using FactuTrust.Application.Features.Payroll.Declarations;
using FactuTrust.Application.Features.Payroll.Leaves;
using FactuTrust.Application.Features.Payroll.Overtime;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>Déclarations sociales (DTS CNSS) et gestion des congés / avances (module RH &amp; Paie).</summary>
[ApiController]
[Route("api/payroll")]
[Authorize]
public class PayrollDeclarationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PayrollDeclarationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ── DTS ──
    [HttpGet("declarations/dts")]
    [Authorize(Policy = PermissionPolicies.PayrollDeclare)]
    [ProducesResponseType(typeof(ApiResponse<DtsDeclarationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDts([FromQuery] int year, [FromQuery] int quarter, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GenerateDtsDeclarationQuery(year, quarter), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<DtsDeclarationDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<DtsDeclarationDto>.Ok(result.Value));
    }

    [HttpGet("declarations/dts/export")]
    [Authorize(Policy = PermissionPolicies.PayrollDeclare)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportDtsCsv([FromQuery] int year, [FromQuery] int quarter, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ExportDtsDeclarationQuery(year, quarter), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return File(result.Value, "text/csv; charset=utf-8", $"dts_{year}_T{quarter}.csv");
    }

    // ── Leaves ──
    [HttpGet("leaves/compute-days")]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ComputeLeaveDays(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        CancellationToken cancellationToken)
    {
        var days = await _mediator.Send(new ComputeLeaveDaysQuery(startDate, endDate), cancellationToken);
        return Ok(ApiResponse<int>.Ok(days));
    }

    [HttpPost("leaves")]
    [Authorize(Policy = PermissionPolicies.PayrollManageEmployees)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateLeave([FromBody] CreateLeaveDto dto, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateLeaveCommand(dto), cancellationToken);
        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("NotFound"))
                return NotFound(ApiResponse<Guid>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        }
        return Ok(ApiResponse<Guid>.Ok(result.Value, "Congé enregistré."));
    }

    [HttpPost("leaves/{id:guid}/approve")]
    [Authorize(Policy = PermissionPolicies.PayrollManageEmployees)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveLeave(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ApproveLeaveCommand(id), cancellationToken);
        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("NotFound"))
                return NotFound(ApiResponse<object>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        }
        return Ok(ApiResponse<object>.Ok(null!, "Congé approuvé."));
    }

    [HttpDelete("leaves/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PayrollManageEmployees)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteLeave(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteLeaveCommand(id), cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Congé supprimé."));
    }

    // ── Advances ──
    [HttpPost("advances")]
    [Authorize(Policy = PermissionPolicies.PayrollManageEmployees)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAdvance([FromBody] CreateAdvanceDto dto, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateAdvanceCommand(dto), cancellationToken);
        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("NotFound"))
                return NotFound(ApiResponse<Guid>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        }
        return Ok(ApiResponse<Guid>.Ok(result.Value, "Avance enregistrée."));
    }

    [HttpDelete("advances/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PayrollManageEmployees)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteAdvance(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteAdvanceCommand(id), cancellationToken);
        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("NotFound"))
                return NotFound(ApiResponse<object>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        }
        return Ok(ApiResponse<object>.Ok(null!, "Avance supprimée."));
    }

    // ── Overtime ──
    [HttpGet("overtime")]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PayrollOvertimeLineDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListOvertime([FromQuery] int year, [FromQuery] int month, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListOvertimeForMonthQuery(year, month), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PayrollOvertimeLineDto>>.Ok(result));
    }

    [HttpPost("overtime")]
    [Authorize(Policy = PermissionPolicies.PayrollManageEmployees)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateOvertime([FromBody] UpsertOvertimeLineDto dto, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateOvertimeLineCommand(dto), cancellationToken);
        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("NotFound"))
                return NotFound(ApiResponse<Guid>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        }
        return Ok(ApiResponse<Guid>.Ok(result.Value, "Heures supplémentaires enregistrées."));
    }

    [HttpPut("overtime/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PayrollManageEmployees)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateOvertime(Guid id, [FromBody] UpsertOvertimeLineDto dto, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdateOvertimeLineCommand(id, dto), cancellationToken);
        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("NotFound"))
                return NotFound(ApiResponse<object>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        }
        return Ok(ApiResponse<object>.Ok(null!, "Heures supplémentaires mises à jour."));
    }

    [HttpDelete("overtime/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PayrollManageEmployees)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteOvertime(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteOvertimeLineCommand(id), cancellationToken);
        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("NotFound"))
                return NotFound(ApiResponse<object>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        }
        return Ok(ApiResponse<object>.Ok(null!, "Ligne supprimée."));
    }

    [HttpPost("overtime/preview")]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<OvertimePreviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewOvertime(
        [FromBody] PreviewOvertimeAmountQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<OvertimePreviewDto>.Ok(result));
    }
}
