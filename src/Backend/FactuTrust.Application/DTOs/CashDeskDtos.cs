using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.DTOs;

/// <summary>
/// Request body for creating a cash desk operation (debit or credit).
/// </summary>
public sealed record CreateCashOperationRequest
{
    public CashOperationType OperationType { get; init; }
    public DateTime OperationDate { get; init; }
    public decimal Amount { get; init; }
    public PaymentMethod Method { get; init; }
    public string Label { get; init; } = null!;
    public CashExpenseCategory? Category { get; init; }
    public CashRevenueCategory? RevenueCategory { get; init; }
    public string? Reference { get; init; }
    public string? Notes { get; init; }

    /// <summary>
    /// Taux de TVA optionnel sur un encaissement « ventes au comptant » (Credit + CashSalesReceipt
    /// uniquement). Le frontend envoie la valeur numérique du taux (0/7/13/19) — acceptée telle
    /// quelle par <c>JsonStringEnumConverter</c> (AllowIntegerValues par défaut). Rejeté si le flag
    /// <c>CashDeskVatEnabled</c> est désactivé (cf. <c>CreateCashOperationCommandHandler</c>).
    /// </summary>
    public VatRate? VatRate { get; init; }
}

/// <summary>
/// Request body for cancelling a cash desk operation.
/// </summary>
public sealed record CancelCashOperationRequest
{
    public string CancellationReason { get; init; } = null!;
}

/// <summary>
/// One cash desk operation row (debit or credit).
/// </summary>
public sealed record CashOperationListItemDto
{
    public Guid Id { get; init; }
    public CashOperationType OperationType { get; init; }
    public string OperationTypeDisplay { get; init; } = null!;
    public DateTime OperationDate { get; init; }
    public PaymentMethod Method { get; init; }
    public string MethodDisplay { get; init; } = null!;
    public string Label { get; init; } = null!;
    public CashExpenseCategory? Category { get; init; }
    public string? CategoryDisplay { get; init; }
    public CashRevenueCategory? RevenueCategory { get; init; }
    public string? RevenueCategoryDisplay { get; init; }
    public string Document { get; init; } = null!;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = null!;
    public string? Reference { get; init; }
    public string? Notes { get; init; }
    public CashOperationStatus Status { get; init; }
    public CashOperationOrigin Origin { get; init; }
    public string? SourceType { get; init; }
    public Guid? SourceId { get; init; }
    public Guid? SourceInvoiceId { get; init; }
    public string? SourceInvoiceNumber { get; init; }

    /// <summary>When the operation is tied to a supplier payment, links to the supplier invoice.</summary>
    public Guid? SourceSupplierInvoiceId { get; init; }

    public string? SourceSupplierInvoiceNumber { get; init; }

    /// <summary>
    /// Taux de TVA en pourcentage entier (0/7/13/19), jamais l'enum brut (qui serait sérialisé en
    /// nom PascalCase). <c>null</c> quand <c>VatRate</c> n'a pas été renseigné sur l'opération.
    /// </summary>
    public int? VatRatePercent { get; init; }

    /// <summary>Montant HT dérivé via <c>CashOperationVatCalculator</c>. <c>null</c> si <see cref="VatRatePercent"/> est <c>null</c>.</summary>
    public decimal? HtAmount { get; init; }

    /// <summary>Montant de TVA dérivé via <c>CashOperationVatCalculator</c>. <c>null</c> si <see cref="VatRatePercent"/> est <c>null</c>.</summary>
    public decimal? VatAmount { get; init; }
}

/// <summary>
/// Balances for a selected month (primary) and year-to-date (secondary).
/// Now reflects real cash balance (credits - debits) per payment method.
/// </summary>
public sealed record CashDeskBalancesDto
{
    public string Currency { get; init; } = null!;
    public IReadOnlyList<CashDeskBalanceRowDto> Primary { get; init; } = Array.Empty<CashDeskBalanceRowDto>();
    public IReadOnlyList<CashDeskBalanceRowDto> Secondary { get; init; } = Array.Empty<CashDeskBalanceRowDto>();
    public decimal PrimaryTotal { get; init; }
    public decimal SecondaryTotal { get; init; }
    public decimal PrimaryCreditsTotal { get; init; }
    public decimal PrimaryDebitsTotal { get; init; }
    public decimal SecondaryCreditsTotal { get; init; }
    public decimal SecondaryDebitsTotal { get; init; }
}

/// <summary>
/// Balance row per payment method showing net balance (credits - debits).
/// </summary>
public sealed record CashDeskBalanceRowDto
{
    public PaymentMethod Method { get; init; }
    public string MethodDisplay { get; init; } = null!;
    public decimal Amount { get; init; }
    public decimal Credits { get; init; }
    public decimal Debits { get; init; }
}
