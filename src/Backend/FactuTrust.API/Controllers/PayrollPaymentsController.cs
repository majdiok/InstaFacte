using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll.Payments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>Paiements de paie (lien trésorerie).</summary>
[ApiController]
[Route("api/payroll")]
[Authorize]
public class PayrollPaymentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PayrollPaymentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("runs/{runId:guid}/payments")]
    [Authorize(Policy = PermissionPolicies.PayrollPay)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> RecordRunPayment(
        Guid runId,
        [FromBody] RecordPayrollRunPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RecordPayrollRunPaymentCommand(runId, request), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        return CreatedAtAction(
            nameof(GetRunPayments),
            new { runId },
            ApiResponse<Guid>.Ok(result.Value, "Paiement enregistré."));
    }

    [HttpPost("payslips/{payslipId:guid}/payments")]
    [Authorize(Policy = PermissionPolicies.PayrollPay)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> RecordPayslipPayment(
        Guid payslipId,
        [FromBody] RecordPayslipPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RecordPayslipPaymentCommand(payslipId, request), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        return Ok(ApiResponse<Guid>.Ok(result.Value, "Paiement enregistré."));
    }

    [HttpGet("runs/{runId:guid}/payments")]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PayrollPaymentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRunPayments(
        Guid runId,
        [FromQuery] bool includeCancelled = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetPayrollRunPaymentsQuery(runId, includeCancelled), cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<IReadOnlyList<PayrollPaymentDto>>.Fail(result.Error.Description));
        return Ok(ApiResponse<IReadOnlyList<PayrollPaymentDto>>.Ok(result.Value));
    }

    [HttpGet("payslips/{payslipId:guid}/payments")]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PayrollPaymentLineDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPayslipPayments(Guid payslipId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPayslipPaymentsQuery(payslipId), cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<IReadOnlyList<PayrollPaymentLineDto>>.Fail(result.Error.Description));
        return Ok(ApiResponse<IReadOnlyList<PayrollPaymentLineDto>>.Ok(result.Value));
    }

    [HttpPost("payments/{paymentId:guid}/cancel")]
    [Authorize(Policy = PermissionPolicies.PayrollPay)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelPayment(
        Guid paymentId,
        [FromBody] CancelPayrollPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CancelPayrollPaymentCommand(paymentId, request.Reason), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Paiement annulé."));
    }

    [HttpPost("runs/{runId:guid}/payments/cancel-all")]
    [Authorize(Policy = PermissionPolicies.PayrollPay)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelAllRunPayments(
        Guid runId,
        [FromBody] CancelAllPayrollRunPaymentsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CancelAllPayrollRunPaymentsCommand(runId, request.Reason), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Tous les paiements ont été annulés."));
    }
}
