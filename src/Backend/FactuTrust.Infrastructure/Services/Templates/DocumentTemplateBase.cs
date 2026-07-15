using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Common.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FactuTrust.Infrastructure.Services.Templates;

/// <summary>
/// Base commune des modèles visuels unifiés. Fournit la mise en page de page A4 et une bibliothèque
/// de blocs réutilisables (en-tête émetteur, bloc tiers, tableau des lignes, ventilation TVA, totaux,
/// montant en lettres, pied). Chaque preset compose ces blocs à sa manière avec sa propre palette.
/// </summary>
public abstract class DocumentTemplateBase : IDocumentTemplate
{
    public abstract string Key { get; }

    protected abstract DocumentTemplateTheme Theme { get; }

    protected static readonly TextStyle BaseTextStyle = TextStyle.Default
        .FontFamily("Lato", "Arial", "Helvetica", "DejaVu Sans", "Liberation Sans", "Calibri", "Segoe UI")
        .FontSize(9);

    public byte[] Render(DocumentRenderModel model)
    {
        QuestPdfBootstrap.EnsureInitialized();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(BaseTextStyle);
                ComposePage(page, model);
            });
        });

        return document.GeneratePdf();
    }

    protected abstract void ComposePage(PageDescriptor page, DocumentRenderModel model);

    // ---------- Helpers de formatage ----------

    protected static string Money(decimal value, string currency) => $"{value:N3} {currency}";
    protected static string MoneySigned(decimal value, string currency) => $"{value:+0.000;-0.000;0.000} {currency}";

    // ---------- Blocs réutilisables ----------

    /// <summary>En-tête émetteur (nom, raison commerciale, adresse, contact, MF/RC, RIB).</summary>
    protected void ComposeIssuerColumn(ColumnDescriptor col, PartyRenderInfo issuer)
    {
        col.Item().Text(string.IsNullOrWhiteSpace(issuer.Name) ? "Émetteur" : issuer.Name).FontSize(14).Bold();
        if (!string.IsNullOrWhiteSpace(issuer.TradeName))
            col.Item().Text(issuer.TradeName!).FontSize(9).FontColor(Colors.Grey.Darken1);

        foreach (var line in issuer.AddressLines)
            col.Item().Text(line).FontSize(8).FontColor(Theme.MutedText);

        var contact = new List<string>();
        if (!string.IsNullOrWhiteSpace(issuer.Phone)) contact.Add(issuer.Phone!);
        if (!string.IsNullOrWhiteSpace(issuer.Email)) contact.Add(issuer.Email!);
        if (contact.Count > 0)
            col.Item().Text(string.Join(" · ", contact)).FontSize(8).FontColor(Theme.MutedText);

        var tax = new List<string>();
        if (!string.IsNullOrWhiteSpace(issuer.TaxId)) tax.Add($"MF: {issuer.TaxId}");
        if (!string.IsNullOrWhiteSpace(issuer.CommerceRegistry)) tax.Add($"RC: {issuer.CommerceRegistry}");
        if (tax.Count > 0)
            col.Item().Text(string.Join(" | ", tax)).FontSize(8);

        if (!string.IsNullOrWhiteSpace(issuer.Rib))
        {
            var ribLine = string.IsNullOrWhiteSpace(issuer.BankName) ? $"RIB: {issuer.Rib}" : $"RIB: {issuer.BankName} {issuer.Rib}";
            col.Item().Text(ribLine).FontSize(8).FontColor(Theme.MutedText);
        }
    }

    /// <summary>Bloc tiers (client / fournisseur / destinataire).</summary>
    protected void ComposePartyColumn(ColumnDescriptor col, DocumentRenderModel model)
    {
        col.Item().Text($"{model.PartyLabel} :").FontSize(9).Bold().FontColor(Colors.Grey.Darken1);
        col.Item().PaddingTop(4).Text(model.Party.Name).Bold();
        if (!string.IsNullOrWhiteSpace(model.Party.TaxId))
            col.Item().Text($"N° Fisc. : {model.Party.TaxId}").FontSize(9);
        foreach (var line in model.Party.AddressLines)
            col.Item().Text(line).FontSize(9);
        if (!string.IsNullOrWhiteSpace(model.Party.Email))
            col.Item().Text(model.Party.Email!).FontSize(9);
    }

    /// <summary>Métadonnées document (n°, dates, statut, références…).</summary>
    protected void ComposeMetaColumn(ColumnDescriptor col, DocumentRenderModel model)
    {
        var first = true;
        foreach (var item in model.MetaItems)
        {
            var line = col.Item();
            if (!first) line = line.PaddingTop(2);
            first = false;
            line.Text($"{item.Label} : {item.Value}").FontSize(9);
        }
    }

    /// <summary>Tableau des lignes du document.</summary>
    protected void ComposeLinesTable(IContainer container, DocumentRenderModel model,
        bool showLineNumber = true, bool showRef = true, bool showUnit = true)
    {
        var showDiscount = model.ShowDiscountColumn;
        var showDelivery = model.ShowDeliveryQuantities;

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                if (showLineNumber) columns.ConstantColumn(22);
                if (showRef) columns.ConstantColumn(48);
                columns.RelativeColumn(2.2f);                 // Désignation
                columns.ConstantColumn(38);                   // Qté
                if (showDelivery) columns.ConstantColumn(38);  // Livré
                if (showUnit) columns.ConstantColumn(36);     // Unité
                columns.ConstantColumn(48);                   // PU HT
                columns.ConstantColumn(44);                   // TVA
                if (showDiscount) columns.ConstantColumn(40); // Remise
                columns.ConstantColumn(54);                   // Total HT
            });

            table.Header(header =>
            {
                void H(string t, bool right = false)
                {
                    var cell = header.Cell().Background(Theme.TableHeaderBg).Padding(4);
                    var txt = right ? cell.AlignRight() : cell;
                    txt.Text(t).FontColor(Theme.TableHeaderText).Bold().FontSize(8);
                }

                if (showLineNumber) H("N°");
                if (showRef) H("RÉF.");
                H("DÉSIGNATION");
                H(showDelivery ? "CMD" : "QTÉ", right: true);
                if (showDelivery) H("LIVRÉ", right: true);
                if (showUnit) H("UNITÉ");
                H("P.U. HT", right: true);
                H("TVA", right: true);
                if (showDiscount) H("REMISE", right: true);
                H("TOTAL HT", right: true);
            });

            foreach (var line in model.Lines.OrderBy(l => l.LineNumber))
            {
                var bg = line.LineNumber % 2 == 0 ? Theme.RowAltBg : Colors.White;

                if (showLineNumber)
                    table.Cell().Background(bg).Padding(4).AlignMiddle().Text(line.LineNumber.ToString()).FontSize(8);
                if (showRef)
                    table.Cell().Background(bg).Padding(4).AlignMiddle().Text(string.IsNullOrWhiteSpace(line.Reference) ? "—" : line.Reference!).FontSize(7);

                table.Cell().Background(bg).Padding(4).Column(c =>
                {
                    c.Item().Text(line.Name).Bold().FontSize(8);
                    if (!string.IsNullOrWhiteSpace(line.Description))
                        c.Item().Text(PdfRenderHelpers.CleanTextForPdf(line.Description)).FontSize(7).FontColor(Colors.Grey.Darken1);
                });

                table.Cell().Background(bg).Padding(4).AlignRight().AlignMiddle()
                    .Text($"{(showDelivery ? (line.OrderedQuantity ?? line.Quantity) : line.Quantity):N2}").FontSize(8);
                if (showDelivery)
                    table.Cell().Background(bg).Padding(4).AlignRight().AlignMiddle()
                        .Text($"{(line.DeliveredQuantity ?? line.Quantity):N2}").FontSize(8);
                if (showUnit)
                    table.Cell().Background(bg).Padding(4).AlignMiddle().Text(line.Unit ?? "—").FontSize(7);

                table.Cell().Background(bg).Padding(4).AlignRight().AlignMiddle().Text($"{line.UnitPriceHt:N3}").FontSize(8);
                table.Cell().Background(bg).Padding(4).AlignRight().AlignMiddle().Text(line.VatLabel).FontSize(7);
                if (showDiscount)
                    table.Cell().Background(bg).Padding(4).AlignRight().AlignMiddle()
                        .Text(line.DiscountPercent is > 0 ? $"{line.DiscountPercent:N1}%" : "—").FontSize(7);
                table.Cell().Background(bg).Padding(4).AlignRight().AlignMiddle().Text($"{line.LineTotalHt:N3}").FontSize(8);
            }
        });
    }

    /// <summary>Tableau de ventilation de la TVA (Taxe | Base HT | Montant).</summary>
    protected void ComposeVatTable(IContainer container, DocumentRenderModel model)
    {
        container.Table(taxTable =>
        {
            taxTable.ColumnsDefinition(c =>
            {
                c.RelativeColumn(1.2f);
                c.RelativeColumn(1);
                c.RelativeColumn(1);
            });

            taxTable.Header(h =>
            {
                void Th(string t, bool right = false)
                {
                    var cell = h.Cell().Background(Theme.TableHeaderBg).Padding(4);
                    (right ? cell.AlignRight() : cell).Text(t).FontColor(Theme.TableHeaderText).Bold().FontSize(8);
                }
                Th("Taxe");
                Th("Base HT", right: true);
                Th("Montant", right: true);
            });

            foreach (var vr in model.VatBreakdown)
            {
                taxTable.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(vr.Label).FontSize(8);
                taxTable.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignRight().Text($"{vr.BaseHt:N3}").FontSize(8);
                taxTable.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignRight().Text($"{vr.Amount:N3}").FontSize(8);
            }
        });
    }

    /// <summary>Colonne des totaux (HT, remise, TVA par taux, timbre, retenue).</summary>
    protected void ComposeTotalsColumn(IContainer container, DocumentRenderModel model)
    {
        container.Column(sumCol =>
        {
            void Row(string label, string value)
            {
                sumCol.Item().PaddingTop(2).Row(r =>
                {
                    r.RelativeItem().Text(label).FontSize(9);
                    r.ConstantItem(110).AlignRight().Text(value).FontSize(9);
                });
            }

            Row(model.IsCreditNote ? "Total HT à rembourser :" : "Total HT :", MoneySigned(model.SubTotal, model.Currency));

            if (model.DiscountTotal is > 0)
                Row("Remise :", Money(model.DiscountTotal.Value, model.Currency));

            if (Math.Abs(model.Fodec) > 0.0005m)
                Row("FODEC :", MoneySigned(model.Fodec, model.Currency));

            foreach (var vr in model.VatBreakdown.Where(v => v.Amount != 0))
            {
                var sign = model.IsCreditNote ? -1m : 1m;
                Row($"{vr.Label} :", MoneySigned(sign * vr.Amount, model.Currency));
            }

            if (Math.Abs(model.FiscalStamp) > 0.0005m)
                Row("Timbre fiscal :", MoneySigned(model.FiscalStamp, model.Currency));

            if (model.WithholdingAmount is { } wh && Math.Abs(wh) > 0.0005m)
                Row("Retenue à la source :", MoneySigned(-wh, model.Currency));
        });
    }

    /// <summary>Bandeau de total TTC (grand total mis en évidence).</summary>
    protected void ComposeGrandTotalBand(IContainer container, DocumentRenderModel model)
    {
        var bg = model.IsCreditNote ? Colors.Red.Darken2 : Theme.TotalBandBg;
        var label = model.WithholdingAmount is > 0
            ? "NET À PAYER :"
            : model.IsCreditNote ? "TOTAL TTC À REMBOURSER :" : "TOTAL TTC :";
        var amount = model.NetAfterWithholding ?? model.Total;

        container.Background(bg).Padding(10).Row(r =>
        {
            r.RelativeItem().Text(label).Bold().FontColor(Theme.TotalBandText).FontSize(11);
            r.RelativeItem().AlignRight().Text(MoneySigned(amount, model.Currency)).Bold().FontColor(Theme.TotalBandText).FontSize(11);
        });
    }

    /// <summary>Montant en toutes lettres.</summary>
    protected void ComposeAmountInWords(IContainer container, DocumentRenderModel model)
    {
        container.Text(text =>
        {
            text.DefaultTextStyle(x => x.FontSize(9).Italic());
            text.Span(model.IsCreditNote ? "À REMBOURSER LA SOMME DE " : "ARRÊTÉE À LA SOMME DE ");
            text.Span(model.AmountInWords).Bold();
            text.Span(".");
        });
    }

    /// <summary>Tableau des règlements (Réf. Pièce | Date | Mode | Montant).</summary>
    protected void ComposePaymentsTable(IContainer container, DocumentRenderModel model)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(1.2f);
                c.RelativeColumn(1);
                c.RelativeColumn(1);
                c.RelativeColumn(1);
            });

            table.Header(h =>
            {
                void Th(string t, bool right = false)
                {
                    var cell = h.Cell().Background(Theme.TableHeaderBg).Padding(4);
                    (right ? cell.AlignRight() : cell).Text(t).FontColor(Theme.TableHeaderText).Bold().FontSize(8);
                }
                Th("Réf. Pièce");
                Th("Date");
                Th("Mode");
                Th("Montant", right: true);
            });

            foreach (var p in model.Payments)
            {
                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(p.Reference).FontSize(8);
                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(p.Date).FontSize(8);
                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(p.Mode).FontSize(8);
                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignRight().Text($"{p.Amount:N3}").FontSize(8);
            }
        });
    }

    /// <summary>Notes / conditions / mentions légales.</summary>
    protected void ComposeNotes(ColumnDescriptor column, DocumentRenderModel model)
    {
        if (!string.IsNullOrWhiteSpace(model.Notes))
        {
            column.Item().PaddingTop(8).Column(c =>
            {
                c.Item().Text("Notes :").Bold().FontSize(9);
                c.Item().Text(PdfRenderHelpers.CleanTextForPdf(model.Notes)).FontSize(9);
            });
        }

        if (!string.IsNullOrWhiteSpace(model.Terms))
        {
            column.Item().PaddingTop(6).Column(c =>
            {
                c.Item().Text("Conditions :").Bold().FontSize(9);
                c.Item().Text(PdfRenderHelpers.CleanTextForPdf(model.Terms)).FontSize(9);
            });
        }

        foreach (var mention in model.LegalMentions)
            column.Item().PaddingTop(4).Text(PdfRenderHelpers.CleanTextForPdf(mention)).FontSize(8).FontColor(Theme.MutedText);

        if (!string.IsNullOrWhiteSpace(model.ClosingNote))
            column.Item().PaddingTop(10).Text(model.ClosingNote!).FontSize(9).FontColor(Colors.Grey.Darken1);
    }

    /// <summary>Pied de page avec numéro de page.</summary>
    protected void ComposePageNumberFooter(IContainer container, DocumentRenderModel model)
    {
        container.Column(column =>
        {
            column.Item().BorderTop(1).BorderColor(Theme.Accent).PaddingTop(6);

            if (!string.IsNullOrWhiteSpace(model.SignatureHash))
                column.Item().Text($"Signature électronique : {model.SignatureHash![..Math.Min(32, model.SignatureHash.Length)]}...")
                    .FontSize(7).FontColor(Colors.Grey.Darken1);

            column.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Column(fc =>
                {
                    if (!string.IsNullOrWhiteSpace(model.Issuer.Name))
                        fc.Item().Text(model.Issuer.Name).FontSize(8).Bold();
                });
                row.ConstantItem(120).AlignRight().AlignMiddle().Text(x =>
                {
                    x.DefaultTextStyle(BaseTextStyle.FontSize(8));
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        });
    }
}
