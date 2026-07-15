using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.FixedAssets;

public sealed record AmortizationReportAssetProjection(
    Guid AssetId,
    string AssetAccountNumber,
    string InventoryNumber,
    string Label,
    DateTime AcquisitionDate,
    decimal OriginValue,
    decimal UsefulLifeYears,
    DepreciationMethod DepreciationMethod,
    string CategoryCode,
    string CategoryLabel,
    decimal PriorAccumulatedDepreciation,
    decimal DotationCalculeeExercice,
    decimal DotationComptabiliseeExercice,
    decimal EndOfYearAccumulatedDepreciation,
    decimal EndOfYearNetBookValue,
    int TotalLineCount,
    int PostedLineCount);

public static class AmortizationReportAssembler
{
    public static AmortizationReportResponse Assemble(
        IReadOnlyList<AmortizationReportAssetProjection> projections,
        int fiscalYear,
        AmortizationReportGroupingMode groupingMode,
        string companyName)
    {
        var generatedAt = DateTime.UtcNow;
        var header = new AmortizationReportHeaderDto(
            companyName,
            fiscalYear,
            new DateTime(fiscalYear, 1, 1),
            new DateTime(fiscalYear, 12, 31),
            generatedAt);

        var rows = projections.Select(ToRow).OrderBy(r => r.AssetAccountNumber).ThenBy(r => r.InventoryNumber).ToList();

        var groups = groupingMode == AmortizationReportGroupingMode.FiscalCategory
            ? BuildCategoryGroups(projections, rows)
            : BuildAccountGroups(projections, rows);

        var grandTotal = SumRows(rows);
        var summaryByNature = BuildSummaryByNature(projections);

        var infoBox = new AmortizationReportInfoBoxDto(
            "TND",
            generatedAt,
            "InstaFact Comptabilité");

        return new AmortizationReportResponse(
            header,
            groupingMode,
            groups,
            grandTotal,
            summaryByNature,
            infoBox);
    }

    private static AmortizationReportAssetRowDto ToRow(AmortizationReportAssetProjection p) =>
        new(
            p.AssetId,
            p.AssetAccountNumber,
            p.InventoryNumber,
            p.Label,
            p.AcquisitionDate,
            p.OriginValue,
            p.UsefulLifeYears,
            p.DepreciationMethod,
            p.PriorAccumulatedDepreciation,
            p.DotationCalculeeExercice,
            p.DotationComptabiliseeExercice,
            p.EndOfYearAccumulatedDepreciation,
            p.EndOfYearNetBookValue,
            ResolvePostingStatus(p.TotalLineCount, p.PostedLineCount));

    private static CurrentYearPostingStatus ResolvePostingStatus(int totalLineCount, int postedLineCount)
    {
        if (postedLineCount <= 0)
            return CurrentYearPostingStatus.None;
        return postedLineCount >= totalLineCount
            ? CurrentYearPostingStatus.FullyPosted
            : CurrentYearPostingStatus.Partial;
    }

    private static IReadOnlyList<AmortizationReportGroupDto> BuildAccountGroups(
        IReadOnlyList<AmortizationReportAssetProjection> projections,
        IReadOnlyList<AmortizationReportAssetRowDto> rows)
    {
        return projections
            .GroupBy(p => GetAccountGroupKey(p.AssetAccountNumber))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var groupRows = rows.Where(r => g.Any(p => p.AssetId == r.AssetId)).ToList();
                var label = $"{g.Key} {g.Select(p => p.CategoryLabel).FirstOrDefault() ?? string.Empty}".Trim();
                return new AmortizationReportGroupDto(
                    g.Key,
                    label,
                    groupRows,
                    SumRows(groupRows));
            })
            .ToList();
    }

    private static IReadOnlyList<AmortizationReportGroupDto> BuildCategoryGroups(
        IReadOnlyList<AmortizationReportAssetProjection> projections,
        IReadOnlyList<AmortizationReportAssetRowDto> rows)
    {
        return projections
            .GroupBy(p => p.CategoryCode)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var groupRows = rows.Where(r => g.Any(p => p.AssetId == r.AssetId)).ToList();
                var label = g.Select(p => p.CategoryLabel).FirstOrDefault() ?? g.Key;
                return new AmortizationReportGroupDto(
                    g.Key,
                    label,
                    groupRows,
                    SumRows(groupRows));
            })
            .ToList();
    }

    private static IReadOnlyList<AmortizationReportSummaryRowDto> BuildSummaryByNature(
        IReadOnlyList<AmortizationReportAssetProjection> projections)
    {
        return projections
            .GroupBy(p => p.CategoryLabel)
            .OrderBy(g => g.Key)
            .Select(g => new AmortizationReportSummaryRowDto(
                g.Key,
                g.Sum(p => p.OriginValue),
                g.Sum(p => p.PriorAccumulatedDepreciation),
                g.Sum(p => p.DotationCalculeeExercice),
                g.Sum(p => p.EndOfYearNetBookValue)))
            .ToList();
    }

    private static AmortizationReportTotalsDto SumRows(IReadOnlyList<AmortizationReportAssetRowDto> rows) =>
        new(
            rows.Sum(r => r.OriginValue),
            rows.Sum(r => r.PriorAccumulatedDepreciation),
            rows.Sum(r => r.DotationCalculeeExercice),
            rows.Sum(r => r.DotationComptabiliseeExercice),
            rows.Sum(r => r.EndOfYearAccumulatedDepreciation),
            rows.Sum(r => r.EndOfYearNetBookValue));

    private static string GetAccountGroupKey(string accountNumber)
    {
        var n = accountNumber.Trim();
        if (n.Length <= 3)
            return n;
        return n[..3];
    }
}
