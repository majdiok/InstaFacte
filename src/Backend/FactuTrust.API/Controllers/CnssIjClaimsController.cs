using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll.CnssIjClaims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>Créances IJ CNSS (subrogation maladie / maternité).</summary>
[ApiController]
[Route("api/payroll/cnss-ij-claims")]
[Authorize]
public class CnssIjClaimsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CnssIjClaimsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CnssIjClaimDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? employeeId,
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListCnssIjClaimsQuery(employeeId, year, month), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<CnssIjClaimDto>>.Ok(result));
    }

    [HttpPost("{id:guid}/mark-paid")]
    [Authorize(Policy = PermissionPolicies.PayrollManageEmployees)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkPaid(Guid id, [FromBody] MarkCnssIjClaimPaidRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new MarkCnssIjClaimPaidCommand(id, request.PaidAt), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Créance IJ marquée comme réglée."));
    }
}
