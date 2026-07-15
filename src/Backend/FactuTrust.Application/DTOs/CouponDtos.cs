using System.ComponentModel.DataAnnotations;
using FactuTrust.Domain.Billing;

namespace FactuTrust.Application.DTOs;

/// <summary>Lot C3 — Vue admin d'un coupon.</summary>
public sealed record CouponDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = null!;
    public CouponType Type { get; init; }
    public string TypeDisplay { get; init; } = null!;
    public decimal Value { get; init; }
    public int? DurationMonths { get; init; }
    public int? MaxRedemptions { get; init; }
    public int RedeemedCount { get; init; }
    public DateTime ValidFrom { get; init; }
    public DateTime ValidTo { get; init; }
    public Guid? AppliesToPlanId { get; init; }
    public string? AppliesToPlanCode { get; init; }
    public bool IsActive { get; init; }
    public bool IsRedeemable { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record CouponRedemptionDto
{
    public Guid Id { get; init; }
    public Guid CouponId { get; init; }
    public Guid TenantId { get; init; }
    public string TenantName { get; init; } = null!;
    public DateTime RedeemedAt { get; init; }
    public decimal AmountSavedTND { get; init; }
    public Guid? AppliedToInvoiceId { get; init; }
}

public sealed record CouponsPageDto
{
    public IReadOnlyList<CouponDto> Items { get; init; } = Array.Empty<CouponDto>();
    public int TotalCount { get; init; }
    public int ActiveCount { get; init; }
    public int RedeemableCount { get; init; }
    public int TotalRedemptions { get; init; }
}

public sealed record CreateCouponRequest
{
    [Required, StringLength(40, MinimumLength = 3)]
    public string Code { get; init; } = null!;

    public CouponType Type { get; init; }

    [Range(0.001, 1_000_000)]
    public decimal Value { get; init; }

    [Range(1, 60)]
    public int? DurationMonths { get; init; }

    [Range(1, 1_000_000)]
    public int? MaxRedemptions { get; init; }

    public DateTime ValidFrom { get; init; }
    public DateTime ValidTo { get; init; }
    public Guid? AppliesToPlanId { get; init; }

    [StringLength(500)]
    public string? Notes { get; init; }
}

public sealed record UpdateCouponRequest
{
    public CouponType Type { get; init; }

    [Range(0.001, 1_000_000)]
    public decimal Value { get; init; }

    [Range(1, 60)]
    public int? DurationMonths { get; init; }

    [Range(1, 1_000_000)]
    public int? MaxRedemptions { get; init; }

    public DateTime ValidFrom { get; init; }
    public DateTime ValidTo { get; init; }
    public Guid? AppliesToPlanId { get; init; }

    [StringLength(500)]
    public string? Notes { get; init; }
}

public static class CouponTypeExtensions
{
    public static string ToDisplayString(this CouponType type) => type switch
    {
        CouponType.Percent => "Pourcentage (%)",
        CouponType.FixedAmount => "Montant fixe (TND)",
        _ => type.ToString()
    };
}
