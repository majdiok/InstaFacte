using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.DTOs;

public sealed record CreateBankDepositRequest
{
    public BankDepositType DepositType { get; init; }
    public DateTime DepositDate { get; init; }
    public Guid BankAccountId { get; init; }
    public decimal Amount { get; init; }
    public int Quantity { get; init; } = 1;
    public string? DepositSlipReference { get; init; }
    public string? Notes { get; init; }
}

public sealed record CancelBankDepositRequest
{
    public string CancellationReason { get; init; } = null!;
}

/// <summary>
/// Net cash-desk balance for a deposit instrument up to a given date (same rule as create bank deposit validation).
/// </summary>
public sealed record BankDepositAvailableBalanceDto
{
    public decimal Amount { get; init; }
    public string Currency { get; init; } = null!;
}

public sealed record BankDepositListItemDto
{
    public Guid Id { get; init; }
    public string Number { get; init; } = null!;
    public BankDepositType DepositType { get; init; }
    public string DepositTypeDisplay { get; init; } = null!;
    public DateTime DepositDate { get; init; }
    public Guid BankAccountId { get; init; }
    public string BankName { get; init; } = null!;
    public string? AccountDesignation { get; init; }
    public string BankCode { get; init; } = null!;
    public string Iban { get; init; } = null!;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = null!;
    public int Quantity { get; init; }
    public string? DepositSlipReference { get; init; }
    public string? Notes { get; init; }
    public BankDepositStatus Status { get; init; }
    public string CashOperationNumber { get; init; } = null!;
}
