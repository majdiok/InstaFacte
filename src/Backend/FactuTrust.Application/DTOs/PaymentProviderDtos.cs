using System.ComponentModel.DataAnnotations;
using FactuTrust.Domain.Billing;

namespace FactuTrust.Application.DTOs;

// ─────────────────────────────────────────────────────────────────────────────
// Lot C5 — DTOs paiements plateforme.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Vue admin d'un provider configuré (sans révéler les secrets).</summary>
public sealed record PaymentProviderConfigDto
{
    public Guid Id { get; init; }
    public string ProviderCode { get; init; } = null!;
    public string DisplayName { get; init; } = null!;
    public bool IsEnabled { get; init; }
    public bool IsTestMode { get; init; }
    public string? AllowedReturnDomain { get; init; }
    /// <summary>Indique si l'admin a déjà saisi des secrets (sans les exposer).</summary>
    public bool HasSecrets { get; init; }
    public bool HasWebhookSecret { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed record PaymentProviderConfigsListDto
{
    public IReadOnlyList<PaymentProviderConfigDto> Items { get; init; } = Array.Empty<PaymentProviderConfigDto>();
    public int EnabledCount { get; init; }
}

public sealed record UpdatePaymentProviderConfigRequest
{
    [Required, StringLength(120, MinimumLength = 2)]
    public string DisplayName { get; init; } = null!;

    public bool IsEnabled { get; init; }
    public bool IsTestMode { get; init; }

    /// <summary>JSON brut des secrets, ex.: <c>{ "apiKey": "...", "merchantId": "..." }</c>. Sera chiffré au stockage.</summary>
    public string? SecretsJson { get; init; }

    [StringLength(500)]
    public string? WebhookSecret { get; init; }

    [StringLength(254)]
    public string? AllowedReturnDomain { get; init; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Intentions de paiement
// ─────────────────────────────────────────────────────────────────────────────

public sealed record PaymentIntentDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string TenantName { get; init; } = null!;
    public Guid InvoiceId { get; init; }
    public string? InvoiceNumber { get; init; }
    public string ProviderCode { get; init; } = null!;
    public string? ProviderRef { get; init; }
    public decimal AmountTND { get; init; }
    public PaymentIntentStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public string? ReturnUrl { get; init; }
    public string? RedirectUrl { get; init; }
    public string? FailureReason { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}

public sealed record PaymentIntentsPageDto
{
    public IReadOnlyList<PaymentIntentDto> Items { get; init; } = Array.Empty<PaymentIntentDto>();
    public int TotalCount { get; init; }
    public int SucceededCount { get; init; }
    public int PendingCount { get; init; }
    public int FailedCount { get; init; }
    public decimal TotalAmountSucceededTnd { get; init; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Checkout (côté tenant)
// ─────────────────────────────────────────────────────────────────────────────

public sealed record InitiateCheckoutRequest
{
    [Required]
    public Guid InvoiceId { get; init; }

    [Required, StringLength(20)]
    public string ProviderCode { get; init; } = null!;

    [StringLength(500)]
    public string? ReturnUrl { get; init; }
}

public sealed record InitiateCheckoutResponse
{
    public Guid IntentId { get; init; }
    public string ProviderCode { get; init; } = null!;
    public string Status { get; init; } = null!;
    /// <summary>URL de redirection vers le provider, null si Wire (pas de redirection).</summary>
    public string? RedirectUrl { get; init; }
    /// <summary>Pour le wire transfer : instructions IBAN à afficher au tenant.</summary>
    public WireTransferInstructionsDto? WireInstructions { get; init; }
}

public sealed record WireTransferInstructionsDto
{
    public string CompanyName { get; init; } = null!;
    public string Iban { get; init; } = null!;
    public string? BankName { get; init; }
    public string Reference { get; init; } = null!;
    public decimal AmountTND { get; init; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Wire receipt (saisie manuelle admin)
// ─────────────────────────────────────────────────────────────────────────────

public sealed record RegisterWireReceiptRequest
{
    [Required, StringLength(120, MinimumLength = 1)]
    public string Reference { get; init; } = null!;

    public DateTime? PaymentDate { get; init; }

    [Range(0.001, 10_000_000)]
    public decimal AmountTND { get; init; }

    /// <summary>Optionnel : justificatif (image/PDF base64 court).</summary>
    public string? ProofFileBase64 { get; init; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Display helpers
// ─────────────────────────────────────────────────────────────────────────────

public static class PaymentIntentStatusDisplayExtensions
{
    public static string ToDisplayString(this PaymentIntentStatus s) => s switch
    {
        PaymentIntentStatus.Created => "Créée",
        PaymentIntentStatus.RedirectIssued => "Redirigée",
        PaymentIntentStatus.Pending => "En attente",
        PaymentIntentStatus.Succeeded => "Réussie",
        PaymentIntentStatus.Failed => "Échouée",
        PaymentIntentStatus.Cancelled => "Annulée",
        PaymentIntentStatus.Refunded => "Remboursée",
        _ => s.ToString()
    };
}
