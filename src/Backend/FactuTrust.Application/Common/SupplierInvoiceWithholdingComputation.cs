using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common;

/// <summary>
/// Shared RS preview / certificate amounts for supplier invoices (same rules as <see cref="IWithholdingTaxService"/>).
/// </summary>
public static class SupplierInvoiceWithholdingComputation
{
    public static async Task<WithholdingTaxType?> ResolveTypeAsync(
        SupplierInvoice invoice,
        Supplier supplier,
        IWithholdingTaxRepository whRepo,
        CancellationToken ct = default)
    {
        WithholdingTaxType? wt = null;
        var typeId = invoice.WithholdingTaxTypeId ?? supplier.DefaultWithholdingTaxTypeId;
        if (typeId.HasValue)
            wt = await whRepo.GetTypeByIdAsync(typeId.Value, ct);

        wt ??= await whRepo.GetTypeByCodeAsync("RS7_000001", ct);
        wt ??= (await whRepo.GetActiveTypesAsync(ct))
            .FirstOrDefault(t => t.Code.StartsWith("RS7", StringComparison.OrdinalIgnoreCase));

        return wt;
    }

    public static WithholdingCalculationResultDto Calculate(
        SupplierInvoice invoice,
        Supplier supplier,
        WithholdingTaxType wt,
        IWithholdingTaxService calc,
        bool hasCNPC,
        bool hasPriseEnCharge,
        decimal? rs7TtcThresholdTnd = null)
    {
        var sub = invoice.SubTotal.Amount;
        var vat = invoice.TotalVat.Amount;
        var vatRate = sub > 0 ? Math.Round(vat / sub * 100m, 4) : 0m;
        var rateOverride = invoice.WithholdingRate ?? supplier.DefaultWithholdingRate ?? wt.DefaultRate;

        var request = new WithholdingCalculationRequest(
            sub,
            vatRate,
            wt.Code,
            supplier.IsResident,
            hasCNPC,
            hasPriseEnCharge,
            rateOverride,
            rs7TtcThresholdTnd);

        return calc.CalculateWithholding(request);
    }

    public static decimal TotalWithheldAmount(WithholdingCalculationResultDto calc) =>
        calc.WithholdingAmount + (calc.VatWithholdingAmount ?? 0m);

    /// <summary>Année utilisée pour le paramètre de seuil RS7 (TTC) : alignée sur la date de solde lorsqu’elle existe.</summary>
    public static int GetFiscalYearForRs7Threshold(DateTime invoiceDate, DateTime? paidAt) =>
        paidAt?.Year ?? invoiceDate.Year;

    /// <summary>Updates invoice preview fields after creation or when supplier RS settings apply.</summary>
    public static async Task ApplyWithholdingPreviewAsync(
        SupplierInvoice invoice,
        IWithholdingTaxRepository whRepo,
        IWithholdingTaxService calc,
        IWithholdingFiscalYearParameterRepository? fiscalYearParameters = null,
        CancellationToken ct = default)
    {
        var supplier = invoice.Supplier;
        if (!supplier.IsSubjectToWithholding)
        {
            invoice.SetWithholdingInfo(false, null, null, null);
            return;
        }

        var wt = await ResolveTypeAsync(invoice, supplier, whRepo, ct);
        if (wt is null)
        {
            invoice.SetWithholdingInfo(false, null, null, null);
            return;
        }

        decimal? rs7Th = null;
        if (fiscalYearParameters is not null)
        {
            var fiscalYear = GetFiscalYearForRs7Threshold(invoice.InvoiceDate, invoice.PaidAt);
            rs7Th = await fiscalYearParameters.GetRs7TtcThresholdAsync(fiscalYear, ct);
        }

        var result = Calculate(invoice, supplier, wt, calc, hasCNPC: false, hasPriseEnCharge: false, rs7Th);
        var totalRs = TotalWithheldAmount(result);
        invoice.SetWithholdingInfo(true, result.WithholdingRate, totalRs, wt.Id);
    }
}
