using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll.Commands;
using FactuTrust.Application.Features.Payroll.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>Salariés et contrats de travail (module RH &amp; Paie).</summary>
[ApiController]
[Route("api/payroll/employees")]
[Authorize]
public class EmployeesController : ControllerBase
{
    private readonly IMediator _mediator;

    public EmployeesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<EmployeeListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEmployees(
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetEmployeesQuery(search, isActive, page, pageSize), cancellationToken);
        return Ok(ApiResponse<PagedResult<EmployeeListDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEmployee(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetEmployeeByIdQuery(id), cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<EmployeeDetailDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<EmployeeDetailDto>.Ok(result.Value));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.PayrollManageEmployees)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateEmployee([FromBody] CreateEmployeeDto dto, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateEmployeeCommand(dto), cancellationToken);
        if (result.IsFailure)
        {
            if (result.Error.Code == "Conflict")
                return Conflict(ApiResponse<Guid>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        }
        return CreatedAtAction(nameof(GetEmployee), new { id = result.Value }, ApiResponse<Guid>.Ok(result.Value, "Salarié créé."));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PayrollManageEmployees)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEmployee(Guid id, [FromBody] UpdateEmployeeDto dto, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdateEmployeeCommand(id, dto), cancellationToken);
        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("NotFound"))
                return NotFound(ApiResponse<object>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        }
        return Ok(ApiResponse<object>.Ok(null!, "Salarié mis à jour."));
    }

    [HttpPatch("{id:guid}/toggle-active")]
    [Authorize(Policy = PermissionPolicies.PayrollManageEmployees)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleActive(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ToggleEmployeeActiveCommand(id), cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<bool>.Fail(result.Error.Description));
        return Ok(ApiResponse<bool>.Ok(result.Value, result.Value ? "Salarié activé." : "Salarié désactivé."));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PayrollManageEmployees)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEmployee(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteEmployeeCommand(id), cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Salarié supprimé."));
    }

    // ── Contracts ──

    [HttpPost("{id:guid}/contracts")]
    [Authorize(Policy = PermissionPolicies.PayrollManageEmployees)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddContract(Guid id, [FromBody] CreateContractDto dto, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new AddContractCommand(id, dto), cancellationToken);
        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("NotFound"))
                return NotFound(ApiResponse<Guid>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        }
        return Ok(ApiResponse<Guid>.Ok(result.Value, "Contrat ajouté."));
    }

    [HttpPut("contracts/{contractId:guid}")]
    [Authorize(Policy = PermissionPolicies.PayrollManageEmployees)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateContract(Guid contractId, [FromBody] UpdateContractDto dto, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdateContractCommand(contractId, dto), cancellationToken);
        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("NotFound"))
                return NotFound(ApiResponse<object>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        }
        return Ok(ApiResponse<object>.Ok(null!, "Contrat mis à jour."));
    }

    [HttpDelete("contracts/{contractId:guid}")]
    [Authorize(Policy = PermissionPolicies.PayrollManageEmployees)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteContract(Guid contractId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteContractCommand(contractId), cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Contrat supprimé."));
    }

    // ── Leaves & advances (per employee) ──

    [HttpGet("{id:guid}/leaves")]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LeaveRequestDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLeaves(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new Application.Features.Payroll.Leaves.GetEmployeeLeavesQuery(id), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<LeaveRequestDto>>.Ok(result));
    }

    [HttpGet("{id:guid}/advances")]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EmployeeAdvanceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAdvances(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new Application.Features.Payroll.Advances.GetEmployeeAdvancesQuery(id), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<EmployeeAdvanceDto>>.Ok(result));
    }

    [HttpGet("{id:guid}/leave-balance")]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<LeaveBalanceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLeaveBalance(Guid id, [FromQuery] int year, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new Application.Features.Payroll.LeaveBalance.GetEmployeeLeaveBalanceQuery(id, year), cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<LeaveBalanceDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<LeaveBalanceDto>.Ok(result.Value));
    }

    [HttpPut("{id:guid}/leave-balance/opening")]
    [Authorize(Policy = PermissionPolicies.PayrollManageEmployees)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetLeaveOpeningBalance(Guid id, [FromBody] SetLeaveOpeningBalanceDto dto, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new Application.Features.Payroll.LeaveBalance.SetLeaveOpeningBalanceCommand(id, dto), cancellationToken);
        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("NotFound"))
                return NotFound(ApiResponse<object>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        }
        return Ok(ApiResponse<object>.Ok(null!, "Solde initial enregistré."));
    }

    [HttpGet("{id:guid}/overtime")]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PayrollOvertimeLineDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEmployeeOvertime(Guid id, [FromQuery] int year, [FromQuery] int month, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new Application.Features.Payroll.Overtime.ListEmployeeOvertimeQuery(id, year, month), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PayrollOvertimeLineDto>>.Ok(result));
    }
}
