using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll.SocialFunds;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>Caisse complémentaire / mutuelle (module RH &amp; Paie).</summary>
[ApiController]
[Route("api/payroll/social-funds")]
[Authorize]
public class PayrollSocialFundsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PayrollSocialFundsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("schemes")]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SocialFundSchemeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListSchemes([FromQuery] bool activeOnly, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListSocialFundSchemesQuery(activeOnly), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<SocialFundSchemeDto>>.Ok(result));
    }

    [HttpPost("schemes")]
    [Authorize(Policy = PermissionPolicies.PayrollSettings)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateScheme([FromBody] UpsertSocialFundSchemeDto dto, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateSocialFundSchemeCommand(dto), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        return Ok(ApiResponse<Guid>.Ok(result.Value, "Caisse créée."));
    }

    [HttpPut("schemes/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PayrollSettings)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateScheme(Guid id, [FromBody] UpsertSocialFundSchemeDto dto, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdateSocialFundSchemeCommand(id, dto), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Caisse mise à jour."));
    }

    [HttpGet("enrollments")]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EmployeeSocialFundEnrollmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListEnrollments([FromQuery] Guid employeeId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListEmployeeSocialFundEnrollmentsQuery(employeeId), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<EmployeeSocialFundEnrollmentDto>>.Ok(result));
    }

    [HttpPost("enrollments")]
    [Authorize(Policy = PermissionPolicies.PayrollManageEmployees)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateEnrollment([FromBody] UpsertEmployeeSocialFundEnrollmentDto dto, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateEmployeeSocialFundEnrollmentCommand(dto), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        return Ok(ApiResponse<Guid>.Ok(result.Value, "Adhésion enregistrée."));
    }

    [HttpPut("enrollments/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PayrollManageEmployees)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateEnrollment(
        Guid id,
        [FromBody] UpdateEnrollmentRequest body,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new UpdateEmployeeSocialFundEnrollmentCommand(id, body.EndDate, body.OverrideEmployeeAmount, body.OverrideEmployerAmount),
            cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Adhésion mise à jour."));
    }

    [HttpDelete("enrollments/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PayrollManageEmployees)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteEnrollment(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteEmployeeSocialFundEnrollmentCommand(id), cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Adhésion supprimée."));
    }

    public sealed record UpdateEnrollmentRequest(DateTime? EndDate, decimal? OverrideEmployeeAmount, decimal? OverrideEmployerAmount);
}
