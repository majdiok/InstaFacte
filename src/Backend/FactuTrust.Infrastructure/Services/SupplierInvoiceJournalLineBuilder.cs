using FactuTrust.Application.Features.Accounting;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Builds journal lines for supplier invoices (goods vs services vs immobilisations).
/// </summary>
internal static class SupplierInvoiceJournalLineBuilder
{
    /// <summary>
    /// Effective (category-resolved) asset account and VAT-capitalization decision for a single
    /// fixed-asset supplier invoice line — resolved by the caller (<c>AccountingService</c>), which knows
    /// the line's <see cref="DepreciationRateCategory"/> (the builder itself only sees invoice lines).
    /// </summary>
    internal readonly record struct FixedAssetLineClassification(string AssetAccountNumber, bool VatCapitalized);

    internal sealed record BuiltLines(decimal GoodsHt, decimal GoodsVat, decimal AssetHt, decimal AssetVat)
    {
        public decimal ServicesHt { get; init; }
        public decimal ServicesVat { get; init; }
    }

    public static (List<JournalLineInput> Lines, BuiltLines Totals) Build(
        SupplierInvoice invoice,
        IReadOnlyDictionary<Guid, ProductType>? lineProductTypes = null,
        IReadOnlyDictionary<Guid, FixedAssetLineClassification>? fixedAssetClassifications = null)
    {
        decimal goodsHt = 0, goodsVat = 0, assetHt = 0, assetVat = 0, servicesHt = 0, servicesVat = 0;
        var assetDebits = new Dictionary<string, decimal>(StringComparer.Ordinal);

        foreach (var line in invoice.Lines)
        {
            if (line.IsFixedAsset)
            {
                assetHt += line.SubTotal.Amount;

                string acc;
                bool vatCapitalized;
                if (fixedAssetClassifications is not null
                    && fixedAssetClassifications.TryGetValue(line.Id, out var classification))
                {
                    acc = classification.AssetAccountNumber;
                    vatCapitalized = classification.VatCapitalized;
                }
                else
                {
                    acc = string.IsNullOrWhiteSpace(line.AssetAccountNumber)
                        ? TunisianPostingAccounts.DefaultFixedAsset
                        : line.AssetAccountNumber.Trim();
                    vatCapitalized = false;
                }

                // Capitalized VAT (passenger vehicles) is debited directly to the asset account instead of
                // the aggregate 43662 line — absence of classification preserves the exact prior behavior.
                var debitAmount = line.SubTotal.Amount;
                if (vatCapitalized)
                    debitAmount += line.VatAmount.Amount;
                else
                    assetVat += line.VatAmount.Amount;

                assetDebits.TryGetValue(acc, out var sum);
                assetDebits[acc] = sum + debitAmount;
                continue;
            }


            if (lineProductTypes is not null
                && lineProductTypes.TryGetValue(line.ProductId, out var productType)
                && productType == ProductType.Service)
            {
                servicesHt += line.SubTotal.Amount;
                servicesVat += line.VatAmount.Amount;
                continue;
            }

            goodsHt += line.SubTotal.Amount;
            goodsVat += line.VatAmount.Amount;
        }

        var lines = new List<JournalLineInput>();
        var label = invoice.InvoiceNumber;

        if (goodsHt > 0)
            lines.Add(new JournalLineInput(TunisianPostingAccounts.PurchasesOfGoods, $"Achats — {label}", goodsHt, 0, null, ThirdPartyKind.None));
        if (servicesHt > 0)
            lines.Add(new JournalLineInput(TunisianPostingAccounts.PurchasesOfServices, $"Prestations — {label}", servicesHt, 0, null, ThirdPartyKind.None));

        var deductibleVat = goodsVat + servicesVat;
        if (deductibleVat > 0)
            lines.Add(new JournalLineInput(TunisianPostingAccounts.VatDeductibleGoods, $"TVA déductible — {label}", deductibleVat, 0, null, ThirdPartyKind.None));

        foreach (var kv in assetDebits.OrderBy(k => k.Key))
        {
            lines.Add(new JournalLineInput(kv.Key, $"Immobilisation — {label}", kv.Value, 0, null, ThirdPartyKind.None));
        }

        if (assetVat > 0)
            lines.Add(new JournalLineInput(TunisianPostingAccounts.VatDeductibleFixedAssets, $"TVA déductible immo — {label}", assetVat, 0, null, ThirdPartyKind.None));

        var ttc = invoice.TotalAmount.Amount;
        var stamp = invoice.FiscalStampAmount?.Amount ?? 0;
        if (Math.Abs(stamp) > 0.0005m && stamp > 0)
        {
            lines.Add(new JournalLineInput(TunisianPostingAccounts.FiscalStampOnPurchase, $"Timbre fiscal — {label}", stamp, 0, null, ThirdPartyKind.None));
            ttc += stamp;
        }

        lines.Add(new JournalLineInput(
            TunisianPostingAccounts.Supplier,
            $"Fournisseur — {label}",
            0,
            ttc,
            invoice.SupplierId,
            ThirdPartyKind.Supplier));

        return (lines, new BuiltLines(goodsHt, goodsVat, assetHt, assetVat)
        {
            ServicesHt = servicesHt,
            ServicesVat = servicesVat
        });
    }
}
