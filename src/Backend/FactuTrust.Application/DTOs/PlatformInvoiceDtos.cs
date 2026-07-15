using System.ComponentModel.DataAnnotations;
using FactuTrust.Domain.Billing;

namespace FactuTrust.Application.DTOs;

// ─────────────────────────────────────────────────────────────────────────────
// Lot C4 — DTOs facturation plateforme (Master DB).
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Vue plate d'une ligne de facture plateforme.</summary>
public sealed record PlatformInvoiceLineDto
{
    public Guid Id { get; init; }
    public string Description { get; init; } = null!;
    public decimal Quantity { get; init; }
    public decimal UnitPriceHT { get; init; }
    public decimal VatRate { get; init; }
    public decimal LineTotalHT { get; init; }
    public decimal LineTotalTTC { get; init; }
    public DateTime? RelatedPeriodFrom { get; init; }
    public DateTime? RelatedPeriodTo { get; init; }
}

/// <summary>Vue plate d'un reçu plateforme.</summary>
public sealed record PlatformReceiptDto
{
    public Guid Id { get; init; }
    public Guid InvoiceId { get; init; }
    public string ReceiptNumber { get; init; } = null!;
    public DateTime PaymentDate { get; init; }
    public PlatformPaymentMethod Method { get; init; }
    public string MethodDisplay { get; init; } = null!;
    public PlatformReceiptStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public string? Reference { get; init; }
    public decimal AmountTND { get; init; }
    public string? ProviderTxId { get; init; }
    public DateTime? ConfirmedAt { get; init; }
    public DateTime? CancelledAt { get; init; }
    public string? CancelledReason { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>Vue récap d'une facture pour la liste.</summary>
public sealed record PlatformInvoiceSummaryDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string TenantName { get; init; } = null!;
    public string? Number { get; init; }
    public DateTime InvoiceDate { get; init; }
    public DateTime? DueDate { get; init; }
    public PlatformInvoiceBillingType BillingType { get; init; }
    public string BillingTypeDisplay { get; init; } = null!;
    public PlatformInvoiceStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public decimal SubtotalHT { get; init; }
    public decimal VatAmount { get; init; }
    public decimal StampDuty { get; init; }
    public decimal TotalTTC { get; init; }
    public decimal TotalReceived { get; init; }
    public decimal RemainingAmount { get; init; }
    public DateTime? IssuedAt { get; init; }
    public DateTime? PaidAt { get; init; }
    public bool IsOverdue { get; init; }
}

/// <summary>Vue détaillée d'une facture (avec lignes + reçus).</summary>
public sealed record PlatformInvoiceDetailDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string TenantName { get; init; } = null!;
    public string? TenantNif { get; init; }
    public string? Number { get; init; }
    public int? SequenceYear { get; init; }
    public DateTime InvoiceDate { get; init; }
    public DateTime? DueDate { get; init; }
    public DateTime? PeriodFrom { get; init; }
    public DateTime? PeriodTo { get; init; }
    public PlatformInvoiceBillingType BillingType { get; init; }
    public string BillingTypeDisplay { get; init; } = null!;
    public PlatformInvoiceStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public decimal SubtotalHT { get; init; }
    public decimal VatAmount { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal CreditsApplied { get; init; }
    public decimal StampDuty { get; init; }
    public decimal TotalTTC { get; init; }
    public decimal TotalReceived { get; init; }
    public decimal RemainingAmount { get; init; }
    public Guid? CouponRedemptionId { get; init; }
    public string? PdfStorageKey { get; init; }
    public string? LegalMentions { get; init; }
    public DateTime? IssuedAt { get; init; }
    public DateTime? PaidAt { get; init; }
    public DateTime? CancelledAt { get; init; }
    public string? CancelledReason { get; init; }
    public Guid? RelatedInvoiceId { get; init; }
    public string? RelatedInvoiceNumber { get; init; }
    public DateTime CreatedAt { get; init; }
    public IReadOnlyList<PlatformInvoiceLineDto> Lines { get; init; } = Array.Empty<PlatformInvoiceLineDto>();
    public IReadOnlyList<PlatformReceiptDto> Receipts { get; init; } = Array.Empty<PlatformReceiptDto>();
}

/// <summary>Page filtrée + KPIs facturation.</summary>
public sealed record PlatformInvoicesPageDto
{
    public IReadOnlyList<PlatformInvoiceSummaryDto> Items { get; init; } = Array.Empty<PlatformInvoiceSummaryDto>();
    public int TotalCount { get; init; }
    public int DraftCount { get; init; }
    public int IssuedCount { get; init; }
    public int PaidCount { get; init; }
    public int OverdueCount { get; init; }
    public decimal TotalIssuedTtc { get; init; }
    public decimal TotalPaidTtc { get; init; }
    public decimal TotalOutstandingTtc { get; init; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Requêtes (CRUD invoices/lines/receipts/fiscal settings)
// ─────────────────────────────────────────────────────────────────────────────

public sealed record CreatePlatformInvoiceLineRequest
{
    [Required, StringLength(500, MinimumLength = 1)]
    public string Description { get; init; } = null!;

    [Range(0.001, 1_000_000)]
    public decimal Quantity { get; init; } = 1;

    [Range(0, 1_000_000)]
    public decimal UnitPriceHT { get; init; }

    [Range(0, 100)]
    public decimal VatRate { get; init; } = 19m;

    public DateTime? RelatedPeriodFrom { get; init; }
    public DateTime? RelatedPeriodTo { get; init; }
}

public sealed record CreatePlatformInvoiceRequest
{
    [Required]
    public Guid TenantId { get; init; }

    public PlatformInvoiceBillingType BillingType { get; init; } = PlatformInvoiceBillingType.Manual;

    public DateTime? InvoiceDate { get; init; }
    public DateTime? DueDate { get; init; }
    public DateTime? PeriodFrom { get; init; }
    public DateTime? PeriodTo { get; init; }

    [Range(0, 1_000_000)]
    public decimal DiscountAmount { get; init; }

    [Range(0, 1_000_000)]
    public decimal CreditsApplied { get; init; }

    public Guid? CouponRedemptionId { get; init; }

    [Required, MinLength(1)]
    public IReadOnlyList<CreatePlatformInvoiceLineRequest> Lines { get; init; } = Array.Empty<CreatePlatformInvoiceLineRequest>();
}

public sealed record CancelPlatformInvoiceRequest
{
    [Required, StringLength(500, MinimumLength = 3)]
    public string Reason { get; init; } = null!;
}

/// <summary>Lot C4 (complément) — Avoir partiel sans annulation de la facture d'origine.</summary>
public sealed record IssueCreditNoteRequest
{
    [Required, StringLength(500, MinimumLength = 3)]
    public string Reason { get; init; } = null!;

    /// <summary>
    /// Montant TTC à créditer (positif). Si null/&lt;=0, on rembourse le TTC total de la facture
    /// d'origine (équivalent à un avoir complet sans annuler).
    /// </summary>
    [Range(0, 10_000_000)]
    public decimal? AmountTtcTND { get; init; }
}

/// <summary>Lot C4 (complément) — Agrégation TVA d'un mois (Plateforme — DGI).</summary>
public sealed record PlatformVatPeriodDto
{
    public int Year { get; init; }
    public int Month { get; init; }
    public string MonthLabel { get; init; } = null!;
    public int InvoicesCount { get; init; }
    public decimal TotalHT { get; init; }
    public decimal TotalVat { get; init; }
    public decimal TotalStamp { get; init; }
    public decimal TotalTTC { get; init; }
    /// <summary>Nombre d'avoirs émis ce mois (BillingType=Refund).</summary>
    public int CreditNotesCount { get; init; }
    public decimal CreditNotesAmountTTC { get; init; }
}

public sealed record CreatePlatformReceiptRequest
{
    [Range(0.001, 10_000_000)]
    public decimal AmountTND { get; init; }

    public DateTime? PaymentDate { get; init; }

    public PlatformPaymentMethod Method { get; init; } = PlatformPaymentMethod.BankTransfer;

    [StringLength(120)]
    public string? Reference { get; init; }

    [StringLength(120)]
    public string? ProviderTxId { get; init; }

    public bool AutoConfirm { get; init; } = true;
}

public sealed record CancelPlatformReceiptRequest
{
    [Required, StringLength(500, MinimumLength = 3)]
    public string Reason { get; init; } = null!;
}

// ─────────────────────────────────────────────────────────────────────────────
// Fiscal settings
// ─────────────────────────────────────────────────────────────────────────────

public sealed record PlatformFiscalSettingsDto
{
    public Guid Id { get; init; }
    public string Nif { get; init; } = null!;
    public string? CodeTva { get; init; }
    public string CompanyName { get; init; } = null!;
    public string Address { get; init; } = null!;
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Website { get; init; }
    public string? Iban { get; init; }
    public string? BankName { get; init; }
    public bool ApplyVat { get; init; }
    public decimal DefaultVatRate { get; init; }
    public decimal TimbreFiscalAmount { get; init; }
    public bool ApplyClientWithholding { get; init; }
    public decimal ClientWithholdingRate { get; init; }
    public string InvoiceNumberPrefix { get; init; } = null!;
    public string ReceiptNumberPrefix { get; init; } = null!;
    public string? LegalMentions { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed record UpdatePlatformFiscalSettingsRequest
{
    [Required, StringLength(40, MinimumLength = 3)]
    public string Nif { get; init; } = null!;

    [StringLength(40)]
    public string? CodeTva { get; init; }

    [Required, StringLength(200, MinimumLength = 2)]
    public string CompanyName { get; init; } = null!;

    [Required, StringLength(500, MinimumLength = 4)]
    public string Address { get; init; } = null!;

    [StringLength(40)]
    public string? Phone { get; init; }

    [StringLength(254)]
    [EmailAddress]
    public string? Email { get; init; }

    [StringLength(254)]
    public string? Website { get; init; }

    [StringLength(60)]
    public string? Iban { get; init; }

    [StringLength(120)]
    public string? BankName { get; init; }

    public bool ApplyVat { get; init; }

    [Range(0, 100)]
    public decimal DefaultVatRate { get; init; }

    [Range(0, 100)]
    public decimal TimbreFiscalAmount { get; init; }

    public bool ApplyClientWithholding { get; init; }

    [Range(0, 100)]
    public decimal ClientWithholdingRate { get; init; }

    [Required, StringLength(10, MinimumLength = 1)]
    public string InvoiceNumberPrefix { get; init; } = "FT";

    [Required, StringLength(10, MinimumLength = 1)]
    public string ReceiptNumberPrefix { get; init; } = "FT-RC";

    [StringLength(2000)]
    public string? LegalMentions { get; init; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Display helpers
// ─────────────────────────────────────────────────────────────────────────────

public static class PlatformInvoiceDisplayExtensions
{
    public static string ToDisplayString(this PlatformInvoiceStatus s) => s switch
    {
        PlatformInvoiceStatus.Draft => "Brouillon",
        PlatformInvoiceStatus.Issued => "Émise",
        PlatformInvoiceStatus.Paid => "Payée",
        PlatformInvoiceStatus.PartiallyPaid => "Partiellement réglée",
        PlatformInvoiceStatus.Overdue => "En retard",
        PlatformInvoiceStatus.Cancelled => "Annulée",
        PlatformInvoiceStatus.Refunded => "Remboursée",
        _ => s.ToString()
    };

    public static string ToDisplayString(this PlatformInvoiceBillingType t) => t switch
    {
        PlatformInvoiceBillingType.Subscription => "Abonnement",
        PlatformInvoiceBillingType.SetupFee => "Frais de mise en place",
        PlatformInvoiceBillingType.Manual => "Manuelle",
        PlatformInvoiceBillingType.Refund => "Avoir",
        _ => t.ToString()
    };

    public static string ToDisplayString(this PlatformPaymentMethod m) => m switch
    {
        PlatformPaymentMethod.BankTransfer => "Virement bancaire",
        PlatformPaymentMethod.CardKonnect => "Carte (Konnect)",
        PlatformPaymentMethod.CardPaymee => "Carte (Paymee)",
        PlatformPaymentMethod.Cash => "Espèces",
        PlatformPaymentMethod.Manual => "Manuel",
        _ => m.ToString()
    };

    public static string ToDisplayString(this PlatformReceiptStatus s) => s switch
    {
        PlatformReceiptStatus.Pending => "En attente",
        PlatformReceiptStatus.Confirmed => "Confirmé",
        PlatformReceiptStatus.Cancelled => "Annulé",
        _ => s.ToString()
    };
}
