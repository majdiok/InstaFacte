using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll.BankTransfer;
using FactuTrust.Application.Features.Payroll.Commands;
using FactuTrust.Application.Features.Payroll.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>Cycles de paie mensuels (module RH &amp; Paie).</summary>
[ApiController]
[Route("api/payroll/runs")]
[Authorize]
public class PayrollRunsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PayrollRunsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PayrollRunListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRuns([FromQuery] int? year, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPayrollRunsQuery(year), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PayrollRunListDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<PayrollRunDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRun(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPayrollRunByIdQuery(id), cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<PayrollRunDetailDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<PayrollRunDetailDto>.Ok(result.Value));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.PayrollRun)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateRun([FromBody] CreatePayrollRunDto dto, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreatePayrollRunCommand(dto), cancellationToken);
        if (result.IsFailure)
        {
            if (result.Error.Code == "Conflict")
                return Conflict(ApiResponse<Guid>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        }
        return CreatedAtAction(nameof(GetRun), new { id = result.Value }, ApiResponse<Guid>.Ok(result.Value, "Cycle de paie créé."));
    }

    [HttpPost("{id:guid}/calculate")]
    [Authorize(Policy = PermissionPolicies.PayrollRun)]
    [ProducesResponseType(typeof(ApiResponse<CalculatePayrollRunResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Calculate(Guid id, [FromBody] CalculatePayrollRunDto dto, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CalculatePayrollRunCommand(id, dto), cancellationToken);
        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("NotFound"))
                return NotFound(ApiResponse<CalculatePayrollRunResultDto>.Fail(result.Error.Description));
            if (result.Error.Code == "Conflict")
                return Conflict(ApiResponse<CalculatePayrollRunResultDto>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<CalculatePayrollRunResultDto>.Fail(result.Error.Description));
        }
        var message = result.Value.Warnings.Count > 0
            ? $"Cycle calculé avec {result.Value.Warnings.Count} avertissement(s)."
            : "Cycle calculé.";
        return Ok(ApiResponse<CalculatePayrollRunResultDto>.Ok(result.Value, message));
    }

    [HttpGet("{id:guid}/prorata-preview")]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<PayrollProrataPreviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProrataPreview(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new PreviewPayrollProrataQuery(id), cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<PayrollProrataPreviewDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<PayrollProrataPreviewDto>.Ok(result.Value));
    }

    [HttpPost("{id:guid}/validate")]
    [Authorize(Policy = PermissionPolicies.PayrollValidate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Validate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ValidatePayrollRunCommand(id), cancellationToken);
        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("NotFound"))
                return NotFound(ApiResponse<object>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        }
        return Ok(ApiResponse<object>.Ok(null!, "Cycle validé. Les écritures comptables ont été générées."));
    }

    [HttpPost("{id:guid}/reopen")]
    [Authorize(Policy = PermissionPolicies.PayrollValidate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reopen(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ReopenPayrollRunCommand(id), cancellationToken);
        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("NotFound"))
                return NotFound(ApiResponse<object>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        }
        return Ok(ApiResponse<object>.Ok(null!, "Cycle rouvert."));
    }

    [HttpPost("{id:guid}/close")]
    [Authorize(Policy = PermissionPolicies.PayrollValidate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Close(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ClosePayrollRunCommand(id), cancellationToken);
        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("NotFound"))
                return NotFound(ApiResponse<object>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        }
        return Ok(ApiResponse<object>.Ok(null!, "Cycle clôturé."));
    }

    /// <summary>Prévisualisation du fichier de virement bancaire (cycle Validé/Clôturé).</summary>
    [HttpGet("{id:guid}/bank-transfer/preview")]
    [Authorize(Policy = PermissionPolicies.PayrollExport)]
    [ProducesResponseType(typeof(ApiResponse<PayrollBankTransferPreviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PreviewBankTransfer(
        Guid id,
        [FromQuery] Guid? bankAccountId,
        [FromQuery] string? label,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GeneratePayrollBankTransferQuery(id, bankAccountId, label),
            cancellationToken);
        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("NotFound"))
                return NotFound(ApiResponse<PayrollBankTransferPreviewDto>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<PayrollBankTransferPreviewDto>.Fail(result.Error.Description));
        }
        return Ok(ApiResponse<PayrollBankTransferPreviewDto>.Ok(result.Value));
    }

    /// <summary>Télécharge le fichier CSV de virement bancaire (cycle Validé/Clôturé).</summary>
    [HttpGet("{id:guid}/bank-transfer/export")]
    [Authorize(Policy = PermissionPolicies.PayrollExport)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportBankTransfer(
        Guid id,
        [FromQuery] string format = "csv",
        [FromQuery] Guid? bankAccountId = null,
        [FromQuery] string? label = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new ExportPayrollBankTransferQuery(id, bankAccountId, label, format),
            cancellationToken);
        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("NotFound"))
                return NotFound(ApiResponse<object>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        }

        var file = result.Value;
        return File(file.Content, file.ContentType, file.FileName);
    }
}
