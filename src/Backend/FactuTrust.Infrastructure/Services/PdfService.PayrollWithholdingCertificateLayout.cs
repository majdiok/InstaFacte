using System.Globalization;
using FactuTrust.Application.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FactuTrust.Infrastructure.Services;

public partial class PdfService
{
    public Task<byte[]> GeneratePayrollWithholdingCertificatePdfAsync(
        PayrollWithholdingCertificateLineDto line,
        PayrollWithholdingCertificateBatchDto batch,
        CancellationToken cancellationToken = default)
    {
        var generatedAt = DateTime.Now.ToString("dd/MM/yyyy", FrFr);

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
                            col.Item().Text(batch.EmployerCompanyName).FontSize(13).Bold();
                            col.Item().Text($"NIF : {batch.EmployerNif}").FontSize(9);
                            if (!string.IsNullOrWhiteSpace(batch.EmployerAddressLine))
                                col.Item().Text(batch.EmployerAddressLine).FontSize(9).FontColor(Colors.Grey.Darken2);
                        });
                        row.ConstantItem(220).AlignRight().Column(col =>
                        {
                            col.Item().Text("CERTIFICAT DE RETENUE À LA SOURCE").FontSize(12).Bold();
                            col.Item().Text($"Exercice {batch.Year}").FontSize(11);
                            col.Item().Text(line.DocumentReference).FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                    });
                    header.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingTop(6).Column(col =>
                {
                    col.Item().Text("Bénéficiaire").FontSize(10).Bold();
                    col.Item().PaddingBottom(6).Column(emp =>
                    {
                        WriteIdentityLine(emp, "Matricule", line.EmployeeNumber);
                        WriteIdentityLine(emp, "Nom & Prénom", line.EmployeeName);
                        WriteIdentityLine(emp, "N° CIN", line.Cin);
                        WriteIdentityLine(emp, "N° CNSS", line.CnssNumber);
                        WriteIdentityLine(emp, "Adresse", line.AddressLine);
                        WriteIdentityLine(emp, "Chef de famille", line.IsHeadOfFamily ? "Oui" : "Non");
                        WriteIdentityLine(emp, "Mois déclarés", line.MonthsCount.ToString(CultureInfo.InvariantCulture));
                    });

                    col.Item().PaddingTop(8).Text("Récapitulatif annuel").FontSize(10).Bold();
                    col.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2.5f);
                            c.RelativeColumn(1f);
                        });

                        WriteAmountRow(table, "Rémunération brute", line.TotalGross);
                        WriteAmountRow(table, "Brut CNSSable", line.TotalCnssableGross);
                        WriteAmountRow(table, "Cotisations CNSS salariales", line.TotalCnssEmployee);
                        WriteAmountRow(table, "Frais professionnels", line.TotalProfessionalExpenses);
                        WriteAmountRow(table, "Déductions pour charges de famille", line.TotalFamilyDeductions);
                        WriteAmountRow(table, "Revenu net imposable annuel", line.AnnualNetTaxable);
                        WriteAmountRow(table, "IRPP retenu", line.TotalIrppWithheld);
                        WriteAmountRow(table, "CSS retenue", line.TotalCssWithheld);
                        if (line.TotalIrppSmigExemption > 0)
                            WriteAmountRow(table, "Exonération IRPP SMIG (informatif)", line.TotalIrppSmigExemption);
                        WriteAmountRow(table, "Total des retenues", line.TotalWithholding, bold: true);
                    });

                    col.Item().PaddingTop(10).Text("Détail mensuel").FontSize(10).Bold();
                    col.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1.2f);
                            c.RelativeColumn(1.2f);
                            c.RelativeColumn(1f);
                            c.RelativeColumn(1f);
                            c.RelativeColumn(1f);
                            c.RelativeColumn(1f);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Mois").Bold().FontSize(8);
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(3).AlignRight().Text("Net imposable").Bold().FontSize(8);
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(3).AlignRight().Text("IRPP").Bold().FontSize(8);
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(3).AlignRight().Text("Rég. IRPP").Bold().FontSize(8);
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(3).AlignRight().Text("CSS").Bold().FontSize(8);
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(3).AlignRight().Text("Rég. CSS").Bold().FontSize(8);
                        });

                        foreach (var month in line.Months.Where(m => m.HasPayslip))
                        {
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(month.MonthLabel).FontSize(8);
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(FormatAmount(month.MonthlyNetTaxable)).FontSize(8);
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(FormatAmount(month.Irpp)).FontSize(8);
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(FormatAmount(month.IrppRegularization)).FontSize(8);
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(FormatAmount(month.Css)).FontSize(8);
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(FormatAmount(month.CssRegularization)).FontSize(8);
                        }
                    });

                    if (line.Warnings.Count > 0)
                    {
                        col.Item().PaddingTop(8).Text($"Avertissements : {string.Join(" — ", line.Warnings)}")
                            .FontSize(8).FontColor(Colors.Orange.Darken2);
                    }
                });

                page.Footer().AlignCenter().Column(footer =>
                {
                    footer.Item().Text($"Généré le {generatedAt} — Document produit par InstaFact.")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                    footer.Item().Text("À conserver pour la déclaration personnelle du salarié.")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        }).GeneratePdf();

        return Task.FromResult(bytes);
    }

    private static void WriteAmountRow(TableDescriptor table, string label, decimal amount, bool bold = false)
    {
        var labelCell = table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(label).FontSize(9);
        if (bold) labelCell.Bold();

        var amountCell = table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4)
            .AlignRight().Text(FormatAmount(amount)).FontSize(9);
        if (bold) amountCell.Bold();
    }
}
