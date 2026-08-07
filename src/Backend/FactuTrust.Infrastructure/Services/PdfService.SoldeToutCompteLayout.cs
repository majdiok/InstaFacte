using FactuTrust.Application.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FactuTrust.Infrastructure.Services;

public partial class PdfService
{
    public Task<byte[]> GenerateSoldeToutComptePdfAsync(
        SoldeToutCompteDto settlement,
        CancellationToken cancellationToken = default)
    {
        var generatedAt = settlement.GeneratedAt.ToString("dd/MM/yyyy", FrFr);
        var terminationDate = settlement.TerminationDate.ToString("dd/MM/yyyy", FrFr);

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
                            col.Item().Text(settlement.EmployerCompanyName).FontSize(13).Bold();
                            if (!string.IsNullOrWhiteSpace(settlement.EmployerNif))
                                col.Item().Text($"NIF : {settlement.EmployerNif}").FontSize(9);
                            if (!string.IsNullOrWhiteSpace(settlement.EmployerAddressLine))
                                col.Item().Text(settlement.EmployerAddressLine).FontSize(9).FontColor(Colors.Grey.Darken2);
                        });
                        row.ConstantItem(220).AlignRight().Column(col =>
                        {
                            col.Item().Text("SOLDE DE TOUT COMPTE").FontSize(12).Bold();
                            col.Item().Text(settlement.SettlementMonthLabel).FontSize(10);
                            col.Item().Text(settlement.DocumentReference).FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                    });
                    header.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingTop(6).Column(col =>
                {
                    col.Item().Text("Salarié").FontSize(10).Bold();
                    col.Item().PaddingBottom(8).Column(emp =>
                    {
                        WriteIdentityLine(emp, "Matricule", settlement.EmployeeNumber);
                        WriteIdentityLine(emp, "Nom & Prénom", settlement.EmployeeName);
                        WriteIdentityLine(emp, "N° CIN", settlement.Cin);
                        WriteIdentityLine(emp, "N° CNSS", settlement.CnssNumber);
                        WriteIdentityLine(emp, "Poste", settlement.JobTitle);
                        WriteIdentityLine(emp, "Date de sortie", terminationDate);
                    });

                    col.Item().PaddingBottom(8).Text(
                        $"Le présent document récapitule les éléments de rémunération et de retenue portés sur le bulletin "
                        + $"de paie du mois de sortie ({settlement.SettlementMonthLabel}), cycle de paie arrêté.")
                        .FontSize(10).LineHeight(1.35f);

                    col.Item().PaddingTop(4).Text("Détail du solde").FontSize(10).Bold();
                    col.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2.5f);
                            c.RelativeColumn(1f);
                        });

                        foreach (var line in settlement.Lines)
                        {
                            var isNet = string.Equals(line.Label, "Net à payer", StringComparison.OrdinalIgnoreCase);
                            WriteSettlementLine(table, line.Label, line.Amount, line.IsDeduction, bold: isNet);
                        }
                    });

                    col.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2.5f);
                            c.RelativeColumn(1f);
                        });
                        WriteAmountRow(table, "Salaire brut", settlement.GrossSalary);
                        WriteAmountRow(table, "CNSS salariale", settlement.CnssEmployee);
                        WriteAmountRow(table, "IRPP mensuel", settlement.Irpp);
                        if (settlement.IrppRegularization != 0)
                            WriteAmountRow(table, "Régularisation IRPP", settlement.IrppRegularization);
                        WriteAmountRow(table, "CSS mensuelle", settlement.Css);
                        if (settlement.CssRegularization != 0)
                            WriteAmountRow(table, "Régularisation CSS", settlement.CssRegularization);
                        if (settlement.OtherDeductions > 0)
                            WriteAmountRow(table, "Autres retenues", settlement.OtherDeductions);
                        WriteAmountRow(table, "Net à payer", settlement.NetSalary, bold: true);
                    });

                    if (settlement.Warnings.Count > 0)
                    {
                        col.Item().PaddingTop(8).Text($"Avertissements : {string.Join(" — ", settlement.Warnings)}")
                            .FontSize(8).FontColor(Colors.Orange.Darken2);
                    }

                    col.Item().PaddingTop(20).Text(
                        "Ce document est établi sur la base des bulletins de paie figés. "
                        + "Il ne se substitue pas aux obligations légales de remise des documents de fin de contrat.")
                        .FontSize(9).FontColor(Colors.Grey.Darken2);

                    col.Item().PaddingTop(16).Text("Fait pour servir et valoir ce que de droit.")
                        .FontSize(10).Italic();
                });

                page.Footer().AlignCenter().Text($"Généré le {generatedAt} — Document produit par InstaFact.")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        }).GeneratePdf();

        return Task.FromResult(bytes);
    }

    private static void WriteSettlementLine(
        TableDescriptor table,
        string label,
        decimal amount,
        bool isDeduction,
        bool bold = false)
    {
        var display = isDeduction && amount > 0 ? $"- {FormatAmount(amount)}" : FormatAmount(amount);
        var labelCell = table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(label).FontSize(9);
        if (bold) labelCell.Bold();

        var amountCell = table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4)
            .AlignRight().Text(display).FontSize(9);
        if (bold) amountCell.Bold();
    }
}
