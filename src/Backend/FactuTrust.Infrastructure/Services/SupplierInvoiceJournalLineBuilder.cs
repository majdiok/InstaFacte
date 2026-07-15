using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Builds journal lines for supplier invoices (goods vs immobilisations).
/// </summary>
internal static class SupplierInvoiceJournalLineBuilder
{
    internal sealed record BuiltLines(decimal GoodsHt, decimal GoodsVat, decimal AssetHt, decimal AssetVat);

    public static (List<JournalLineInput> Lines, BuiltLines Totals) Build(SupplierInvoice invoice)
    {
        decimal goodsHt = 0, goodsVat = 0, assetHt = 0, assetVat = 0;
        var assetDebits = new Dictionary<string, decimal>(StringComparer.Ordinal);

        foreach (var line in invoice.Lines)
        {
            if (line.IsFixedAsset)
            {
                assetHt += line.SubTotal.Amount;
                assetVat += line.VatAmount.Amount;
                var acc = string.IsNullOrWhiteSpace(line.AssetAccountNumber) ? "218" : line.AssetAccountNumber.Trim();
                assetDebits.TryGetValue(acc, out var sum);
                assetDebits[acc] = sum + line.SubTotal.Amount;
            }
            else
            {
                goodsHt += line.SubTotal.Amount;
                goodsVat += line.VatAmount.Amount;
            }
        }

        var lines = new List<JournalLineInput>();
        var label = invoice.InvoiceNumber;

        if (goodsHt > 0)
            lines.Add(new JournalLineInput("607", $"Achats — {label}", goodsHt, 0, null, ThirdPartyKind.None));
        if (goodsVat > 0)
            lines.Add(new JournalLineInput("43666", $"TVA déductible — {label}", goodsVat, 0, null, ThirdPartyKind.None));

        foreach (var kv in assetDebits.OrderBy(k => k.Key))
        {
            lines.Add(new JournalLineInput(kv.Key, $"Immobilisation — {label}", kv.Value, 0, null, ThirdPartyKind.None));
        }

        if (assetVat > 0)
            lines.Add(new JournalLineInput("43662", $"TVA déductible immo — {label}", assetVat, 0, null, ThirdPartyKind.None));

        var ttc = invoice.TotalAmount.Amount;
        var stamp = invoice.FiscalStampAmount?.Amount ?? 0;
        if (Math.Abs(stamp) > 0.0005m && stamp > 0)
        {
            lines.Add(new JournalLineInput("6371", $"Timbre fiscal — {label}", stamp, 0, null, ThirdPartyKind.None));
            ttc += stamp;
        }

        lines.Add(new JournalLineInput(
            "4011",
            $"Fournisseur — {label}",
            0,
            ttc,
            invoice.SupplierId,
            ThirdPartyKind.Supplier));

        return (lines, new BuiltLines(goodsHt, goodsVat, assetHt, assetVat));
    }
}