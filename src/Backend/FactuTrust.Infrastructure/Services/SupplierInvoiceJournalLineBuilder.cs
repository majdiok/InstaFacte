using FactuTrust.Application.Features.Accounting;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Builds journal lines for supplier invoices (goods vs services vs immobilisations).
/// </summary>
internal static class SupplierInvoiceJournalLineBuilder
{
    internal sealed record BuiltLines(decimal GoodsHt, decimal GoodsVat, decimal AssetHt, decimal AssetVat)
    {
        public decimal ServicesHt { get; init; }
        public decimal ServicesVat { get; init; }
    }

    public static (List<JournalLineInput> Lines, BuiltLines Totals) Build(
        SupplierInvoice invoice,
        IReadOnlyDictionary<Guid, ProductType>? lineProductTypes = null)
    {
        decimal goodsHt = 0, goodsVat = 0, assetHt = 0, assetVat = 0, servicesHt = 0, servicesVat = 0;
        var assetDebits = new Dictionary<string, decimal>(StringComparer.Ordinal);

        foreach (var line in invoice.Lines)
        {
            if (line.IsFixedAsset)
            {
                assetHt += line.SubTotal.Amount;
                assetVat += line.VatAmount.Amount;
                var acc = string.IsNullOrWhiteSpace(line.AssetAccountNumber)
                    ? TunisianPostingAccounts.DefaultFixedAsset
                    : line.AssetAccountNumber.Trim();
                assetDebits.TryGetValue(acc, out var sum);
                assetDebits[acc] = sum + line.SubTotal.Amount;
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
