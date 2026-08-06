using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Enums;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll.Reports;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>États de contrôle du module RH &amp; Paie : livre de paie simplifié et journal de paie.</summary>
[ApiController]
[Route("api/payroll/reports")]
[Authorize]
public class PayrollReportsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PayrollReportsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ── Livre de paie simplifié ──

    [HttpGet("payroll-book")]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<PayrollBookDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPayrollBook(
        [FromQuery] int year,
        [FromQuery] int fromMonth,
        [FromQuery] int toMonth,
        [FromQuery] bool includeCalculated = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GeneratePayrollBookQuery(year, fromMonth, toMonth, includeCalculated), cancellationToken);

        if (result.IsFailure)
            return Failure<PayrollBookDto>(result.Error.Code, result.Error.Description);

        return Ok(ApiResponse<PayrollBookDto>.Ok(result.Value));
    }

    [HttpGet("payroll-book/export")]
    [Authorize(Policy = PermissionPolicies.PayrollExport)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportPayrollBook(
        [FromQuery] int year,
        [FromQuery] int fromMonth,
        [FromQuery] int toMonth,
        [FromQuery] bool includeCalculated = false,
        [FromQuery] AccountingExportFormat format = AccountingExportFormat.Csv,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new ExportPayrollBookQuery(year, fromMonth, toMonth, includeCalculated, format), cancellationToken);

        if (result.IsFailure)
            return Failure<object>(result.Error.Code, result.Error.Description);

        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }

    // ── Journal de paie ──

    [HttpGet("payroll-journal")]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<PayrollJournalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPayrollJournal(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] bool includeCalculated = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GeneratePayrollJournalQuery(year, month, includeCalculated), cancellationToken);

        if (result.IsFailure)
            return Failure<PayrollJournalDto>(result.Error.Code, result.Error.Description);

        return Ok(ApiResponse<PayrollJournalDto>.Ok(result.Value));
    }

    [HttpGet("payroll-journal/export")]
    [Authorize(Policy = PermissionPolicies.PayrollExport)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportPayrollJournal(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] bool includeCalculated = false,
        [FromQuery] AccountingExportFormat format = AccountingExportFormat.Csv,
        [FromQuery] PayrollJournalView view = PayrollJournalView.ByEmployee,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new ExportPayrollJournalQuery(year, month, includeCalculated, format, view), cancellationToken);

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
