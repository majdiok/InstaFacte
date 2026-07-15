namespace FactuTrust.Domain.Enums;

/// <summary>
/// Represents the payment status of an invoice.
/// </summary>
public enum PaymentStatus
{
    /// <summary>
    /// No payment received yet.
    /// </summary>
    Unpaid = 0,

    /// <summary>
    /// Partial payment received.
    /// </summary>
    PartiallyPaid = 1,

    /// <summary>
    /// Full payment received.
    /// </summary>
    Paid = 2,

    /// <summary>
    /// Payment is overdue.
    /// </summary>
    Overdue = 3,

    /// <summary>
    /// Refunded to the client.
    /// </summary>
    Refunded = 4
}

/// <summary>
/// Represents the method of payment.
/// </summary>
public enum PaymentMethod
{
    /// <summary>
    /// Cash payment.
    /// </summary>
    Cash = 0,

    /// <summary>
    /// Bank transfer.
    /// </summary>
    BankTransfer = 1,

    /// <summary>
    /// Check payment.
    /// </summary>
    Check = 2,

    /// <summary>
    /// Credit/Debit card.
    /// </summary>
    Card = 3,

    /// <summary>
    /// Mobile payment (e.g., D17, Flouci).
    /// </summary>
    MobilePayment = 4,

    /// <summary>
    /// Bill of exchange / promissory note (traite, effet de commerce). Unlike the other modes this
    /// is not an immediate treasury movement: it creates an effet en portefeuille (client → 412,
    /// supplier → 403) with a maturity date, settled later at échéance.
    /// </summary>
    Traite = 5,

    /// <summary>
    /// Other payment method.
    /// </summary>
    Other = 99
}

public static class PaymentMethodExtensions
{
    public static string ToDisplayString(this PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Espèces",
        PaymentMethod.BankTransfer => "Virement bancaire",
        PaymentMethod.Check => "Chèque",
        PaymentMethod.Card => "Carte bancaire",
        PaymentMethod.MobilePayment => "Paiement mobile",
        PaymentMethod.Traite => "Traite (effet de commerce)",
        PaymentMethod.Other => "Autre",
        _ => throw new ArgumentOutOfRangeException(nameof(method))
    };
}
