using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.DTOs;

public sealed record DepreciationRateCategoryDto(
    Guid Id,
    string Code,
    string Label,
    decimal LegalRatePercent,
    decimal UsefulLifeYears,
    string DefaultAssetAccount,
    string DefaultDepreciationAccount,
    string DefaultExpenseAccount,
    bool IsNonDepreciable);

/// <summary>
/// Paramètres du module Immobilisations du dossier (tenant) — support des exercices décalés
/// (plan « Exercices décalés », décisions D1/D2/D3). Contrat stable consommé par P4/frontend.
/// </summary>
public sealed record FixedAssetSettingsDto(
    int FiscalYearStartMonth,
    string FiscalYearLabelFormat,
    string FiscalYearLabelSample);

/// <summary>
/// Requête de mise à jour des paramètres Immobilisations du dossier (permission Accounting).
/// </summary>
public sealed record UpdateFixedAssetSettingsRequest(
    int FiscalYearStartMonth,
    string FiscalYearLabelFormat);

public sealed record CreateFixedAssetRequest(
    string Label,
    Guid DepreciationRateCategoryId,
    decimal AcquisitionCost,
    decimal CapitalizedFees,
    decimal ResidualValue,
    DateTime AcquisitionDate,
    string? Description,
    decimal VatAmount,
    string? Location,
    Guid? SupplierId,
    string? AssetAccountNumber,
    string? DepreciationAccountNumber,
    string? ExpenseAccountNumber,
    DepreciationMethod DepreciationMethod = DepreciationMethod.Linear,
    decimal? AccelerationCoefficient = null,
    decimal? DepreciationRatePercent = null,
    decimal? UsefulLifeYears = null);

public sealed record UpdateFixedAssetRequest(
    string Label,
    decimal AcquisitionCost,
    decimal CapitalizedFees,
    decimal ResidualValue,
    DateTime AcquisitionDate,
    string? Description,
    string? Location,
    string? AssetAccountNumber,
    string? DepreciationAccountNumber,
    string? ExpenseAccountNumber,
    Guid? DepreciationRateCategoryId = null,
    DepreciationMethod? DepreciationMethod = null,
    decimal? AccelerationCoefficient = null,
    decimal? DepreciationRatePercent = null,
    decimal? UsefulLifeYears = null,
    decimal? VatAmount = null);

public sealed record PreviewDepreciationScheduleRequest(
    DateTime? InServiceDate);

public sealed record PutFixedAssetInServiceRequest(
    DateTime InServiceDate,
    string CreditAccountNumber);

public sealed record PostDepreciationRunRequest(int FiscalYear);

public sealed record DisposeFixedAssetRequest(
    DateTime DisposalDate,
    decimal DisposalProceeds,
    string? TreasuryAccountNumber,
    string? ReceivableAccountNumber = null);

public sealed record DepreciationRunResultDto(
    int FiscalYear,
    int PostedCount,
    int SkippedCount,
    decimal TotalDepreciationAmount,
    IReadOnlyList<string> Errors,
    int AlreadyPostedCount = 0,
    string FiscalYearLabel = "");

public sealed record FixedAssetDto(
    Guid Id,
    string InventoryNumber,
    string Label,
    string? Description,
    FixedAssetStatus Status,
    string AssetAccountNumber,
    string DepreciationAccountNumber,
    string ExpenseAccountNumber,
    decimal AcquisitionCost,
    decimal CapitalizedFees,
    decimal ResidualValue,
    decimal TotalCapitalizedCost,
    decimal VatAmount,
    DateTime AcquisitionDate,
    DateTime? InServiceDate,
    DateTime? DisposalDate,
    Guid DepreciationRateCategoryId,
    string DepreciationRateCategoryLabel,
    decimal DepreciationRatePercent,
    decimal UsefulLifeYears,
    DepreciationMethod DepreciationMethod,
    decimal AccelerationCoefficient,
    decimal AccumulatedDepreciation,
    decimal NetBookValue,
    string? Location,
    string? CreditAccountNumber,
    Guid? SupplierId,
    Guid? SupplierInvoiceId,
    Guid? SupplierInvoiceLineId,
    bool VatCapitalized);

public sealed record DepreciationScheduleLineDto(
    Guid Id,
    int FiscalYear,
    int? PeriodMonth,
    decimal OpeningNbv,
    decimal NormalAnnualAmount,
    decimal PriorAccumulatedDepreciation,
    decimal DepreciationAmount,
    decimal AccumulatedDepreciation,
    decimal ClosingNbv,
    bool IsPosted,
    bool IsReversed);

public sealed record FixedAssetScheduleDto(
    Guid FixedAssetId,
    string InventoryNumber,
    string Label,
    DateTime AcquisitionDate,
    DateTime? InServiceDate,
    decimal TotalCapitalizedCost,
    decimal DepreciationRatePercent,
    decimal UsefulLifeYears,
    decimal DepreciableBase,
    IReadOnlyList<DepreciationScheduleLineDto> Lines,
    DepreciationMethod DepreciationMethod = DepreciationMethod.Linear,
    decimal AccelerationCoefficient = 1m);

public sealed record FixedAssetListResponse(
    IReadOnlyList<FixedAssetDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public enum CurrentYearPostingStatus
{
    None = 0,
    Partial = 1,
    FullyPosted = 2
}

public sealed record FixedAssetAmortizationTableRowDto(
    Guid AssetId,
    string InventoryNumber,
    string Label,
    FixedAssetStatus Status,
    Guid DepreciationRateCategoryId,
    string DepreciationRateCategoryLabel,
    DepreciationMethod DepreciationMethod,
    DateTime AcquisitionDate,
    DateTime? InServiceDate,
    decimal OriginValue,
    decimal AccumulatedDepreciation,
    decimal NetBookValue,
    int FiscalYear,
    decimal DotationCalculeeExercice,
    decimal DotationComptabiliseeExercice,
    CurrentYearPostingStatus PostingStatus);

public sealed record FixedAssetAmortizationTableResponse(
    IReadOnlyList<FixedAssetAmortizationTableRowDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int FiscalYear);

public enum AmortizationReportGroupingMode
{
    AssetAccount = 0,
    FiscalCategory = 1
}

public sealed record AmortizationReportHeaderDto(
    string CompanyName,
    int FiscalYear,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    DateTime GeneratedAtUtc);

public sealed record AmortizationReportAssetRowDto(
    Guid AssetId,
    string AssetAccountNumber,
    string InventoryNumber,
    string Label,
    DateTime AcquisitionDate,
    decimal OriginValue,
    decimal UsefulLifeYears,
    DepreciationMethod DepreciationMethod,
    decimal PriorAccumulatedDepreciation,
    decimal DotationCalculeeExercice,
    decimal DotationComptabiliseeExercice,
    decimal EndOfYearAccumulatedDepreciation,
    decimal EndOfYearNetBookValue,
    CurrentYearPostingStatus PostingStatus);

public sealed record AmortizationReportTotalsDto(
    decimal OriginValue,
    decimal PriorAccumulatedDepreciation,
    decimal DotationCalculeeExercice,
    decimal DotationComptabiliseeExercice,
    decimal EndOfYearAccumulatedDepreciation,
    decimal EndOfYearNetBookValue);

public sealed record AmortizationReportGroupDto(
    string GroupCode,
    string GroupLabel,
    IReadOnlyList<AmortizationReportAssetRowDto> Rows,
    AmortizationReportTotalsDto Subtotal);

public sealed record AmortizationReportSummaryRowDto(
    string NatureLabel,
    decimal OriginValue,
    decimal PriorAccumulatedDepreciation,
    decimal DotationCalculeeExercice,
    decimal EndOfYearNetBookValue);

public sealed record AmortizationReportInfoBoxDto(
    string CurrencyCode,
    DateTime GeneratedAtUtc,
    string ProductName);

public sealed record AmortizationReportResponse(
    AmortizationReportHeaderDto Header,
    AmortizationReportGroupingMode GroupingMode,
    IReadOnlyList<AmortizationReportGroupDto> Groups,
    AmortizationReportTotalsDto GrandTotal,
    IReadOnlyList<AmortizationReportSummaryRowDto> SummaryByNature,
    AmortizationReportInfoBoxDto InfoBox);
