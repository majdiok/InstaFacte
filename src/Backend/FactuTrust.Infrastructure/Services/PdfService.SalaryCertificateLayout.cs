using FactuTrust.Application.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FactuTrust.Infrastructure.Services;

public partial class PdfService
{
    public Task<byte[]> GenerateSalaryCertificatePdfAsync(
        SalaryCertificateDto certificate,
        CancellationToken cancellationToken = default)
    {
        var generatedAt = certificate.GeneratedAt.ToString("dd/MM/yyyy", FrFr);
        var periodStart = certificate.PeriodStart.ToString("dd/MM/yyyy", FrFr);
        var periodEnd = certificate.PeriodEnd.ToString("dd/MM/yyyy", FrFr);

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
                            col.Item().Text(certificate.EmployerCompanyName).FontSize(13).Bold();
                            if (!string.IsNullOrWhiteSpace(certificate.EmployerNif))
                                col.Item().Text($"NIF : {certificate.EmployerNif}").FontSize(9);
                            if (!string.IsNullOrWhiteSpace(certificate.EmployerAddressLine))
                                col.Item().Text(certificate.EmployerAddressLine).FontSize(9).FontColor(Colors.Grey.Darken2);
                        });
                        row.ConstantItem(220).AlignRight().Column(col =>
                        {
                            col.Item().Text("ATTESTATION DE SALAIRE").FontSize(12).Bold();
                            col.Item().Text($"{certificate.PeriodMonths} derniers mois").FontSize(10);
                            col.Item().Text(certificate.DocumentReference).FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                    });
                    header.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingTop(6).Column(col =>
                {
                    col.Item().Text("Salarié").FontSize(10).Bold();
                    col.Item().PaddingBottom(8).Column(emp =>
                    {
                        WriteIdentityLine(emp, "Matricule", certificate.EmployeeNumber);
                        WriteIdentityLine(emp, "Nom & Prénom", certificate.EmployeeName);
                        WriteIdentityLine(emp, "N° CIN", certificate.Cin);
                        WriteIdentityLine(emp, "N° CNSS", certificate.CnssNumber);
                        WriteIdentityLine(emp, "Poste", certificate.JobTitle);
                    });

                    col.Item().PaddingBottom(8).Text(
                        $"Nous attestons que les rémunérations ci-dessous ont été versées à M./Mme {certificate.EmployeeName} "
                        + $"sur la période du {periodStart} au {periodEnd} "
                        + $"({certificate.PayslipCount} bulletin(s) figé(s) sur {certificate.PeriodMonths} mois demandés).")
                        .FontSize(10).LineHeight(1.35f);

                    col.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2f);
                            c.RelativeColumn(1f);
                            c.RelativeColumn(1f);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Mois").Bold().FontSize(9);
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Brut").Bold().FontSize(9);
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Net").Bold().FontSize(9);
                        });

                        foreach (var month in certificate.Months)
                        {
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(month.MonthLabel).FontSize(9);
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignRight()
                                .Text(FormatAmount(month.GrossSalary)).FontSize(9);
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignRight()
                                .Text(FormatAmount(month.NetSalary)).FontSize(9);
                        }

                        table.Cell().Background(Colors.Grey.Lighten4).Border(0.5f).Padding(4).Text("Totaux").Bold().FontSize(9);
                        table.Cell().Background(Colors.Grey.Lighten4).Border(0.5f).Padding(4).AlignRight()
                            .Text(FormatAmount(certificate.TotalGrossSalary)).Bold().FontSize(9);
                        table.Cell().Background(Colors.Grey.Lighten4).Border(0.5f).Padding(4).AlignRight()
                            .Text(FormatAmount(certificate.TotalNetSalary)).Bold().FontSize(9);
                    });

                    col.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2f);
                            c.RelativeColumn(1f);
                        });
                        WriteAmountRow(table, "Salaire brut moyen mensuel", certificate.AverageGrossSalary, bold: true);
                        WriteAmountRow(table, "Salaire net moyen mensuel", certificate.AverageNetSalary, bold: true);
                    });

                    if (certificate.Warnings.Count > 0)
                    {
                        col.Item().PaddingTop(8).Text($"Avertissements : {string.Join(" — ", certificate.Warnings)}")
                            .FontSize(8).FontColor(Colors.Orange.Darken2);
                    }

                    col.Item().PaddingTop(20).Text("Fait pour servir et valoir ce que de droit.")
                        .FontSize(10).Italic();
                });

                page.Footer().AlignCenter().Text($"Généré le {generatedAt} — Document produit par InstaFact.")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        }).GeneratePdf();

        return Task.FromResult(bytes);
    }
}
