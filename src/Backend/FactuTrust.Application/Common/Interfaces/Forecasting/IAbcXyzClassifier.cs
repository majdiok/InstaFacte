using FactuTrust.Application.Features.Forecasting.Dtos;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces.Forecasting;

/// <summary>
/// Computes ABC (revenue contribution) and XYZ (demand variability) classifications for the product catalog
/// using the last 12 months of sales. Idempotent: rerunning yields the same result for the same window.
/// </summary>
public interface IAbcXyzClassifier
{
    /// <summary>Recompute and persist ABC/XYZ for every active stock-managed product.</summary>
    Task<int> ClassifyAsync(CancellationToken ct = default);

    Task<AbcXyzMatrixDto> GetMatrixAsync(
        Guid? warehouseId,
        AbcClass? abcFilter,
        XyzClass? xyzFilter,
        CancellationToken ct = default);
}
