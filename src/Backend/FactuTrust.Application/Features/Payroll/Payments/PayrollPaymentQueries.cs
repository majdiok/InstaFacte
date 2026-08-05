using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Payments;

public sealed record GetPayrollRunPaymentsQuery(Guid RunId, bool IncludeCancelled = false)
    : IRequest<Result<IReadOnlyList<PayrollPaymentDto>>>;

public sealed class GetPayrollRunPaymentsQueryHandler
    : IRequestHandler<GetPayrollRunPaymentsQuery, Result<IReadOnlyList<PayrollPaymentDto>>>
{
    private readonly IPayrollPaymentRepository _payments;
    private readonly IPayrollRunRepository _runs;

    public GetPayrollRunPaymentsQueryHandler(IPayrollPaymentRepository payments, IPayrollRunRepository runs)
    {
        _payments = payments;
        _runs = runs;
    }

    public async Task<Result<IReadOnlyList<PayrollPaymentDto>>> Handle(
        GetPayrollRunPaymentsQuery request,
        CancellationToken cancellationToken)
    {
        var run = await _runs.GetByIdAsync(request.RunId, cancellationToken);
        if (run is null)
            return Result.Failure<IReadOnlyList<PayrollPaymentDto>>(Error.NotFound("PayrollRun", request.RunId));

        var payments = await _payments.ListByPayrollRunAsync(
            request.RunId, request.IncludeCancelled, cancellationToken);

        var payslipNames = (await _runs.GetByIdWithPayslipsAsync(request.RunId, cancellationToken))
            ?.Payslips.ToDictionary(p => p.Id, p => p.EmployeeName)
            ?? new Dictionary<Guid, string>();

        var dtos = payments.Select(p => PayrollPaymentMappings.ToDto(p, payslipNames)).ToList();
        return Result.Success<IReadOnlyList<PayrollPaymentDto>>(dtos);
    }
}

public sealed record GetPayslipPaymentsQuery(Guid PayslipId)
    : IRequest<Result<IReadOnlyList<PayrollPaymentLineDto>>>;

public sealed class GetPayslipPaymentsQueryHandler
    : IRequestHandler<GetPayslipPaymentsQuery, Result<IReadOnlyList<PayrollPaymentLineDto>>>
{
    private readonly IPayrollPaymentRepository _payments;
    private readonly IPayrollRunRepository _runs;

    public GetPayslipPaymentsQueryHandler(IPayrollPaymentRepository payments, IPayrollRunRepository runs)
    {
        _payments = payments;
        _runs = runs;
    }

    public async Task<Result<IReadOnlyList<PayrollPaymentLineDto>>> Handle(
        GetPayslipPaymentsQuery request,
        CancellationToken cancellationToken)
    {
        var payslip = await _runs.GetPayslipByIdAsync(request.PayslipId, cancellationToken);
        if (payslip is null)
            return Result.Failure<IReadOnlyList<PayrollPaymentLineDto>>(Error.NotFound("Payslip", request.PayslipId));

        var lines = await _payments.ListLinesByPayslipAsync(request.PayslipId, cancellationToken);
        var dtos = lines.Select(l => new PayrollPaymentLineDto
        {
            Id = l.Id,
            PayslipId = l.PayslipId,
            EmployeeId = l.EmployeeId,
            EmployeeName = payslip.EmployeeName,
            Amount = l.Amount.Amount,
            EmployeeAuxiliaryAccount = l.EmployeeAuxiliaryAccount
        }).ToList();

        return Result.Success<IReadOnlyList<PayrollPaymentLineDto>>(dtos);
    }
}

internal static class PayrollPaymentMappings
{
    public static PayrollPaymentDto ToDto(
        Domain.Entities.Payroll.PayrollPayment payment,
        IReadOnlyDictionary<Guid, string> payslipNames)
    {
        return new PayrollPaymentDto
        {
            Id = payment.Id,
            PayrollRunId = payment.PayrollRunId,
            Amount = payment.Amount.Amount,
            PaymentDate = payment.PaymentDate,
            Method = payment.Method.ToString(),
            MethodDisplay = payment.Method.ToDisplayString(),
            BankAccountId = payment.BankAccountId,
            Reference = payment.Reference,
            Notes = payment.Notes,
            IsCancelled = payment.IsCancelled,
            CancelledAt = payment.CancelledAt,
            CancelledBy = payment.CancelledBy,
            CancellationReason = payment.CancellationReason,
            CreatedAt = payment.CreatedAt,
            CreatedBy = payment.CreatedBy,
            Lines = payment.Lines.Select(l => new PayrollPaymentLineDto
            {
                Id = l.Id,
                PayslipId = l.PayslipId,
                EmployeeId = l.EmployeeId,
                EmployeeName = payslipNames.GetValueOrDefault(l.PayslipId) ?? string.Empty,
                Amount = l.Amount.Amount,
                EmployeeAuxiliaryAccount = l.EmployeeAuxiliaryAccount
            }).ToList()
        };
    }
}
