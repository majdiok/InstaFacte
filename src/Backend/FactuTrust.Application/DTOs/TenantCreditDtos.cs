using System.ComponentModel.DataAnnotations;

namespace FactuTrust.Application.DTOs;

/// <summary>Lot C3 — Vue admin d'un crédit tenant.</summary>
public sealed record TenantCreditDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string TenantName { get; init; } = null!;
    public decimal AmountTND { get; init; }
    public decimal ConsumedAmountTND { get; init; }
    public decimal RemainingTND { get; init; }
    public string Reason { get; init; } = null!;
    public Guid GrantedByUserId { get; init; }
    public DateTime GrantedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public Guid? RelatedInvoiceId { get; init; }
    public DateTime? RevokedAt { get; init; }
    public string? RevocationReason { get; init; }
    public bool IsActive { get; init; }
}

public sealed record TenantCreditsPageDto
{
    public IReadOnlyList<TenantCreditDto> Items { get; init; } = Array.Empty<TenantCreditDto>();
    public int TotalCount { get; init; }
    public int ActiveCount { get; init; }
    public decimal TotalGrantedTND { get; init; }
    public decimal TotalRemainingTND { get; init; }
}

public sealed record GrantTenantCreditRequest
{
    [Range(0.001, 1_000_000)]
    public decimal AmountTND { get; init; }

    [Required, StringLength(500, MinimumLength = 3)]
    public string Reason { get; init; } = null!;

    public DateTime? ExpiresAt { get; init; }
}

public sealed record RevokeTenantCreditRequest
{
    [Required, StringLength(500, MinimumLength = 3)]
    public string Reason { get; init; } = null!;
}
