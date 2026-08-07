using FactuTrust.Application.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FactuTrust.Infrastructure.Services;

public partial class PdfService
{
    public Task<byte[]> GenerateCivpAttestationPdfAsync(
        CivpAttestationDto attestation,
        CancellationToken cancellationToken = default)
    {
        var generatedAt = attestation.GeneratedAt.ToString("dd/MM/yyyy", FrFr);
        var start = attestation.CivpStartDate.ToString("dd/MM/yyyy", FrFr);
        var end = attestation.CivpEndDate.ToString("dd/MM/yyyy", FrFr);

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
                            col.Item().Text(attestation.EmployerCompanyName).FontSize(13).Bold();
                            if (!string.IsNullOrWhiteSpace(attestation.EmployerNif))
                                col.Item().Text($"NIF : {attestation.EmployerNif}").FontSize(9);
                            if (!string.IsNullOrWhiteSpace(attestation.EmployerAddressLine))
                                col.Item().Text(attestation.EmployerAddressLine).FontSize(9).FontColor(Colors.Grey.Darken2);
                        });
                        row.ConstantItem(220).AlignRight().Column(col =>
                        {
                            col.Item().Text("ATTESTATION CIVP / SIVP").FontSize(12).Bold();
                            col.Item().Text(attestation.DocumentReference).FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                    });
                    header.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingTop(8).Column(col =>
                {
                    col.Item().PaddingBottom(8).Text("Stagiaire").FontSize(10).Bold();
                    col.Item().PaddingBottom(10).Column(emp =>
                    {
                        WriteIdentityLine(emp, "Matricule", attestation.EmployeeNumber);
                        WriteIdentityLine(emp, "Nom & Prénom", attestation.EmployeeName);
                        WriteIdentityLine(emp, "N° CIN", attestation.Cin);
                        WriteIdentityLine(emp, "Poste", attestation.JobTitle);
                    });

                    col.Item().PaddingVertical(8).Text(
                        $"Nous certifions que M./Mme {attestation.EmployeeName} est engagé(e) dans le cadre "
                        + $"d'un contrat SIVP/CIVP pour la période du {start} au {end}.")
                        .FontSize(10).LineHeight(1.4f);

                    col.Item().PaddingTop(8).Column(details =>
                    {
                        WriteIdentityLine(details, "Référence ANETI", attestation.AnetiReference);
                        WriteIdentityLine(details, "Subvention État (mensuelle)", FormatTnd(attestation.CivpStateGrant));
                        WriteIdentityLine(details, "Indemnité employeur (mensuelle)", FormatTnd(attestation.CivpEmployerAllowance));
                    });

                    col.Item().PaddingTop(24).Text("Fait pour servir et valoir ce que de droit.")
                        .FontSize(10).Italic();

                    col.Item().PaddingTop(28).AlignRight().Text($"Fait le {generatedAt}").FontSize(9);
                });

                page.Footer().AlignCenter().Text($"Document généré le {generatedAt} — InstaFact")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        }).GeneratePdf();

        return Task.FromResult(bytes);
    }

    private static string FormatTnd(decimal amount) => $"{amount:N3} TND";
}
