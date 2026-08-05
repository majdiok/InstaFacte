using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll.Garnishments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>Saisies sur salaire et pensions alimentaires (module RH &amp; Paie).</summary>
[ApiController]
[Route("api/payroll/garnishments")]
[Authorize]
public class EmployeeGarnishmentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public EmployeeGarnishmentsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EmployeeGarnishmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] Guid employeeId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListEmployeeGarnishmentsQuery(employeeId), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<EmployeeGarnishmentDto>>.Ok(result));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.PayrollManageGarnishments)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeGarnishmentDto dto, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateEmployeeGarnishmentCommand(dto), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        return Ok(ApiResponse<Guid>.Ok(result.Value, "Saisie enregistrée."));
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = PermissionPolicies.PayrollManageGarnishments)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CancelEmployeeGarnishmentCommand(id), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Saisie annulée."));
    }
}
