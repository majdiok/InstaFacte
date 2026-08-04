using FactuTrust.Domain.Entities.Honoraires;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FactuTrust.Infrastructure.Services.Honoraires;

public static class HonorairesPdfRenderer
{
    private static readonly Color Primary = Color.FromHex("#1d4ed8");

    public static byte[] RenderInvoice(HonorairesInvoice invoice)
    {
        var title = invoice.IsCreditNote ? "AVOIR HONORAIRES" : "FACTURE HONORAIRES";
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));
                page.Header().Column(col =>
                {
                    col.Item().Text(title).FontSize(18).Bold().FontColor(Primary);
                    col.Item().Text(invoice.Number ?? "N° Provisoire").FontSize(12);
                    col.Item().Text($"Date : {invoice.IssueDate:dd/MM/yyyy}");
                });
                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Item().Text($"Client : {invoice.ClientName}").Bold();
                    if (!string.IsNullOrWhiteSpace(invoice.ClientNif))
                        col.Item().Text($"MF : {invoice.ClientNif}");
                    if (!string.IsNullOrWhiteSpace(invoice.ClientAddress))
                        col.Item().Text(invoice.ClientAddress);
                    col.Item().PaddingTop(12).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(4);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1.5f);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1.5f);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Background(Primary).Padding(4).Text("Désignation").FontColor(Colors.White);
                            h.Cell().Background(Primary).Padding(4).Text("Qté").FontColor(Colors.White);
                            h.Cell().Background(Primary).Padding(4).Text("PU").FontColor(Colors.White);
                            h.Cell().Background(Primary).Padding(4).Text("TVA").FontColor(Colors.White);
                            h.Cell().Background(Primary).Padding(4).Text("Total HT").FontColor(Colors.White);
                        });
                        foreach (var line in invoice.Lines.OrderBy(l => l.LineNumber))
                        {
                            table.Cell().BorderBottom(0.5f).Padding(4).Element(e => RenderLineDesignation(e, line.ActivityCode, line.Designation, line.Description));
                            table.Cell().BorderBottom(0.5f).Padding(4).Text(line.Quantity.ToString("N3"));
                            table.Cell().BorderBottom(0.5f).Padding(4).Text(line.UnitPrice.Amount.ToString("N3"));
                            table.Cell().BorderBottom(0.5f).Padding(4).Text($"{(int)line.VatRate}%");
                            table.Cell().BorderBottom(0.5f).Padding(4).Text(line.SubTotal.Amount.ToString("N3"));
                        }
                    });
                    col.Item().AlignRight().PaddingTop(12).Column(tot =>
                    {
                        tot.Item().Text($"Total HT : {invoice.SubTotal.Amount:N3} {invoice.Currency}");
                        tot.Item().Text($"TVA : {invoice.TotalVat.Amount:N3} {invoice.Currency}");
                        if (invoice.WithholdingAmount.Amount != 0)
                            tot.Item().Text($"Retenue : {invoice.WithholdingAmount.Amount:N3} {invoice.Currency}");
                        tot.Item().Text($"Total TTC : {invoice.TotalAmount.Amount:N3} {invoice.Currency}").Bold().FontColor(Primary);
                    });
                });
            });
        }).GeneratePdf();
    }

    public static byte[] RenderQuote(HonorairesQuote quote)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));
                page.Header().Column(col =>
                {
                    col.Item().Text("DEVIS HONORAIRES").FontSize(18).Bold().FontColor(Primary);
                    col.Item().Text(quote.Number ?? "N° Provisoire").FontSize(12);
                    col.Item().Text($"Date : {quote.IssueDate:dd/MM/yyyy}");
                });
                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Item().Text($"Client : {quote.ClientName}").Bold();
                    col.Item().PaddingTop(12).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(4);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1.5f);
                            c.RelativeColumn(1.5f);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Background(Primary).Padding(4).Text("Désignation").FontColor(Colors.White);
                            h.Cell().Background(Primary).Padding(4).Text("Qté").FontColor(Colors.White);
                            h.Cell().Background(Primary).Padding(4).Text("PU").FontColor(Colors.White);
                            h.Cell().Background(Primary).Padding(4).Text("Total HT").FontColor(Colors.White);
                        });
                        foreach (var line in quote.Lines.OrderBy(l => l.LineNumber))
                        {
                            table.Cell().BorderBottom(0.5f).Padding(4).Element(e => RenderLineDesignation(e, line.ActivityCode, line.Designation, line.Description));
                            table.Cell().BorderBottom(0.5f).Padding(4).Text(line.Quantity.ToString("N3"));
                            table.Cell().BorderBottom(0.5f).Padding(4).Text(line.UnitPrice.Amount.ToString("N3"));
                            table.Cell().BorderBottom(0.5f).Padding(4).Text(line.SubTotal.Amount.ToString("N3"));
                        }
                    });
                    col.Item().AlignRight().PaddingTop(12).Text($"Total TTC : {quote.TotalAmount.Amount:N3} {quote.Currency}").Bold();
                });
            });
        }).GeneratePdf();
    }

    private static void RenderLineDesignation(IContainer container, string? activityCode, string designation, string? description)
    {
        container.Column(col =>
        {
            if (!string.IsNullOrWhiteSpace(activityCode))
                col.Item().Text(activityCode).FontSize(8).FontColor(Colors.Grey.Darken1);
            col.Item().Text(designation);
            if (!string.IsNullOrWhiteSpace(description))
                col.Item().Text(description).FontSize(9).FontColor(Colors.Grey.Darken2);
        });
    }
}
