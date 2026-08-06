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

    private static readonly CultureInfo FrFr = CultureInfo.GetCultureInfo("fr-FR");

    public Task<byte[]> GeneratePayslipPdfAsync(PayslipDetailDto payslip, string companyName, CancellationToken cancellationToken = default)
    {
        var period = payslip.Month >= 1 && payslip.Month <= 12
            ? $"{PayslipMonthNamesFr[payslip.Month]} {payslip.Year}"
            : $"{payslip.Month}/{payslip.Year}";

        var earnings = payslip.Lines
            .Where(l => string.Equals(l.Kind, "Earning", StringComparison.OrdinalIgnoreCase))
            .OrderBy(l => l.Order)
            .ToList();
        var socialDeductions = payslip.Lines
            .Where(l => string.Equals(l.Kind, "Deduction", StringComparison.OrdinalIgnoreCase)
                        && l.Label.Contains("CNSS", StringComparison.OrdinalIgnoreCase))
            .OrderBy(l => l.Order)
            .ToList();
        var fiscalDeductions = payslip.Lines
            .Where(l => string.Equals(l.Kind, "Deduction", StringComparison.OrdinalIgnoreCase)
                        && !l.Label.Contains("CNSS", StringComparison.OrdinalIgnoreCase))
            .OrderBy(l => l.Order)
            .ToList();

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
                            col.Item().Text(string.IsNullOrWhiteSpace(companyName) ? "Employeur" : companyName)
                                .FontSize(13).Bold();
                            if (!string.IsNullOrWhiteSpace(payslip.CompanyAddress))
                                col.Item().Text(payslip.CompanyAddress).FontSize(9).FontColor(Colors.Grey.Darken2);
                        });
                        row.ConstantItem(200).AlignRight().Column(col =>
                        {
                            col.Item().Text("FICHE DE PAIE").FontSize(16).Bold();
                            col.Item().Text(period).FontSize(11);
                        });
                    });
                    header.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);

                    header.Item().PaddingBottom(6).Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            WriteIdentityLine(left, "Matricule", payslip.EmployeeNumber);
                            WriteIdentityLine(left, "Nom & Prénom", payslip.EmployeeName);
                            WriteIdentityLine(left, "Fonction", payslip.JobTitle);
                            WriteIdentityLine(left, "Catégorie", payslip.Category);
                            WriteIdentityLine(left, "Echelon", payslip.Echelon);
                        });
                        row.RelativeItem().Column(right =>
                        {
                            WriteIdentityLine(right, "N° CNSS", payslip.CnssNumber);
                            WriteIdentityLine(right, "N° CIN", payslip.Cin);
                            WriteIdentityLine(right, "Chef de famille", payslip.IsHeadOfFamily ? "Oui" : "Non");
                        });
                    });
                });

                page.Content().PaddingTop(4).Column(col =>
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3.2f);
                            columns.RelativeColumn(1.3f);
                            columns.RelativeColumn(1.0f);
                            columns.RelativeColumn(1.3f);
                            columns.RelativeColumn(1.3f);
                        });

                        table.Header(th =>
                        {
                            th.Cell().Background(Colors.Grey.Lighten3).Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(4)
                                .Text("Rubriques").Bold().FontSize(9);
                            th.Cell().Background(Colors.Grey.Lighten3).Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(4)
                                .AlignRight().Text("Base / Nbr").Bold().FontSize(9);
                            th.Cell().Background(Colors.Grey.Lighten3).Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(4)
                                .AlignRight().Text("Taux").Bold().FontSize(9);
                            th.Cell().Background(Colors.Grey.Lighten3).Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(4)
                                .AlignRight().Text("Gains").Bold().FontSize(9);
                            th.Cell().Background(Colors.Grey.Lighten3).Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(4)
                                .AlignRight().Text("Retenues").Bold().FontSize(9);
                        });

                        foreach (var line in earnings)
                        {
                            var baseText = string.Equals(line.Label, "Salaire de base", StringComparison.OrdinalIgnoreCase)
                                           && payslip.WorkedDays.HasValue
                                ? FormatDays(payslip.WorkedDays.Value)
                                : FormatBase(line.Base);
                            WriteDataRow(table, line.Label, baseText, FormatRate(line.Rate), FormatAmount(line.Amount), string.Empty);
                        }

                        WriteSubtotalRow(table, "Salaire brut", FormatAmount(payslip.GrossSalary), string.Empty);

                        foreach (var line in socialDeductions)
                        {
                            WriteDataRow(
                                table,
                                line.Label,
                                FormatBase(line.Base ?? payslip.CnssableGross),
                                FormatRate(line.Rate),
                                string.Empty,
                                FormatAmount(line.Amount));
                        }

                        WriteSubtotalRow(table, "Salaire brut imposable", FormatAmount(payslip.MonthlyNetTaxable), string.Empty);

                        foreach (var line in fiscalDeductions)
                        {
                            WriteDataRow(
                                table,
                                line.Label,
                                FormatBase(line.Base),
                                FormatRate(line.Rate),
                                string.Empty,
                                FormatAmount(line.Amount));
                        }

                        table.Cell().ColumnSpan(4).BorderTop(1).Padding(6).AlignRight()
                            .Text("Net à payer").Bold().FontSize(10);
                        table.Cell().BorderTop(1).Padding(6).AlignRight()
                            .Text(FormatAmount(payslip.NetSalary)).Bold().FontSize(10);
                    });
                });

                page.Footer().AlignCenter().Column(col =>
                {
                    if (payslip.ProrataDeductionAmount > 0)
                    {
                        col.Item().Text(
                            $"Prorata : {payslip.ProrataWorkedDays:N2} j travaillés / 26 — {payslip.ProrataNonWorkedDays:N2} j non rémunérés — retenue {FormatAmount(payslip.ProrataDeductionAmount)}")
                            .FontSize(8).FontColor(Colors.Grey.Darken1);
                    }

                    col.Item().Text(t =>
                    {
                        t.Span("Document généré le ").FontSize(8).FontColor(Colors.Grey.Darken1);
                        t.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture))
                            .FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                });
            });
        }).GeneratePdf();

        return Task.FromResult(bytes);
    }

    private static void WriteIdentityLine(ColumnDescriptor col, string label, string? value)
    {
        col.Item().PaddingBottom(2).Text(t =>
        {
            t.Span($"{label} : ").FontSize(9);
            t.Span(string.IsNullOrWhiteSpace(value) ? string.Empty : value).FontSize(9).Bold();
        });
    }

    private static void WriteDataRow(
        TableDescriptor table,
        string label,
        string baseText,
        string rateText,
        string gainsText,
        string retenuesText)
    {
        table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(3).Text(label).FontSize(9);
        table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(3).AlignRight().Text(baseText).FontSize(9);
        table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(3).AlignRight().Text(rateText).FontSize(9);
        table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(3).AlignRight().Text(gainsText).FontSize(9);
        table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(3).AlignRight().Text(retenuesText).FontSize(9);
    }

    private static void WriteSubtotalRow(TableDescriptor table, string label, string gainsText, string retenuesText)
    {
        table.Cell().Background(Colors.Grey.Lighten4).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4)
            .Text(label).Bold().FontSize(9);
        table.Cell().Background(Colors.Grey.Lighten4).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4);
        table.Cell().Background(Colors.Grey.Lighten4).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4);
        table.Cell().Background(Colors.Grey.Lighten4).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4)
            .AlignRight().Text(gainsText).Bold().FontSize(9);
        table.Cell().Background(Colors.Grey.Lighten4).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4)
            .AlignRight().Text(retenuesText).Bold().FontSize(9);
    }

    private static string FormatAmount(decimal amount) =>
        amount.ToString("N3", FrFr);

    private static string FormatBase(decimal? baseAmount) =>
        baseAmount.HasValue ? FormatAmount(baseAmount.Value) : string.Empty;

    private static string FormatRate(decimal? rate) =>
        rate.HasValue ? $"{rate.Value.ToString("0.##", FrFr)} %" : string.Empty;

    private static string FormatDays(decimal days) =>
        $"{days.ToString("0.00", FrFr)} j";
}
