using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Forecasting;

/// <summary>
/// A persisted promotion / discount recommendation produced by deterministic rules using sales history,
/// stock state, ABC/XYZ classification and the Tunisian commercial calendar.
/// Recommendations are inert: actual discounts must be applied through the existing catalog/pricing
/// mechanisms after explicit user confirmation.
/// </summary>
public sealed class PromotionRecommendation : Entity
{
    /// <summary>Target product. Null when CategoryId is set (category-wide recommendation).</summary>
    public Guid? ProductId { get; private set; }

    /// <summary>Target product category. Null when ProductId is set (single-product recommendation).</summary>
    public Guid? CategoryId { get; private set; }

    public DateTime GeneratedAt { get; private set; }

    public PromotionRecommendationType Type { get; private set; }

    /// <summary>Suggested discount percent (0–100). 0 means a non-price recommendation (e.g. PrePic highlight).</summary>
    public decimal SuggestedDiscountPercent { get; private set; }

    /// <summary>Estimated revenue uplift percent if the recommendation is applied (deterministic heuristic).</summary>
    public decimal ExpectedUpliftPercent { get; private set; }

    /// <summary>Inclusive start of the validity window (UTC date).</summary>
    public DateTime ValidFrom { get; private set; }

    /// <summary>Inclusive end of the validity window (UTC date).</summary>
    public DateTime ValidUntil { get; private set; }

    /// <summary>Short human-readable rationale displayed in the UI (e.g. "Surstock: 142 unités, rotation faible").</summary>
    public string ReasoningSummary { get; private set; } = "";

    /// <summary>JSON array of reason codes (e.g. ["Dormant90d", "Ramadan-J15", "ClassCZ"]) for traceability.</summary>
    public string ReasonCodesJson { get; private set; } = "[]";

    public PromotionRecommendationStatus Status { get; private set; }

    /// <summary>Linked Tunisian event code if this recommendation is calendar-driven.</summary>
    public string? RelatedEventCode { get; private set; }

    /// <summary>Linked discount/promotion entity once activated (free-form GUID for forward compatibility).</summary>
    public Guid? LinkedDiscountId { get; private set; }

    public DateTime? ProcessedAt { get; private set; }
    public string? ProcessedBy { get; private set; }

    private PromotionRecommendation() { }

    public static PromotionRecommendation Create(
        Guid? productId,
        Guid? categoryId,
        PromotionRecommendationType type,
        decimal suggestedDiscountPercent,
        decimal expectedUpliftPercent,
        DateTime validFrom,
        DateTime validUntil,
        string reasoningSummary,
        string reasonCodesJson,
        string? relatedEventCode = null)
    {
        if (productId is null && categoryId is null)
            throw new ArgumentException("At least one of ProductId or CategoryId is required");
        if (productId is not null && categoryId is not null)
            throw new ArgumentException("Provide either ProductId or CategoryId, not both");
        if (suggestedDiscountPercent < 0 || suggestedDiscountPercent > 100)
            throw new ArgumentException("SuggestedDiscountPercent must be in [0..100]", nameof(suggestedDiscountPercent));
        if (expectedUpliftPercent < 0)
            throw new ArgumentException("ExpectedUpliftPercent must be ≥ 0", nameof(expectedUpliftPercent));
        if (validUntil < validFrom)
            throw new ArgumentException("ValidUntil must be ≥ ValidFrom", nameof(validUntil));
        if (string.IsNullOrWhiteSpace(reasoningSummary))
            throw new ArgumentException("ReasoningSummary is required", nameof(reasoningSummary));

        return new PromotionRecommendation
        {
            ProductId = productId,
            CategoryId = categoryId,
            Type = type,
            SuggestedDiscountPercent = Math.Round(suggestedDiscountPercent, 2),
            ExpectedUpliftPercent = Math.Round(expectedUpliftPercent, 2),
            ValidFrom = validFrom.Date,
            ValidUntil = validUntil.Date,
            ReasoningSummary = reasoningSummary.Trim(),
            ReasonCodesJson = string.IsNullOrWhiteSpace(reasonCodesJson) ? "[]" : reasonCodesJson,
            Status = PromotionRecommendationStatus.Pending,
            RelatedEventCode = string.IsNullOrWhiteSpace(relatedEventCode) ? null : relatedEventCode,
            GeneratedAt = DateTime.UtcNow
        };
    }

    public void Accept(string userId)
    {
        if (Status != PromotionRecommendationStatus.Pending)
            throw new InvalidOperationException($"Cannot accept a recommendation in status {Status}");
        Status = PromotionRecommendationStatus.Accepted;
        ProcessedAt = DateTime.UtcNow;
        ProcessedBy = userId;
    }

    public void Dismiss(string userId)
    {
        if (Status is PromotionRecommendationStatus.Activated or PromotionRecommendationStatus.Expired)
            throw new InvalidOperationException($"Cannot dismiss a recommendation in status {Status}");
        Status = PromotionRecommendationStatus.Dismissed;
        ProcessedAt = DateTime.UtcNow;
        ProcessedBy = userId;
    }

    public void LinkActivatedDiscount(Guid discountId, string userId)
    {
        if (discountId == Guid.Empty)
            throw new ArgumentException("DiscountId is required", nameof(discountId));
        LinkedDiscountId = discountId;
        Status = PromotionRecommendationStatus.Activated;
        ProcessedAt = DateTime.UtcNow;
        ProcessedBy = userId;
    }

    public void MarkExpired()
    {
        if (Status is PromotionRecommendationStatus.Pending or PromotionRecommendationStatus.Accepted)
            Status = PromotionRecommendationStatus.Expired;
    }
}
