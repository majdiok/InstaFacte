using System.Globalization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Billing;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FactuTrust.Infrastructure.Services.Billing;

/// <summary>
/// Lot C4 — Rendu PDF des factures et reçus plateforme via QuestPDF.
///
/// Conforme aux mentions obligatoires DGI Tunisie :
/// <list type="bullet">
///   <item>En-tête : NIF, raison sociale, adresse, code TVA émetteur.</item>
///   <item>Lignes : Description / Quantité / PU HT / Total HT / TVA% / Total TVA / TTC.</item>
///   <item>Totaux : Total HT / Total TVA / Timbre fiscal / Total TTC.</item>
///   <item>QR code : encode <c>NIF|Number|Date|TTC</c>.</item>
///   <item>Mentions légales paramétrées en pied.</item>
/// </list>
/// </summary>
public sealed class PlatformInvoicePdfRenderer : IPlatformInvoicePdfRenderer
{
    private static readonly TextStyle DefaultTextStyle = TextStyle.Default
        .FontFamily("Lato", "Arial", "Helvetica", "DejaVu Sans", "Liberation Sans", "Calibri", "Segoe UI")
        .FontSize(10);

    private static readonly CultureInfo TndCulture = CultureInfo.GetCultureInfo("fr-TN");

    public byte[] RenderInvoice(PlatformInvoiceDetailDto invoice, PlatformFiscalSettingsDto fiscal)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(fiscal);

        var qrBytes = BuildQrPng(invoice, fiscal);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(DefaultTextStyle);

                page.Header().Element(c => ComposeHeader(c, invoice, fiscal));
                page.Content().Element(c => ComposeContent(c, invoice, fiscal, qrBytes));
                page.Footer().Element(c => ComposeFooter(c, fiscal));
            });
        });

        return document.GeneratePdf();
    }

    public byte[] RenderReceipt(PlatformReceiptDto receipt, PlatformInvoiceDetailDto parent, PlatformFiscalSettingsDto fiscal)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(fiscal);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(DefaultTextStyle);

                page.Header().Element(c => ComposeHeader(c, parent, fiscal, isReceipt: true, receiptNumber: receipt.ReceiptNumber));
                page.Content().Element(c => ComposeReceiptContent(c, receipt, parent));
                page.Footer().Element(c => ComposeFooter(c, fiscal));
            });
        });

        return document.GeneratePdf();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Composition
    // ─────────────────────────────────────────────────────────────────────────

    private static void ComposeHeader(IContainer container, PlatformInvoiceDetailDto invoice,
        PlatformFiscalSettingsDto fiscal, bool isReceipt = false, string? receiptNumber = null)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(fiscal.CompanyName).FontSize(14).Bold();
                    c.Item().Text(fiscal.Address).FontSize(9);
                    if (!string.IsNullOrWhiteSpace(fiscal.Phone)) c.Item().Text($"Tél : {fiscal.Phone}").FontSize(9);
                    if (!string.IsNullOrWhiteSpace(fiscal.Email)) c.Item().Text(fiscal.Email).FontSize(9);
                    c.Item().Text($"NIF : {fiscal.Nif}").FontSize(9).Bold();
                    if (!string.IsNullOrWhiteSpace(fiscal.CodeTva))
                        c.Item().Text($"Code TVA : {fiscal.CodeTva}").FontSize(9);
                });

                row.ConstantItem(220).Column(c =>
                {
                    c.Item().AlignRight().Text(isReceipt ? "REÇU DE PAIEMENT" : "FACTURE").FontSize(20).Bold();
                    var docNum = isReceipt ? (receiptNumber ?? "—") : (invoice.Number ?? "BROUILLON");
                    c.Item().AlignRight().Text(docNum).FontSize(13);
                    c.Item().AlignRight().Text($"Date : {invoice.InvoiceDate:dd/MM/yyyy}").FontSize(9);
                    if (invoice.DueDate.HasValue && !isReceipt)
                        c.Item().AlignRight().Text($"Échéance : {invoice.DueDate.Value:dd/MM/yyyy}").FontSize(9);
                    if (invoice.PeriodFrom.HasValue && invoice.PeriodTo.HasValue)
                        c.Item().AlignRight().Text($"Période : {invoice.PeriodFrom.Value:dd/MM/yyyy} → {invoice.PeriodTo.Value:dd/MM/yyyy}").FontSize(9);
                });
            });

            col.Item().PaddingTop(10).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);

            col.Item().PaddingTop(8).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Facturé à").FontSize(8).FontColor(Colors.Grey.Darken2);
                    c.Item().Text(invoice.TenantName).FontSize(11).Bold();
                    if (!string.IsNullOrWhiteSpace(invoice.TenantNif))
                        c.Item().Text($"NIF : {invoice.TenantNif}").FontSize(9);
                });

                row.ConstantItem(220).Column(c =>
                {
                    c.Item().AlignRight().Text(invoice.BillingTypeDisplay).FontSize(9).FontColor(Colors.Grey.Darken3);
                    if (invoice.Status == PlatformInvoiceStatus.Cancelled)
                        c.Item().AlignRight().Text("ANNULÉE").FontSize(11).Bold().FontColor(Colors.Red.Darken2);
                });
            });
        });
    }

    private static void ComposeContent(IContainer container, PlatformInvoiceDetailDto invoice,
        PlatformFiscalSettingsDto fiscal, byte[]? qrBytes)
    {
        container.PaddingVertical(12).Column(col =>
        {
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(5);
                    c.RelativeColumn(1.2f);
                    c.RelativeColumn(1.6f);
                    c.RelativeColumn(1f);
                    c.RelativeColumn(1.6f);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Description").Bold();
                    header.Cell().Element(HeaderCellRight).Text("Qté").Bold();
                    header.Cell().Element(HeaderCellRight).Text("PU HT").Bold();
                    header.Cell().Element(HeaderCellRight).Text("TVA%").Bold();
                    header.Cell().Element(HeaderCellRight).Text("Total HT").Bold();
                });

                foreach (var line in invoice.Lines)
                {
                    table.Cell().Element(BodyCell).Text(line.Description);
                    table.Cell().Element(BodyCellRight).Text(line.Quantity.ToString("0.###", TndCulture));
                    table.Cell().Element(BodyCellRight).Text(FormatTnd(line.UnitPriceHT));
                    table.Cell().Element(BodyCellRight).Text($"{line.VatRate:0.##}");
                    table.Cell().Element(BodyCellRight).Text(FormatTnd(line.LineTotalHT));
                }

                static IContainer HeaderCell(IContainer c) => c.PaddingVertical(6).PaddingHorizontal(4)
                    .Background(Colors.Grey.Lighten3).BorderBottom(0.5f).BorderColor(Colors.Grey.Medium);

                static IContainer HeaderCellRight(IContainer c) => HeaderCell(c).AlignRight();
                static IContainer BodyCell(IContainer c) => c.PaddingVertical(5).PaddingHorizontal(4).BorderBottom(0.3f).BorderColor(Colors.Grey.Lighten2);
                static IContainer BodyCellRight(IContainer c) => BodyCell(c).AlignRight();
            });

            col.Item().PaddingTop(14).Row(row =>
            {
                row.RelativeItem().AlignBottom().Element(qrCell =>
                {
                    if (qrBytes is { Length: > 0 })
                    {
                        qrCell.Width(96).Image(qrBytes);
                    }
                    else
                    {
                        qrCell.Text(string.Empty);
                    }
                });

                row.ConstantItem(260).Column(totals =>
                {
                    AddTotalRow(totals, "Sous-total HT", FormatTnd(invoice.SubtotalHT));
                    if (invoice.DiscountAmount > 0)
                        AddTotalRow(totals, "Remise", $"-{FormatTnd(invoice.DiscountAmount)}");
                    if (invoice.VatAmount > 0)
                        AddTotalRow(totals, $"TVA ({fiscal.DefaultVatRate:0.##}%)", FormatTnd(invoice.VatAmount));
                    if (invoice.StampDuty > 0)
                        AddTotalRow(totals, "Timbre fiscal", FormatTnd(invoice.StampDuty));
                    if (invoice.CreditsApplied > 0)
                        AddTotalRow(totals, "Crédits utilisés", $"-{FormatTnd(invoice.CreditsApplied)}");

                    totals.Item().PaddingTop(4).BorderTop(0.7f).BorderColor(Colors.Grey.Darken1);
                    totals.Item().Row(r =>
                    {
                        r.RelativeItem().PaddingVertical(4).Text("TOTAL TTC").Bold().FontSize(11);
                        r.ConstantItem(110).PaddingVertical(4).AlignRight().Text(FormatTnd(invoice.TotalTTC)).Bold().FontSize(11);
                    });

                    if (invoice.TotalReceived > 0)
                    {
                        AddTotalRow(totals, "Déjà payé", FormatTnd(invoice.TotalReceived));
                        AddTotalRow(totals, "Reste à payer", FormatTnd(invoice.RemainingAmount));
                    }
                });
            });

            if (!string.IsNullOrWhiteSpace(invoice.LegalMentions))
            {
                col.Item().PaddingTop(16).Text(invoice.LegalMentions!).FontSize(8).FontColor(Colors.Grey.Darken2);
            }
        });
    }

    private static void ComposeReceiptContent(IContainer container, PlatformReceiptDto receipt, PlatformInvoiceDetailDto parent)
    {
        container.PaddingVertical(20).Column(col =>
        {
            col.Item().Text("Encaissement enregistré").FontSize(13).Bold();
            col.Item().PaddingTop(10).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Facture liée :").FontSize(9).FontColor(Colors.Grey.Darken2);
                    c.Item().Text(parent.Number ?? "—").FontSize(11).Bold();
                    c.Item().PaddingTop(8).Text("Mode :").FontSize(9).FontColor(Colors.Grey.Darken2);
                    c.Item().Text(receipt.MethodDisplay).FontSize(11);
                    if (!string.IsNullOrWhiteSpace(receipt.Reference))
                    {
                        c.Item().PaddingTop(8).Text("Référence :").FontSize(9).FontColor(Colors.Grey.Darken2);
                        c.Item().Text(receipt.Reference!).FontSize(10);
                    }
                });

                row.ConstantItem(180).Column(c =>
                {
                    c.Item().AlignRight().Text("Montant encaissé").FontSize(9).FontColor(Colors.Grey.Darken2);
                    c.Item().AlignRight().Text(FormatTnd(receipt.AmountTND)).Bold().FontSize(18).FontColor(Colors.Green.Darken2);
                    c.Item().PaddingTop(8).AlignRight().Text($"Le {receipt.PaymentDate:dd/MM/yyyy}").FontSize(9);
                });
            });
        });
    }

    private static void ComposeFooter(IContainer container, PlatformFiscalSettingsDto fiscal)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
            col.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text(fiscal.LegalMentions ?? string.Empty).FontSize(7).FontColor(Colors.Grey.Darken2);
                row.ConstantItem(120).AlignRight().Text(t =>
                {
                    t.Span("Page ").FontSize(8).FontColor(Colors.Grey.Darken2);
                    t.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken2);
                    t.Span(" / ").FontSize(8).FontColor(Colors.Grey.Darken2);
                    t.TotalPages().FontSize(8).FontColor(Colors.Grey.Darken2);
                });
            });
        });
    }

    private static void AddTotalRow(ColumnDescriptor col, string label, string amount)
    {
        col.Item().Row(r =>
        {
            r.RelativeItem().PaddingVertical(2).Text(label).FontSize(9);
            r.ConstantItem(110).PaddingVertical(2).AlignRight().Text(amount).FontSize(9);
        });
    }

    /// <summary>QR code DGI : encode <c>NIF|Number|Date|TTC</c> selon spec FactuTrust.</summary>
    private static byte[] BuildQrPng(PlatformInvoiceDetailDto invoice, PlatformFiscalSettingsDto fiscal)
    {
        var payload = string.Join("|",
            fiscal.Nif,
            invoice.Number ?? "DRAFT",
            invoice.InvoiceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            invoice.TotalTTC.ToString("0.###", CultureInfo.InvariantCulture));

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        var pngQr = new PngByteQRCode(qrCodeData);
        return pngQr.GetGraphic(8);
    }

    private static string FormatTnd(decimal amount)
        => amount.ToString("N3", TndCulture) + " TND";
}
