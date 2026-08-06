using System.Globalization;
using FactuTrust.Application.DTOs;
using FactuTrust.Infrastructure.Services.Templates;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FactuTrust.Infrastructure.Services;

public partial class PdfService
{
    private static readonly string[] MonthLabelsFr =
    {
        "", "Janvier", "Février", "Mars", "Avril", "Mai", "Juin",
        "Juillet", "Août", "Septembre", "Octobre", "Novembre", "Décembre"
    };

    public Task<byte[]> GenerateCnssContributionRemittancePdfAsync(
        CnssContributionRemittanceDto remittance,
        CancellationToken cancellationToken = default)
    {
        var generatedAt = DateTime.Now.ToString("dd/MM/yyyy", FrFr);
        var periodLabel = $"{MonthLabelsFr[remittance.Month]} {remittance.Year}";

        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(DefaultTextStyle);

                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text(remittance.EmployerCompanyName).FontSize(13).Bold();
                            col.Item().Text($"NIF : {remittance.EmployerNif}").FontSize(9);
                            if (!string.IsNullOrWhiteSpace(remittance.EmployerCnssNumber))
                                col.Item().Text($"Matricule CNSS : {remittance.EmployerCnssNumber}").FontSize(9);
                            if (!string.IsNullOrWhiteSpace(remittance.EmployerAddressLine))
                                col.Item().Text(remittance.EmployerAddressLine).FontSize(9).FontColor(Colors.Grey.Darken2);
                        });
                        row.ConstantItem(240).AlignRight().Column(col =>
                        {
                            col.Item().Text("BORDEREAU DE PAIEMENT DES COTISATIONS CNSS").FontSize(11).Bold();
                            col.Item().Text(periodLabel).FontSize(11);
                            col.Item().Text(remittance.DocumentReference).FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                    });
                    header.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingTop(6).Column(col =>
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeadCell).Text("CNSS");
                            header.Cell().Element(HeadCell).Text("Nom");
                            header.Cell().Element(HeadCell).AlignRight().Text("Brut CNSS");
                            header.Cell().Element(HeadCell).AlignRight().Text("CNSS sal.");
                            header.Cell().Element(HeadCell).AlignRight().Text("CNSS pat.");
                            header.Cell().Element(HeadCell).AlignRight().Text("AT");
                            header.Cell().Element(HeadCell).AlignRight().Text("Total");
                        });

                        foreach (var line in remittance.Lines)
                        {
                            table.Cell().Element(BodyCell).Text(PdfRenderHelpers.CleanTextForPdf(line.CnssNumber ?? "—"));
                            table.Cell().Element(BodyCell).Text(PdfRenderHelpers.CleanTextForPdf(line.EmployeeName));
                            table.Cell().Element(BodyCell).AlignRight().Text(FormatCnssAmount(line.CnssableGross));
                            table.Cell().Element(BodyCell).AlignRight().Text(FormatCnssAmount(line.CnssEmployee));
                            table.Cell().Element(BodyCell).AlignRight().Text(FormatCnssAmount(line.CnssEmployer));
                            table.Cell().Element(BodyCell).AlignRight().Text(FormatCnssAmount(line.WorkAccident));
                            table.Cell().Element(BodyCell).AlignRight().Text(FormatCnssAmount(line.LineTotal));
                        }

                        table.Footer(footer =>
                        {
                            footer.Cell().ColumnSpan(2).Element(TotalCell).Text("TOTAL").Bold();
                            footer.Cell().Element(TotalCell).AlignRight().Text(FormatCnssAmount(remittance.Lines.Sum(l => l.CnssableGross))).Bold();
                            footer.Cell().Element(TotalCell).AlignRight().Text(FormatCnssAmount(remittance.TotalCnssEmployee)).Bold();
                            footer.Cell().Element(TotalCell).AlignRight().Text(FormatCnssAmount(remittance.TotalCnssEmployer)).Bold();
                            footer.Cell().Element(TotalCell).AlignRight().Text(FormatCnssAmount(remittance.TotalWorkAccident)).Bold();
                            footer.Cell().Element(TotalCell).AlignRight().Text(FormatCnssAmount(remittance.TotalDue)).Bold();
                        });
                    });

                    col.Item().PaddingTop(16).Column(summary =>
                    {
                        summary.Item().Text("Synthèse").FontSize(10).Bold();
                        WriteSummaryRow(summary, "CNSS salariale", remittance.TotalCnssEmployee);
                        WriteSummaryRow(summary, "CNSS patronale", remittance.TotalCnssEmployer);
                        WriteSummaryRow(summary, "Accident du travail", remittance.TotalWorkAccident);
                        summary.Item().PaddingTop(4).Row(row =>
                        {
                            row.RelativeItem().Text("TOTAL À VERSER CNSS").Bold().FontSize(11);
                            row.ConstantItem(120).AlignRight().Text(FormatCnssAmount(remittance.TotalDue)).Bold().FontSize(11);
                        });
                    });

                    if (remittance.Payment is not null)
                    {
                        col.Item().PaddingTop(12).Text(
                            $"Versement enregistré le {remittance.Payment.PaymentDate:dd/MM/yyyy}" +
                            (string.IsNullOrWhiteSpace(remittance.Payment.Reference)
                                ? string.Empty
                                : $" — Réf. {remittance.Payment.Reference}"))
                            .FontSize(9)
                            .FontColor(Colors.Green.Darken2);
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span($"Généré le {generatedAt} — ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    text.Span("Document de travail — non substitut au bordereau officiel CNSS").FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
                });
            });
        }).GeneratePdf();

        return Task.FromResult(bytes);
    }

    private static void WriteSummaryRow(ColumnDescriptor col, string label, decimal amount)
    {
        col.Item().Row(row =>
        {
            row.RelativeItem().Text(label).FontSize(9);
            row.ConstantItem(120).AlignRight().Text(FormatCnssAmount(amount)).FontSize(9);
        });
    }

    private static string FormatCnssAmount(decimal value) =>
        value.ToString("N3", FrFr);
}
