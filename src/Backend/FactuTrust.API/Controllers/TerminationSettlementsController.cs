using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll.Terminations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

[ApiController]
[Route("api/payroll/settlements")]
[Authorize]
public class TerminationSettlementsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TerminationSettlementsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TerminationSettlementDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] int? year, [FromQuery] int? month, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListTerminationSettlementsQuery(year, month), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<TerminationSettlementDto>>.Ok(result));
    }

    [HttpGet("preview")]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<TerminationSettlementPreviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Preview(
        [FromQuery] Guid employeeId,
        [FromQuery] DateTime terminationDate,
        [FromQuery] string reason,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new PreviewTerminationSettlementQuery(employeeId, terminationDate, reason), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<TerminationSettlementPreviewDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<TerminationSettlementPreviewDto>.Ok(result.Value));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.PayrollManageTermination)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Upsert([FromBody] UpsertTerminationSettlementDto dto, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpsertTerminationSettlementCommand(dto), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        return Ok(ApiResponse<Guid>.Ok(result.Value, "Solde de tout compte enregistré."));
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = PermissionPolicies.PayrollManageTermination)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ApproveTerminationSettlementCommand(id), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Solde approuvé."));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PayrollManageTermination)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteTerminationSettlementCommand(id), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Solde supprimé."));
    }
}
