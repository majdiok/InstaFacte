using System.Globalization;
using FactuTrust.Application.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FactuTrust.Infrastructure.Services;

public partial class PdfService
{
    private static readonly string[] PayslipMonthNamesFr =
    {
        "", "Janvier", "Février", "Mars", "Avril", "Mai", "Juin",
        "Juillet", "Août", "Septembre", "Octobre", "Novembre", "Décembre"
    };

    public Task<byte[]> GeneratePayslipPdfAsync(PayslipDetailDto payslip, string companyName, CancellationToken cancellationToken = default)
    {
        var period = payslip.Month >= 1 && payslip.Month <= 12
            ? $"{PayslipMonthNamesFr[payslip.Month]} {payslip.Year}"
            : $"{payslip.Month}/{payslip.Year}";

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
                            col.Item().Text(string.IsNullOrWhiteSpace(companyName) ? "Employeur" : companyName).FontSize(14).Bold();
                            col.Item().Text($"Bulletin de paie — {period}").FontSize(11).FontColor(Colors.Blue.Darken2);
                        });
                        row.ConstantItem(180).AlignRight().Column(col =>
                        {
                            col.Item().Text(payslip.EmployeeName).Bold().FontSize(11);
                            col.Item().Text($"Matricule : {payslip.EmployeeNumber}").FontSize(9);
                            if (!string.IsNullOrWhiteSpace(payslip.Cin))
                                col.Item().Text($"CIN : {payslip.Cin}").FontSize(9);
                            if (!string.IsNullOrWhiteSpace(payslip.CnssNumber))
                                col.Item().Text($"CNSS : {payslip.CnssNumber}").FontSize(9);
                            if (payslip.HireDate.HasValue)
                                col.Item().Text($"Date d'embauche : {payslip.HireDate.Value:dd/MM/yyyy}").FontSize(9);
                            if (payslip.LeaveBalanceRemaining.HasValue)
                                col.Item().Text($"Solde congés : {payslip.LeaveBalanceRemaining.Value:N1} j").FontSize(9);
                        });
                    });
                    header.Item().PaddingVertical(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingVertical(8).Column(col =>
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(28);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("#").Bold().FontSize(9);
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Libellé").Bold().FontSize(9);
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Base").Bold().FontSize(9);
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Taux %").Bold().FontSize(9);
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Montant").Bold().FontSize(9);
                        });

                        foreach (var line in payslip.Lines.OrderBy(l => l.Order))
                        {
                            table.Cell().Padding(3).Text(line.Order.ToString()).FontSize(9);
                            table.Cell().Padding(3).Text(line.Label).FontSize(9);
                            table.Cell().Padding(3).AlignRight().Text(line.Base.HasValue ? FormatAmount(line.Base.Value) : "—").FontSize(9);
                            table.Cell().Padding(3).AlignRight().Text(line.Rate.HasValue ? line.Rate.Value.ToString("0.####", CultureInfo.InvariantCulture) : "—").FontSize(9);
                            table.Cell().Padding(3).AlignRight().Text(FormatAmount(line.Amount)).FontSize(9);
                        }
                    });

                    col.Item().PaddingTop(12).Background(Colors.Grey.Lighten4).Padding(10).Row(row =>
                    {
                        row.RelativeItem().Text("Brut").Bold();
                        row.ConstantItem(120).AlignRight().Text(FormatAmount(payslip.GrossSalary)).Bold();
                    });
                    col.Item().PaddingTop(4).Background(Colors.Blue.Lighten5).Padding(10).Row(row =>
                    {
                        row.RelativeItem().Text("Net à payer").Bold().FontSize(12);
                        row.ConstantItem(120).AlignRight().Text(FormatAmount(payslip.NetSalary)).Bold().FontSize(12);
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Document généré le ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)).FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        }).GeneratePdf();

        return Task.FromResult(bytes);
    }

    private static string FormatAmount(decimal amount) =>
        amount.ToString("N3", CultureInfo.GetCultureInfo("fr-FR"));
}
