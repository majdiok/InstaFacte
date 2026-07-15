using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>Bulletins de paie (module RH &amp; Paie).</summary>
[ApiController]
[Route("api/payroll/payslips")]
[Authorize]
public class PayslipsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PayslipsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<PayslipDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPayslip(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPayslipByIdQuery(id), cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<PayslipDetailDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<PayslipDetailDto>.Ok(result.Value));
    }

    [HttpGet("{id:guid}/pdf")]
    [Authorize(Policy = PermissionPolicies.PayrollExport)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadPdf(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPayslipPdfQuery(id), cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description));

        return File(result.Value, "application/pdf", $"bulletin-paie-{id}.pdf");
    }
}
