using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll.EmployeeLoans;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>Prêts salariés (module RH &amp; Paie).</summary>
[ApiController]
[Route("api/payroll/loans")]
[Authorize]
public class EmployeeLoansController : ControllerBase
{
    private readonly IMediator _mediator;

    public EmployeeLoansController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EmployeeLoanDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] Guid employeeId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListEmployeeLoansQuery(employeeId), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<EmployeeLoanDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<EmployeeLoanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetEmployeeLoanByIdQuery(id), cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<EmployeeLoanDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<EmployeeLoanDto>.Ok(result.Value));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.PayrollManageEmployees)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeLoanDto dto, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateEmployeeLoanCommand(dto), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        return Ok(ApiResponse<Guid>.Ok(result.Value, "Prêt enregistré."));
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = PermissionPolicies.PayrollManageEmployees)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CancelEmployeeLoanCommand(id), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Prêt annulé."));
    }
}
