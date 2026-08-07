using FactuTrust.Application.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FactuTrust.Infrastructure.Services;

public partial class PdfService
{
    public Task<byte[]> GenerateEmploymentCertificatePdfAsync(
        EmploymentCertificateDto certificate,
        CancellationToken cancellationToken = default)
    {
        var generatedAt = certificate.GeneratedAt.ToString("dd/MM/yyyy", FrFr);
        var hireDate = certificate.HireDate.ToString("dd/MM/yyyy", FrFr);
        var endDate = certificate.TerminationDate?.ToString("dd/MM/yyyy", FrFr);
        var contractStart = certificate.ContractStartDate.ToString("dd/MM/yyyy", FrFr);
        var contractEnd = certificate.ContractEndDate?.ToString("dd/MM/yyyy", FrFr);

        var bodyText = certificate.IsStillEmployed
            ? BuildStillEmployedBody(certificate, hireDate, contractStart)
            : BuildFormerEmployeeBody(certificate, hireDate, endDate!, contractStart, contractEnd);

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
                        row.ConstantItem(200).AlignRight().Column(col =>
                        {
                            col.Item().Text("CERTIFICAT DE TRAVAIL").FontSize(13).Bold();
                            col.Item().Text(certificate.DocumentReference).FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                    });
                    header.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingTop(8).Column(col =>
                {
                    col.Item().PaddingBottom(8).Text("Salarié").FontSize(10).Bold();
                    col.Item().PaddingBottom(10).Column(emp =>
                    {
                        WriteIdentityLine(emp, "Matricule", certificate.EmployeeNumber);
                        WriteIdentityLine(emp, "Nom & Prénom", certificate.EmployeeName);
                        WriteIdentityLine(emp, "N° CIN", certificate.Cin);
                        WriteIdentityLine(emp, "N° CNSS", certificate.CnssNumber);
                        WriteIdentityLine(emp, "Adresse", certificate.AddressLine);
                    });

                    col.Item().PaddingVertical(8).Text(bodyText).FontSize(10).LineHeight(1.4f);

                    col.Item().PaddingTop(8).Column(details =>
                    {
                        WriteIdentityLine(details, "Date d'embauche", hireDate);
                        if (!certificate.IsStillEmployed && endDate is not null)
                            WriteIdentityLine(details, "Date de sortie", endDate);
                        WriteIdentityLine(details, "Poste", certificate.JobTitle);
                        WriteIdentityLine(details, "Type de contrat", certificate.ContractTypeDisplay);
                        WriteIdentityLine(details, "Période couverte", FormatPeriod(certificate.ContractStartDate, certificate.ContractEndDate, certificate.IsStillEmployed));
                    });

                    if (certificate.Warnings.Count > 0)
                    {
                        col.Item().PaddingTop(10).Text($"Avertissements : {string.Join(" — ", certificate.Warnings)}")
                            .FontSize(8).FontColor(Colors.Orange.Darken2);
                    }

                    col.Item().PaddingTop(24).Text("Fait pour servir et valoir ce que de droit.")
                        .FontSize(10).Italic();

                    col.Item().PaddingTop(28).Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Text("Cachet et signature de l'employeur").FontSize(9).FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(180).AlignRight().Column(right =>
                        {
                            right.Item().Text($"Fait le {generatedAt}").FontSize(9);
                        });
                    });
                });

                page.Footer().AlignCenter().Text($"Document généré le {generatedAt} — InstaFact")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        }).GeneratePdf();

        return Task.FromResult(bytes);
    }

    private static string BuildStillEmployedBody(EmploymentCertificateDto certificate, string hireDate, string contractStart)
    {
        var job = string.IsNullOrWhiteSpace(certificate.JobTitle) ? "salarié(e)" : certificate.JobTitle;
        return $"Nous soussignés, {certificate.EmployerCompanyName}, certifions que M./Mme {certificate.EmployeeName} "
               + $"est employé(e) en nos services depuis le {hireDate} (contrat du {contractStart}) "
               + $"en qualité de {job}.";
    }

    private static string BuildFormerEmployeeBody(
        EmploymentCertificateDto certificate,
        string hireDate,
        string endDate,
        string contractStart,
        string? contractEnd)
    {
        var job = string.IsNullOrWhiteSpace(certificate.JobTitle) ? "salarié(e)" : certificate.JobTitle;
        var periodEnd = string.IsNullOrWhiteSpace(contractEnd) ? endDate : contractEnd;
        return $"Nous soussignés, {certificate.EmployerCompanyName}, certifions que M./Mme {certificate.EmployeeName} "
               + $"a été employé(e) en nos services du {contractStart} au {periodEnd} "
               + $"(sortie le {endDate}) en qualité de {job}.";
    }

    private static string FormatPeriod(DateTime start, DateTime? end, bool isStillEmployed)
    {
        var startText = start.ToString("dd/MM/yyyy", FrFr);
        if (isStillEmployed)
            return $"du {startText} à ce jour";
        return end.HasValue
            ? $"du {startText} au {end.Value:dd/MM/yyyy}"
            : $"à partir du {startText}";
    }
}
