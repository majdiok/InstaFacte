using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Features.SupplierInvoices.Services;

/// <summary>
/// Resolves the invoice number for supplier invoice creation (auto-reserve vs manual entry).
/// </summary>
public static class SupplierInvoiceNumberResolver
{
    public static async Task<Result<string>> ResolveAsync(
        string? invoiceNumber,
        bool useSuggestedNumber,
        DateTime invoiceDate,
        ISupplierInvoiceNumberService numberService,
        CancellationToken cancellationToken)
    {
        var trimmed = invoiceNumber?.Trim();

        if (useSuggestedNumber || string.IsNullOrWhiteSpace(trimmed))
        {
            var reserved = await numberService.ReserveNextAsync(invoiceDate, cancellationToken);
            return Result.Success(reserved);
        }

        if (!await numberService.IsAvailableAsync(trimmed, cancellationToken))
        {
            var nextSuggested = await SafePreviewAsync(numberService, invoiceDate, cancellationToken);
            var metadata = new Dictionary<string, object?>
            {
                ["suggestedInvoiceNumber"] = nextSuggested,
                ["conflictingInvoiceNumber"] = trimmed
            };
            return Result.Failure<string>(Error.Conflict(
                $"Le numéro de facture '{trimmed}' existe déjà. Veuillez en choisir un autre.",
                metadata));
        }

        return Result.Success(trimmed);
    }

    private static async Task<string?> SafePreviewAsync(
        ISupplierInvoiceNumberService numberService,
        DateTime invoiceDate,
        CancellationToken cancellationToken)
    {
        try
        {
            return await numberService.PreviewNextAsync(invoiceDate, cancellationToken);
        }
        catch
        {
            return null;
        }
    }
}
