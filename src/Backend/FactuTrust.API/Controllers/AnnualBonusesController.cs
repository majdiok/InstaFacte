using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll.AnnualBonuses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

[ApiController]
[Route("api/payroll/annual-bonuses")]
[Authorize]
public class AnnualBonusesController : ControllerBase
{
    private readonly IMediator _mediator;

    public AnnualBonusesController(IMediator mediator) => _mediator = mediator;

    [HttpGet("rules")]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AnnualBonusRuleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListRules([FromQuery] int? fiscalYear, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListAnnualBonusRulesQuery(fiscalYear), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AnnualBonusRuleDto>>.Ok(result));
    }

    [HttpPost("rules")]
    [Authorize(Policy = PermissionPolicies.PayrollSettings)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateRule([FromBody] UpsertAnnualBonusRuleDto dto, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateAnnualBonusRuleCommand(dto), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        return Ok(ApiResponse<Guid>.Ok(result.Value, "Règle créée."));
    }

    [HttpPut("rules/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PayrollSettings)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateRule(Guid id, [FromBody] UpsertAnnualBonusRuleDto dto, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdateAnnualBonusRuleCommand(id, dto), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Règle mise à jour."));
    }

    [HttpDelete("rules/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PayrollSettings)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteRule(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteAnnualBonusRuleCommand(id), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Règle supprimée."));
    }

    [HttpGet("assignments")]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EmployeeAnnualBonusRuleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAssignments([FromQuery] Guid? employeeId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListEmployeeAnnualBonusRulesQuery(employeeId), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<EmployeeAnnualBonusRuleDto>>.Ok(result));
    }

    [HttpPost("assignments")]
    [Authorize(Policy = PermissionPolicies.PayrollManageEmployees)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpsertAssignment([FromBody] UpsertEmployeeAnnualBonusRuleDto dto, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpsertEmployeeAnnualBonusRuleCommand(dto), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        return Ok(ApiResponse<Guid>.Ok(result.Value, "Affectation enregistrée."));
    }
}
