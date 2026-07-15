using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Forecasting;

/// <summary>
/// ABC/XYZ classification of a product computed from the last 12 months of sales.
/// One row per product (the latest classification supersedes any previous one — see ComputedAt).
/// Used by the forecasting engine to differentiate replenishment and promotion strategies
/// (AX = stock fort + auto-réappro, CZ = candidat déstockage, etc.).
/// </summary>
public sealed class ProductAbcXyzClassification : Entity
{
    public Guid ProductId { get; private set; }

    public DateTime ComputedAt { get; private set; }

    public AbcClass AbcClass { get; private set; }

    public XyzClass XyzClass { get; private set; }

    /// <summary>Cumulative revenue percent at this product's position when sorted by revenue desc (0..100).</summary>
    public decimal CumulativeRevenuePercent { get; private set; }

    /// <summary>Coefficient of variation of monthly demand (σ/μ).</summary>
    public decimal DemandCv { get; private set; }

    /// <summary>Total revenue for the product over the reference window (millimes-precision decimal).</summary>
    public decimal ReferenceRevenue { get; private set; }

    /// <summary>Number of months with at least one sale in the reference window.</summary>
    public int ActiveMonths { get; private set; }

    private ProductAbcXyzClassification() { }

    public static ProductAbcXyzClassification Create(
        Guid productId,
        AbcClass abc,
        XyzClass xyz,
        decimal cumulativeRevenuePercent,
        decimal demandCv,
        decimal referenceRevenue,
        int activeMonths)
    {
        if (productId == Guid.Empty)
            throw new ArgumentException("ProductId is required", nameof(productId));
        if (cumulativeRevenuePercent < 0 || cumulativeRevenuePercent > 100)
            throw new ArgumentException("CumulativeRevenuePercent must be in [0..100]", nameof(cumulativeRevenuePercent));
        if (demandCv < 0)
            throw new ArgumentException("DemandCv must be ≥ 0", nameof(demandCv));
        if (referenceRevenue < 0)
            throw new ArgumentException("ReferenceRevenue must be ≥ 0", nameof(referenceRevenue));
        if (activeMonths < 0 || activeMonths > 12)
            throw new ArgumentException("ActiveMonths must be in [0..12]", nameof(activeMonths));

        return new ProductAbcXyzClassification
        {
            ProductId = productId,
            ComputedAt = DateTime.UtcNow,
            AbcClass = abc,
            XyzClass = xyz,
            CumulativeRevenuePercent = Math.Round(cumulativeRevenuePercent, 4),
            DemandCv = Math.Round(demandCv, 6),
            ReferenceRevenue = Math.Round(referenceRevenue, 3),
            ActiveMonths = activeMonths
        };
    }

    /// <summary>Convenience: 9-cell matrix code (e.g. "AX", "BZ", "C-").</summary>
    public string MatrixCode =>
        $"{(AbcClass == AbcClass.Unclassified ? "-" : AbcClass.ToString())}" +
        $"{(XyzClass == XyzClass.Unclassified ? "-" : XyzClass.ToString())}";
}
