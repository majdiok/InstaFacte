using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.DTOs;

// ─────────────────────────────── Payroll payments (trésorerie) ───────────────────────────────

public sealed record PayrollPaymentDto
{
    public Guid Id { get; init; }
    public Guid PayrollRunId { get; init; }
    public decimal Amount { get; init; }
    public DateTime PaymentDate { get; init; }
    public string Method { get; init; } = null!;
    public string MethodDisplay { get; init; } = null!;
    public Guid? BankAccountId { get; init; }
    public string? Reference { get; init; }
    public string? Notes { get; init; }
    public bool IsCancelled { get; init; }
    public DateTime? CancelledAt { get; init; }
    public string? CancelledBy { get; init; }
    public string? CancellationReason { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? CreatedBy { get; init; }
    public IReadOnlyList<PayrollPaymentLineDto> Lines { get; init; } = Array.Empty<PayrollPaymentLineDto>();
}

public sealed record PayrollPaymentLineDto
{
    public Guid Id { get; init; }
    public Guid PayslipId { get; init; }
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = null!;
    public decimal Amount { get; init; }
    public string EmployeeAuxiliaryAccount { get; init; } = null!;
}

public sealed record RecordPayrollRunPaymentRequest
{
    public DateTime PaymentDate { get; init; }
    public PaymentMethod Method { get; init; } = PaymentMethod.BankTransfer;
    public Guid? BankAccountId { get; init; }
    public string? Reference { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<PayslipPaymentAmountRequest>? PayslipAmounts { get; init; }
}

public sealed record PayslipPaymentAmountRequest
{
    public Guid PayslipId { get; init; }
    public decimal Amount { get; init; }
}

public sealed record RecordPayslipPaymentRequest
{
    public DateTime PaymentDate { get; init; }
    public decimal Amount { get; init; }
    public PaymentMethod Method { get; init; } = PaymentMethod.BankTransfer;
    public Guid? BankAccountId { get; init; }
    public string? Reference { get; init; }
    public string? Notes { get; init; }
}

public sealed record CancelPayrollPaymentRequest
{
    public string Reason { get; init; } = null!;
}

public sealed record CancelAllPayrollRunPaymentsRequest
{
    public string Reason { get; init; } = null!;
}
