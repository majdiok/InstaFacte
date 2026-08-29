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
            a.SupplierId,
            a.SupplierInvoiceId,
            a.SupplierInvoiceLineId,
            a.VatCapitalized);

    public static DepreciationScheduleLineDto ToDto(DepreciationScheduleLine l) => ToDto(l, false);

    /// <summary>
    /// Surcharge portant l'état d'extourne (T13, C6) : <paramref name="isReversed"/> reflète
    /// <c>JournalEntry.IsReversed</c> de l'écriture liée à la ligne (projection
    /// <c>GetScheduleLinesWithReversalStateAsync</c>). Une ligne peut être <c>IsPosted=false</c> +
    /// <c>IsReversed=true</c> après dé-postage (lien d'audit conservé) — le frontend ne bloque la
    /// régénération que sur <c>IsPosted &amp;&amp; !IsReversed</c>.
    /// </summary>
    public static DepreciationScheduleLineDto ToDto(DepreciationScheduleLine l, bool isReversed) =>
        new(l.Id, l.FiscalYear, l.PeriodMonth, l.OpeningNbv, l.NormalAnnualAmount,
            l.PriorAccumulatedDepreciation, l.DepreciationAmount, l.AccumulatedDepreciation,
            l.ClosingNbv, l.IsPosted, isReversed);
}