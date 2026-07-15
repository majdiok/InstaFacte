using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Services;

public interface IDepreciationEngine
{
    /// <summary>
    /// Builds the full linear depreciation schedule (prorata temporis on entry/disposal years).
    /// </summary>
    IReadOnlyList<DepreciationScheduleLine> GenerateSchedule(FixedAsset asset, int? throughFiscalYear = null);

    /// <summary>
    /// Pro-rata depreciation for the fiscal year of disposal (months in service during that year).
    /// </summary>
    decimal CalculateDisposalYearDepreciation(
        FixedAsset asset,
        int fiscalYear,
        decimal priorAccumulatedDepreciation);
}