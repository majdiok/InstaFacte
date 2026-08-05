using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll.Advances;
using FactuTrust.Application.Features.Payroll.Declarations;
using FactuTrust.Application.Features.Payroll.Leaves;
using FactuTrust.Application.Features.Payroll.Overtime;
using FactuTrust.Application.Features.Payroll.VariableAllowances;
using FactuTrust.Application.Features.Payroll.MealVouchers;
using FactuTrust.Application.Features.Payroll.InKindBenefits;
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

    // ── Variable allowances ──
    [HttpGet("variable-allowances")]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PayrollVariableAllowanceLineDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListVariableAllowances([FromQuery] int year, [FromQuery] int month, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListVariableAllowancesForMonthQuery(year, month), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PayrollVariableAllowanceLineDto>>.Ok(result));
    }

    [HttpPost("variable-allowances")]
    [Authorize(Policy = PermissionPolicies.PayrollManageEmployees)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateVariableAllowance([FromBody] UpsertVariableAllowanceLineDto dto, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateVariableAllowanceLineCommand(dto), cancellationToken);
        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("NotFound"))
                return NotFound(ApiResponse<Guid>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        }
        return Ok(ApiResponse<Guid>.Ok(result.Value, "Prime variable enregistrée."));
    }

    [HttpPut("variable-allowances/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PayrollManageEmployees)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateVariableAllowance(Guid id, [FromBody] UpsertVariableAllowanceLineDto dto, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdateVariableAllowanceLineCommand(id, dto), cancellationToken);
        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("NotFound"))
                return NotFound(ApiResponse<object>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        }
        return Ok(ApiResponse<object>.Ok(null!, "Prime variable mise à jour."));
    }

    [HttpDelete("variable-allowances/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PayrollManageEmployees)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteVariableAllowance(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteVariableAllowanceLineCommand(id), cancellationToken);
        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("NotFound"))
                return NotFound(ApiResponse<object>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        }
        return Ok(ApiResponse<object>.Ok(null!, "Prime variable supprimée."));
    }

    // ── Meal vouchers ──
    [HttpGet("meal-vouchers")]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PayrollMealVoucherLineDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMealVouchers([FromQuery] int year, [FromQuery] int month, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListMealVouchersForMonthQuery(year, month), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PayrollMealVoucherLineDto>>.Ok(result));
    }

    [HttpPost("meal-vouchers")]
    [Authorize(Policy = PermissionPolicies.PayrollManageEmployees)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateMealVoucher([FromBody] UpsertPayrollMealVoucherLineDto dto, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateMealVoucherLineCommand(dto), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        return Ok(ApiResponse<Guid>.Ok(result.Value, "Tickets restaurant enregistrés."));
    }

    [HttpPut("meal-vouchers/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PayrollManageEmployees)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateMealVoucher(Guid id, [FromBody] UpsertPayrollMealVoucherLineDto dto, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdateMealVoucherLineCommand(id, dto), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Tickets restaurant mis à jour."));
    }

    [HttpDelete("meal-vouchers/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PayrollManageEmployees)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteMealVoucher(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteMealVoucherLineCommand(id), cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Ligne supprimée."));
    }

    // ── In-kind benefits ──
    [HttpGet("in-kind-benefits")]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EmployeeInKindBenefitDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListInKindBenefits([FromQuery] Guid employeeId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListInKindBenefitsForEmployeeQuery(employeeId), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<EmployeeInKindBenefitDto>>.Ok(result));
    }

    [HttpPost("in-kind-benefits")]
    [Authorize(Policy = PermissionPolicies.PayrollManageEmployees)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateInKindBenefit([FromBody] UpsertEmployeeInKindBenefitDto dto, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateInKindBenefitCommand(dto), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        return Ok(ApiResponse<Guid>.Ok(result.Value, "Avantage en nature enregistré."));
    }

    [HttpPut("in-kind-benefits/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PayrollManageEmployees)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateInKindBenefit(Guid id, [FromBody] UpsertEmployeeInKindBenefitDto dto, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdateInKindBenefitCommand(id, dto), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Avantage en nature mis à jour."));
    }

    [HttpDelete("in-kind-benefits/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PayrollManageEmployees)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteInKindBenefit(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteInKindBenefitCommand(id), cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Avantage en nature supprimé."));
    }
}
