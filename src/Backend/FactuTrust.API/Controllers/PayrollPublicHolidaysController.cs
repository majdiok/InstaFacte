using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll.PublicHolidays;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>Jours fériés tunisiens (module RH &amp; Paie).</summary>
[ApiController]
[Route("api/payroll/public-holidays")]
[Authorize]
public class PayrollPublicHolidaysController : ControllerBase
{
    private readonly IMediator _mediator;

    public PayrollPublicHolidaysController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PayrollPublicHolidayDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] int year, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListPublicHolidaysQuery(year), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PayrollPublicHolidayDto>>.Ok(result));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.PayrollSettings)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] UpsertPayrollPublicHolidayDto dto, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreatePublicHolidayCommand(dto), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        return Ok(ApiResponse<Guid>.Ok(result.Value, "Jour férié créé."));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PayrollSettings)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertPayrollPublicHolidayDto dto, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdatePublicHolidayCommand(id, dto), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Jour férié mis à jour."));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PayrollSettings)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeletePublicHolidayCommand(id), cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Jour férié supprimé."));
    }

    [HttpPost("seed")]
    [Authorize(Policy = PermissionPolicies.PayrollSettings)]
    [ProducesResponseType(typeof(ApiResponse<SeedPayrollPublicHolidaysResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Seed([FromBody] SeedPayrollPublicHolidaysDto dto, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new SeedPublicHolidaysCommand(dto), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<SeedPayrollPublicHolidaysResultDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<SeedPayrollPublicHolidaysResultDto>.Ok(
            result.Value,
            $"{result.Value.InsertedCount} jour(s) férié(s) ajouté(s), {result.Value.SkippedCount} ignoré(s)."));
    }
}
