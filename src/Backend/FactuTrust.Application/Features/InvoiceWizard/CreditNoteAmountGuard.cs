using FactuTrust.Application.Common.Validation;
using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Features.InvoiceWizard;

/// <summary>
/// Compare le TTC commercial (lignes, hors timbre) d'un avoir au TTC commercial
/// disponible sur la facture d'origine. Formules homogènes : millimes, FODEC puis TVA.
/// </summary>
public static class CreditNoteAmountGuard
{
    public static decimal CommercialTtcFromDraftLines(
        IEnumerable<DraftInvoiceLine> lines, decimal fodecRatePercent)
    {
        return TunisianValidationRules.RoundToMillimes(
            lines.Sum(l => TunisianInvoiceLineCalculation.CalculateLine(
                l.Quantity,
                l.UnitPriceHT,
                l.DiscountType,
                l.DiscountValue,
                l.VatRate,
                l.FodecApplicable,
                fodecRatePercent).TotalTTC));
    }

    public static decimal CommercialTtcFromInvoice(Invoice invoice) =>
        Math.Abs(invoice.TotalAmount.Amount - invoice.FiscalStampAmount.Amount);

    public static bool DoesNotExceed(decimal draftCommercial, decimal availableCommercial) =>
        draftCommercial <= availableCommercial + TunisianValidationRules.NumericLimits.CalculationTolerance;
}
