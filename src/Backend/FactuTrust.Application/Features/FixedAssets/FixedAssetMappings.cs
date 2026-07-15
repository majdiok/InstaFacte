using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Features.FixedAssets;

internal static class FixedAssetMappings
{
    public static DepreciationRateCategoryDto ToDto(DepreciationRateCategory c) =>
        new(c.Id, c.Code, c.Label, c.LegalRatePercent, c.UsefulLifeYears,
            c.DefaultAssetAccount, c.DefaultDepreciationAccount, c.DefaultExpenseAccount, c.IsNonDepreciable);

    public static FixedAssetDto ToDto(FixedAsset a) =>
        new(
            a.Id,
            a.InventoryNumber,
            a.Label,
            a.Description,
            a.Status,
            a.AssetAccountNumber,
            a.DepreciationAccountNumber,
            a.ExpenseAccountNumber,
            a.AcquisitionCost,
            a.CapitalizedFees,
            a.ResidualValue,
            a.TotalCapitalizedCost,
            a.VatAmount,
            a.AcquisitionDate,
            a.InServiceDate,
            a.DisposalDate,
            a.DepreciationRateCategoryId,
            a.DepreciationRateCategory?.Label ?? string.Empty,
            a.DepreciationRatePercent,
            a.UsefulLifeYears,
            a.DepreciationMethod,
            a.AccelerationCoefficient,
            a.AccumulatedDepreciation,
            a.NetBookValue,
            a.Location,
            a.CreditAccountNumber,
            a.SupplierInvoiceId,
            a.SupplierInvoiceLineId);

    public static DepreciationScheduleLineDto ToDto(DepreciationScheduleLine l) =>
        new(l.Id, l.FiscalYear, l.PeriodMonth, l.OpeningNbv, l.NormalAnnualAmount,
            l.PriorAccumulatedDepreciation, l.DepreciationAmount, l.AccumulatedDepreciation,
            l.ClosingNbv, l.IsPosted);
}