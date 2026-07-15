namespace FactuTrust.Domain.Enums;

/// <summary>
/// Source instrument deposited to the bank (cash drawer classification).
/// </summary>
public enum BankDepositType
{
    Cash = 0,
    Check = 1,
    Draft = 2
}

public static class BankDepositTypeExtensions
{
    /// <summary>
    /// Maps deposit instrument to the cash desk payment method bucket used for balances.
    /// Drafts (traites) are tracked under bank transfer in the cash desk.
    /// </summary>
    public static PaymentMethod ToPaymentMethod(this BankDepositType type) => type switch
    {
        BankDepositType.Cash => PaymentMethod.Cash,
        BankDepositType.Check => PaymentMethod.Check,
        BankDepositType.Draft => PaymentMethod.BankTransfer,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public static string ToDisplayString(this BankDepositType type) => type switch
    {
        BankDepositType.Cash => "En espèces",
        BankDepositType.Check => "Chèques",
        BankDepositType.Draft => "Traites",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
