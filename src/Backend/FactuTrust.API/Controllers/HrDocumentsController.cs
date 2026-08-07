using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll.HrDocuments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>Documents RH (certificat de travail, attestation de salaire, solde de tout compte).</summary>
[ApiController]
[Route("api/payroll/hr-documents")]
[Authorize]
public class HrDocumentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public HrDocumentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("employment-certificate/{employeeId:guid}/pdf")]
    [Authorize(Policy = PermissionPolicies.PayrollHrDocuments)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadEmploymentCertificate(Guid employeeId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GenerateEmploymentCertificateQuery(employeeId), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return File(result.Value, "application/pdf", $"certificat-travail-{employeeId}.pdf");
    }

    [HttpGet("salary-certificate/{employeeId:guid}/pdf")]
    [Authorize(Policy = PermissionPolicies.PayrollHrDocuments)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadSalaryCertificate(
        Guid employeeId,
        [FromQuery] int months = 3,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GenerateSalaryCertificateQuery(employeeId, months), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return File(result.Value, "application/pdf", $"attestation-salaire-{months}m-{employeeId}.pdf");
    }

    [HttpGet("solde-tout-compte/{employeeId:guid}/pdf")]
    [Authorize(Policy = PermissionPolicies.PayrollHrDocuments)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadSoldeToutCompte(Guid employeeId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GenerateSoldeToutCompteQuery(employeeId), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return File(result.Value, "application/pdf", $"solde-tout-compte-{employeeId}.pdf");
    }
}
