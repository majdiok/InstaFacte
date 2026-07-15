namespace FactuTrust.Domain.Enums;

/// <summary>
/// Fixed vocabulary for cash desk revenue (credit) classification.
/// Used only when OperationType = Credit.
/// </summary>
public enum CashRevenueCategory
{
    CashSalesReceipt = 0,
    ClientReceivablesReceipt = 1,
    PartnerContributionsReceipt = 2,
    BankCreditReceipt = 3,
    Other = 99
}

public static class CashRevenueCategoryExtensions
{
    public static string ToDisplayString(this CashRevenueCategory category) => category switch
    {
        CashRevenueCategory.CashSalesReceipt => "Encaissement ventes au comptant",
        CashRevenueCategory.ClientReceivablesReceipt => "Encaissement créances clients",
        CashRevenueCategory.PartnerContributionsReceipt => "Encaissements reçus des associés",
        CashRevenueCategory.BankCreditReceipt => "Encaissement crédit bancaire",
        CashRevenueCategory.Other => "Autres encaissements",
        _ => throw new ArgumentOutOfRangeException(nameof(category))
    };
}
