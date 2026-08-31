using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll.Commands;
using FactuTrust.Application.Features.Payroll.Reports;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Boîte à outils firm-only de remédiation SCE paie (plan §5 / WS-5) : diagnostic conformité,
/// OD de reclassement, dé-solde ciblé avances/échéances et rapport d'exposition. Chaque endpoint
/// est barré par la politique <c>PayrollFirmOperation</c> (contexte cabinet actif) — double barrière
/// avec <c>PayrollFirmExclusiveRequests</c> côté pipeline MediatR.
/// </summary>
[ApiController]
[Route("api/payroll/compliance")]
[Authorize]
public sealed class PayrollComplianceController : ControllerBase
{
    private readonly IMediator _mediator;

    public PayrollComplianceController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ── Diagnostic conformité paie (7 checks scorés) ──
    [HttpGet("diagnostic")]
    [Authorize(Policy = PermissionPolicies.PayrollFirmOperation)]
    [ProducesResponseType(typeof(ApiResponse<PayrollComplianceDiagnosticDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDiagnostic(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new PayrollComplianceDiagnosticQuery(), cancellationToken);
        if (result.IsFailure)
            return Failure<PayrollComplianceDiagnosticDto>(result.Error.Code, result.Error.Description);
        return Ok(ApiResponse<PayrollComplianceDiagnosticDto>.Ok(result.Value));
    }

    // ── OD de reclassement SCE d'un cycle ──
    [HttpPost("reclassification/{runId:guid}")]
    [Authorize(Policy = PermissionPolicies.PayrollFirmOperation)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerateReclassification(
        Guid runId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GeneratePayrollReclassificationCommand(runId), cancellationToken);
        if (result.IsFailure)
            return Failure<Guid>(result.Error.Code, result.Error.Description);
        return Ok(ApiResponse<Guid>.Ok(result.Value, "OD de reclassement générée."));
    }

    // ── Dé-solde ciblé : avance (R-06) ──
    [HttpPost("unsettle-advance/{advanceId:guid}")]
    [Authorize(Policy = PermissionPolicies.PayrollFirmOperation)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UnsettleAdvance(
        Guid advanceId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new UnsettleAdvanceCommand(advanceId), cancellationToken);
        if (result.IsFailure)
            return Failure<object>(result.Error.Code, result.Error.Description);
        return Ok(ApiResponse<object>.Ok(null!, "Avance dé-soldée."));
    }

    // ── Dé-solde ciblé : échéance de prêt (R-06) ──
    [HttpPost("unsettle-loan-installment/{installmentId:guid}")]
    [Authorize(Policy = PermissionPolicies.PayrollFirmOperation)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UnsettleLoanInstallment(
        Guid installmentId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new UnsettleLoanInstallmentCommand(installmentId), cancellationToken);
        if (result.IsFailure)
            return Failure<object>(result.Error.Code, result.Error.Description);
        return Ok(ApiResponse<object>.Ok(null!, "Échéance dé-soldée."));
    }

    // ── Rapport d'exposition (deltas CNSS sal/pat + IRPP barème) ──
    [HttpGet("exposure")]
    [Authorize(Policy = PermissionPolicies.PayrollFirmOperation)]
    [ProducesResponseType(typeof(ApiResponse<PayrollExposureReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExposure(
        [FromQuery] int year,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetPayrollExposureReportQuery(year), cancellationToken);
        if (result.IsFailure)
            return Failure<PayrollExposureReportDto>(result.Error.Code, result.Error.Description);
        return Ok(ApiResponse<PayrollExposureReportDto>.Ok(result.Value));
    }

    [HttpGet("exposure/export")]
    [Authorize(Policy = PermissionPolicies.PayrollFirmOperation)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportExposure(
        [FromQuery] int year,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ExportPayrollExposureReportQuery(year), cancellationToken);
        if (result.IsFailure)
            return Failure<object>(result.Error.Code, result.Error.Description);
        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }

    private IActionResult Failure<T>(string code, string description)
    {
        if (code.Contains("NotFound"))
            return NotFound(ApiResponse<T>.Fail(description));
        if (code == "Conflict")
            return Conflict(ApiResponse<T>.Fail(description));
        return BadRequest(ApiResponse<T>.Fail(description));
    }
}
