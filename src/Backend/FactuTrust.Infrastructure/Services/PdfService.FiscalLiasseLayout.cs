using FactuTrust.Application.DTOs;
using FactuTrust.Infrastructure.Services.Templates;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Rendus PDF de la partie fiscale de la liasse : détermination du résultat fiscal (réintégrations /
/// déductions + calcul de l'impôt) et liasse consolidée (états NCT + fiscal + tableaux annexes).
/// Réutilise les helpers de <see cref="PdfService"/> (BuildReport, en-tête, cellules, formatage).
/// </summary>
public partial class PdfService
{
    public Task<byte[]> GenerateFiscalResultPdfAsync(FiscalResultDeclarationDto dto, AccountingReportHeader header, CancellationToken cancellationToken = default)
    {
        var bytes = BuildReport(header, landscape: false, col => ComposeFiscalDetermination(col, dto));
        return Task.FromResult(bytes);
    }

    public Task<byte[]> GenerateConsolidatedLiassePdfAsync(ConsolidatedLiasseDto dto, AccountingReportHeader header, CancellationToken cancellationToken = default)
    {
        var bytes = BuildReport(header, landscape: false, col =>
        {
            col.Item().Text("ÉTATS FINANCIERS (NCT)").FontSize(12).Bold().FontColor(Colors.Blue.Darken2);
            col.Item().PaddingTop(6).Text("Bilan — Actif").Bold();
            NctLinesTable(col, dto.FinancialStatements.BalanceSheet.Assets);
            col.Item().PaddingTop(6).Text("Bilan — Capitaux propres et passifs").Bold();
            NctLinesTable(col, dto.FinancialStatements.BalanceSheet.EquityAndLiabilities);
            col.Item().PaddingTop(6).Text("Compte de résultat").Bold();
            NctLinesTable(col, dto.FinancialStatements.IncomeStatement.Lines);

            col.Item().PageBreak();
            ComposeFiscalDetermination(col, dto.FiscalResult);

            if (dto.AmortizationTable.Count > 0)
            {
                col.Item().PageBreak();
                FiscalAnnexTable(col, "TABLEAU DES AMORTISSEMENTS", dto.AmortizationTable, "Dotation exercice", "VNC");
            }
            if (dto.ProvisionsTable.Count > 0)
            {
                col.Item().PaddingTop(12);
                FiscalAnnexTable(col, "TABLEAU DES PROVISIONS", dto.ProvisionsTable, "Montant", "N-1");
            }
        });
        return Task.FromResult(bytes);
    }

    private static void ComposeFiscalDetermination(ColumnDescriptor col, FiscalResultDeclarationDto dto)
    {
        var kindLabel = dto.TaxpayerKind == 1 ? "IRPP (BIC)" : "IS (sociétés)";
        col.Item().Text($"DÉTERMINATION DU RÉSULTAT FISCAL — {kindLabel}").FontSize(12).Bold().FontColor(Colors.Blue.Darken2);

        var reint = dto.Adjustments.Where(a => a.Kind == 0).ToList();
        col.Item().PaddingTop(8).Text("Réintégrations").Bold();
        AdjustmentTable(col, reint, dto.Computation.TotalReintegrations, "Total des réintégrations");

        var deduc = dto.Adjustments.Where(a => a.Kind == 1).ToList();
        col.Item().PaddingTop(8).Text("Déductions").Bold();
        AdjustmentTable(col, deduc, dto.Computation.TotalDeductions, "Total des déductions");

        col.Item().PaddingTop(8).Text("Calcul de l'impôt").Bold();
        var c = dto.Computation;
        var rows = new List<(string Label, decimal Value, bool Emphasize)>
        {
            ("Résultat comptable net", c.AccountingResult, false),
            ("+ Réintégrations", c.TotalReintegrations, false),
            ("- Déductions", c.TotalDeductions, false),
            ("Résultat fiscal avant reports", c.ResultBeforeCarryForward, true),
            ("- Déficits imputés", c.DeficitsImputed, false),
            ("- Amortissements différés imputés", c.DeferredDepreciationImputed, false),
            ("Résultat fiscal imposable", c.TaxableResult, true),
            (dto.TaxpayerKind == 1 ? "IRPP (barème)" : "IS (résultat × taux)", c.TaxOnResult, false),
            ("Minimum d'impôt", c.MinimumTax, false),
            ("Impôt dû", c.TaxDue, true),
            ("Contribution sociale de solidarité (CSS)", c.Css, false),
            ("Total impôt dû", c.TotalTaxDue, true),
            ("- Acomptes provisionnels", c.AcomptesPaid, false),
            ("- Retenues à la source subies", c.WithholdingSuffered, false),
            ("- Crédit d'impôt antérieur", c.PriorTaxCredit, false),
            ("Net à payer", c.NetToPay, true),
            ("Crédit d'impôt à reporter", c.CreditToCarry, true)
        };

        col.Item().Table(table =>
        {
            table.ColumnsDefinition(cd => { cd.RelativeColumn(3); cd.ConstantColumn(110); });
            foreach (var (label, value, emphasize) in rows)
            {
                var t1 = table.Cell().Element(emphasize ? TotalCell : BodyCell).Text(label);
                if (emphasize) t1.Bold();
                var t2 = table.Cell().Element(emphasize ? TotalCell : BodyCell).AlignRight().Text(Amount(value));
                if (emphasize) t2.Bold();
            }
        });
    }

    private static void AdjustmentTable(ColumnDescriptor col, IReadOnlyList<FiscalAdjustmentLineDto> lines, decimal total, string totalLabel)
    {
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(cd => { cd.RelativeColumn(3); cd.ConstantColumn(110); });
            table.Header(h =>
            {
                h.Cell().Element(HeadCell).Text("Libellé").Bold();
                h.Cell().Element(HeadCell).AlignRight().Text("Montant").Bold();
            });
            if (lines.Count == 0)
            {
                table.Cell().Element(BodyCell).Text("—").FontColor(Colors.Grey.Darken1);
                table.Cell().Element(BodyCell).AlignRight().Text("0.000");
            }
            foreach (var l in lines)
            {
                table.Cell().Element(BodyCell).Text(PdfRenderHelpers.CleanTextForPdf(l.Label));
                table.Cell().Element(BodyCell).AlignRight().Text(Amount(l.Amount));
            }
            table.Cell().Element(TotalCell).Text(totalLabel).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Amount(total)).Bold();
        });
    }

    private static void NctLinesTable(ColumnDescriptor col, IReadOnlyList<NctLineDto> lines)
    {
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(cd => { cd.RelativeColumn(3); cd.ConstantColumn(100); cd.ConstantColumn(100); });
            table.Header(h =>
            {
                h.Cell().Element(HeadCell).Text("Rubrique").Bold();
                h.Cell().Element(HeadCell).AlignRight().Text("Exercice").Bold();
                h.Cell().Element(HeadCell).AlignRight().Text("N-1").Bold();
            });
            foreach (var l in lines)
            {
                var lt = table.Cell().Element(l.IsSubtotal ? TotalCell : BodyCell).PaddingLeft(l.Level * 10).Text(l.Label);
                if (l.IsSubtotal) lt.Bold();
                var at1 = table.Cell().Element(l.IsSubtotal ? TotalCell : BodyCell).AlignRight().Text(Amount(l.Amount));
                if (l.IsSubtotal) at1.Bold();
                var at2 = table.Cell().Element(l.IsSubtotal ? TotalCell : BodyCell).AlignRight().Text(Amount(l.PreviousAmount));
                if (l.IsSubtotal) at2.Bold();
            }
        });
    }

    private static void FiscalAnnexTable(ColumnDescriptor col, string title, IReadOnlyList<FiscalTableRowDto> rows, string amountHeader, string previousHeader)
    {
        col.Item().Text(title).FontSize(12).Bold().FontColor(Colors.Blue.Darken2);
        col.Item().PaddingTop(4).Table(table =>
        {
            table.ColumnsDefinition(cd => { cd.ConstantColumn(80); cd.RelativeColumn(3); cd.ConstantColumn(100); cd.ConstantColumn(100); });
            table.Header(h =>
            {
                h.Cell().Element(HeadCell).Text("Code").Bold();
                h.Cell().Element(HeadCell).Text("Libellé").Bold();
                h.Cell().Element(HeadCell).AlignRight().Text(amountHeader).Bold();
                h.Cell().Element(HeadCell).AlignRight().Text(previousHeader).Bold();
            });
            foreach (var r in rows)
            {
                table.Cell().Element(BodyCell).Text(r.Code);
                table.Cell().Element(BodyCell).Text(PdfRenderHelpers.CleanTextForPdf(r.Label));
                table.Cell().Element(BodyCell).AlignRight().Text(Amount(r.Amount));
                table.Cell().Element(BodyCell).AlignRight().Text(r.PreviousAmount.HasValue ? Amount(r.PreviousAmount.Value) : string.Empty);
            }
        });
    }
}
