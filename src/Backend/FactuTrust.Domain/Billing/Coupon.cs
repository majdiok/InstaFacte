using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Billing;

/// <summary>Type de réduction d'un coupon.</summary>
public enum CouponType
{
    /// <summary>Pourcentage de réduction (Value entre 0 et 100).</summary>
    Percent = 0,
    /// <summary>Montant fixe en TND déduit (Value est un montant absolu).</summary>
    FixedAmount = 1
}

/// <summary>
/// Lot C3 — Coupon promotionnel.
///
/// Un coupon est utilisable par un (ou plusieurs) tenant(s) :
/// <list type="bullet">
///   <item>Type <see cref="CouponType.Percent"/> → réduction de <see cref="Value"/>%.</item>
///   <item>Type <see cref="CouponType.FixedAmount"/> → réduction de <see cref="Value"/> TND.</item>
/// </list>
///
/// Contraintes :
/// <list type="bullet">
///   <item>Si <see cref="MaxRedemptions"/> != null, le coupon est invalide une fois que ce nombre est atteint.</item>
///   <item>Validité bornée par <see cref="ValidFrom"/> / <see cref="ValidTo"/>.</item>
///   <item>Si <see cref="AppliesToPlanId"/> != null, le coupon ne s'applique qu'à ce plan précis.</item>
///   <item>Une seule redemption par <c>(CouponId, TenantId)</c> via index unique côté table <c>CouponRedemptions</c>.</item>
/// </list>
/// </summary>
public sealed class Coupon : Entity
{
    public string Code { get; private set; } = null!;
    public CouponType Type { get; private set; }
    public decimal Value { get; private set; }
    public int? DurationMonths { get; private set; }
    public int? MaxRedemptions { get; private set; }
    public int RedeemedCount { get; private set; }
    public DateTime ValidFrom { get; private set; }
    public DateTime ValidTo { get; private set; }
    public Guid? AppliesToPlanId { get; private set; }
    public bool IsActive { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public string? Notes { get; private set; }

    private Coupon() { }

    public static Coupon Create(
        string code,
        CouponType type,
        decimal value,
        DateTime validFrom,
        DateTime validTo,
        Guid createdByUserId,
        int? durationMonths = null,
        int? maxRedemptions = null,
        Guid? appliesToPlanId = null,
        string? notes = null)
    {
        return new Coupon
        {
            Code = (code ?? string.Empty).Trim().ToUpperInvariant(),
            Type = type,
            Value = value,
            DurationMonths = durationMonths,
            MaxRedemptions = maxRedemptions,
            RedeemedCount = 0,
            ValidFrom = validFrom,
            ValidTo = validTo,
            AppliesToPlanId = appliesToPlanId,
            IsActive = true,
            CreatedByUserId = createdByUserId,
            Notes = notes?.Trim()
        };
    }

    public void Update(
        CouponType type,
        decimal value,
        DateTime validFrom,
        DateTime validTo,
        int? durationMonths,
        int? maxRedemptions,
        Guid? appliesToPlanId,
        string? notes)
    {
        Type = type;
        Value = value;
        ValidFrom = validFrom;
        ValidTo = validTo;
        DurationMonths = durationMonths;
        MaxRedemptions = maxRedemptions;
        AppliesToPlanId = appliesToPlanId;
        Notes = notes?.Trim();
    }

    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;

    public void IncrementRedemptions() => RedeemedCount++;

    /// <summary>Indique si le coupon est utilisable maintenant (actif, dans la période, non saturé).</summary>
    public bool IsRedeemableNow()
    {
        if (!IsActive) return false;
        var nowUtc = DateTime.UtcNow;
        if (nowUtc < ValidFrom || nowUtc > ValidTo) return false;
        if (MaxRedemptions.HasValue && RedeemedCount >= MaxRedemptions.Value) return false;
        return true;
    }

    /// <summary>Calcule le montant économisé sur une base donnée (montant facture en TND).</summary>
    public decimal ComputeDiscount(decimal baseAmountTND)
    {
        if (baseAmountTND <= 0) return 0m;
        var discount = Type switch
        {
            CouponType.Percent => Math.Round(baseAmountTND * (Value / 100m), 3),
            CouponType.FixedAmount => Math.Min(Value, baseAmountTND),
            _ => 0m
        };
        return Math.Max(0m, discount);
    }
}

/// <summary>Lot C3 — Trace d'une utilisation de coupon par un tenant.</summary>
public sealed class CouponRedemption : Entity
{
    public Guid CouponId { get; private set; }
    public Guid TenantId { get; private set; }
    public DateTime RedeemedAt { get; private set; }
    public decimal AmountSavedTND { get; private set; }
    public Guid? AppliedToInvoiceId { get; private set; }

    private CouponRedemption() { }

    public static CouponRedemption Create(
        Guid couponId,
        Guid tenantId,
        decimal amountSavedTND,
        Guid? appliedToInvoiceId = null)
    {
        return new CouponRedemption
        {
            CouponId = couponId,
            TenantId = tenantId,
            RedeemedAt = DateTime.UtcNow,
            AmountSavedTND = amountSavedTND,
            AppliedToInvoiceId = appliedToInvoiceId
        };
    }
}
