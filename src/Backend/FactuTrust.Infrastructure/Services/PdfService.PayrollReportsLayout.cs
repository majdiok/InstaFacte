using FactuTrust.Application.Common.Enums;
using FactuTrust.Application.DTOs;
using FactuTrust.Infrastructure.Services.Templates;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Rendus PDF des états de contrôle paie (livre de paie simplifié, journal de paie).
/// Réutilise l'enveloppe tabulaire des états comptables (<c>BuildReport</c>, cellules, formats)
/// pour une présentation homogène, sans toucher aux rendus existants.
/// </summary>
public partial class PdfService
{
    private const string ProvisionalNotice = "ÉTAT PROVISOIRE — inclut des cycles calculés non validés";

    /// <summary>Bandeau d'avertissement affiché en tête des états incluant des cycles non validés.</summary>
    private static void ProvisionalBanner(ColumnDescriptor col)
    {
        col.Item()
            .PaddingBottom(6)
            .Background(Colors.Red.Lighten4)
            .Border(0.5f)
            .BorderColor(Colors.Red.Medium)
            .Padding(4)
            .Text(ProvisionalNotice)
            .FontSize(9)
            .Bold()
            .FontColor(Colors.Red.Darken2);
    }

    // ── Livre de paie simplifié ────────────────────────────────────────────────────────────

    public Task<byte[]> GeneratePayrollBookPdfAsync(
        PayrollBookDto book,
        AccountingReportHeader header,
        CancellationToken cancellationToken = default)
    {
        // Les colonnes de régularisation n'apparaissent que si l'exercice en comporte : un mois
        // ordinaire garde ainsi un état lisible.
        var showRegularization = book.Lines.Any(l => l.IrppRegularization != 0m || l.CssRegularization != 0m);

        var bytes = BuildReport(header, landscape: true, col =>
        {
            if (book.IsProvisional)
                ProvisionalBanner(col);

            if (book.Lines.Count == 0)
            {
                EmptyNotice(col, "Aucun bulletin sur la période.");
                return;
            }

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(58);   // matricule
                    c.RelativeColumn(3);    // salarié
                    c.ConstantColumn(70);   // n° CNSS
                    c.ConstantColumn(32);   // mois
                    c.ConstantColumn(72);   // brut
                    c.ConstantColumn(72);   // brut CNSSable
                    c.ConstantColumn(66);   // CNSS salarié
                    c.ConstantColumn(72);   // net imposable
                    c.ConstantColumn(62);   // IRPP
                    c.ConstantColumn(56);   // CSS
                    if (showRegularization)
                        c.ConstantColumn(66); // régularisations
                    c.ConstantColumn(66);   // autres retenues
                    c.ConstantColumn(76);   // net à payer
                });

                table.Header(h =>
                {
                    h.Cell().Element(HeadCell).Text("Matricule").Bold();
                    h.Cell().Element(HeadCell).Text("Salarié").Bold();
                    h.Cell().Element(HeadCell).Text("N° CNSS").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Mois").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Brut").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Brut CNSS").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("CNSS sal.").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Net imposable").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("IRPP").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("CSS").Bold();
                    if (showRegularization)
                        h.Cell().Element(HeadCell).AlignRight().Text("Régul.").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Autres ret.").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Net à payer").Bold();
                });

                foreach (var line in book.Lines)
                {
                    table.Cell().Element(BodyCell).Text(PdfRenderHelpers.CleanTextForPdf(line.EmployeeNumber));
                    table.Cell().Element(BodyCell).Text(PdfRenderHelpers.CleanTextForPdf(line.EmployeeName));
                    table.Cell().Element(BodyCell).Text(PdfRenderHelpers.CleanTextForPdf(line.CnssNumber ?? string.Empty));
                    table.Cell().Element(BodyCell).AlignRight().Text(line.MonthsCount.ToString());
                    table.Cell().Element(BodyCell).AlignRight().Text(Amount(line.GrossSalary));
                    table.Cell().Element(BodyCell).AlignRight().Text(Amount(line.CnssableGross));
                    table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(line.CnssEmployee));
                    table.Cell().Element(BodyCell).AlignRight().Text(Amount(line.NetTaxable));
                    table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(line.Irpp));
                    table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(line.Css));
                    if (showRegularization)
                        table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(line.IrppRegularization + line.CssRegularization));
                    table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(line.OtherDeductions));
                    table.Cell().Element(BodyCell).AlignRight().Text(Amount(line.NetSalary));
                }

                table.Cell().ColumnSpan(3).Element(TotalCell).Text($"TOTAUX — {book.EmployeeCount} salarié(s)").Bold();
                table.Cell().Element(TotalCell).Text(string.Empty);
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(book.TotalGross)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(book.TotalCnssableGross)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(book.TotalCnssEmployee)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(book.TotalNetTaxable)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(book.TotalIrpp)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(book.TotalCss)).Bold();
                if (showRegularization)
                    table.Cell().Element(TotalCell).AlignRight().Text(Amount(book.TotalIrppRegularization + book.TotalCssRegularization)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(book.TotalOtherDeductions)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(book.TotalNetSalary)).Bold();
            });

            RecapBlock(col, "Charges patronales de la période", new (string Label, decimal Value)[]
            {
                ("CNSS patronale", book.TotalCnssEmployer),
                ("Accident de travail", book.TotalWorkAccident),
                ("TFP", book.TotalTfp),
                ("FOPROLOS", book.TotalFoprolos),
                ("CSS patronale", book.TotalCssEmployer),
                ("Total charges patronales", book.TotalEmployerCharges),
                ("Coût employeur (brut + charges)", book.TotalEmployerCost)
            });

            if (book.MissingMonths.Count > 0)
            {
                col.Item().PaddingTop(8).Text(
                        $"Mois sans cycle éligible : {string.Join(", ", book.MissingMonths)}.")
                    .FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
            }
        });

        return Task.FromResult(bytes);
    }

    // ── Journal de paie ────────────────────────────────────────────────────────────────────

    public Task<byte[]> GeneratePayrollJournalPdfAsync(
        PayrollJournalDto journal,
        PayrollJournalView view,
        AccountingReportHeader header,
        CancellationToken cancellationToken = default)
    {
        var bytes = view == PayrollJournalView.Accounting
            ? BuildJournalAccountingPdf(journal, header)
            : BuildJournalByEmployeePdf(journal, header);

        return Task.FromResult(bytes);
    }

    private static byte[] BuildJournalByEmployeePdf(PayrollJournalDto journal, AccountingReportHeader header) =>
        BuildReport(header, landscape: true, col =>
        {
            if (journal.IsProvisional)
                ProvisionalBanner(col);

            if (journal.Lines.Count == 0)
            {
                EmptyNotice(col, "Aucun bulletin sur le mois.");
                return;
            }

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(58);   // matricule
                    c.RelativeColumn(3);    // salarié
                    c.ConstantColumn(76);   // brut
                    c.ConstantColumn(70);   // CNSS salarié
                    c.ConstantColumn(76);   // net imposable
                    c.ConstantColumn(66);   // IRPP
                    c.ConstantColumn(58);   // CSS
                    c.ConstantColumn(70);   // autres retenues
                    c.ConstantColumn(80);   // net à payer
                    c.ConstantColumn(78);   // charges patronales
                    c.ConstantColumn(82);   // coût employeur
                });

                table.Header(h =>
                {
                    h.Cell().Element(HeadCell).Text("Matricule").Bold();
                    h.Cell().Element(HeadCell).Text("Salarié").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Brut").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("CNSS sal.").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Net imposable").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("IRPP").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("CSS").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Autres ret.").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Net à payer").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Charges pat.").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Coût employeur").Bold();
                });

                foreach (var line in journal.Lines)
                {
                    table.Cell().Element(BodyCell).Text(PdfRenderHelpers.CleanTextForPdf(line.EmployeeNumber));
                    table.Cell().Element(BodyCell).Text(PdfRenderHelpers.CleanTextForPdf(line.EmployeeName));
                    table.Cell().Element(BodyCell).AlignRight().Text(Amount(line.GrossSalary));
                    table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(line.CnssEmployee));
                    table.Cell().Element(BodyCell).AlignRight().Text(Amount(line.MonthlyNetTaxable));
                    table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(line.Irpp + line.IrppRegularization));
                    table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(line.Css + line.CssRegularization));
                    table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(line.OtherDeductions));
                    table.Cell().Element(BodyCell).AlignRight().Text(Amount(line.NetSalary));
                    table.Cell().Element(BodyCell).AlignRight().Text(Amount(line.TotalEmployerCharges));
                    table.Cell().Element(BodyCell).AlignRight().Text(Amount(line.TotalCost));
                }

                table.Cell().ColumnSpan(2).Element(TotalCell).Text($"TOTAUX — {journal.EmployeeCount} salarié(s)").Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(journal.TotalGross)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(journal.TotalCnssEmployee)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(journal.TotalNetTaxable)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(journal.TotalIrpp + journal.TotalIrppRegularization)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(journal.TotalCss + journal.TotalCssRegularization)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(journal.TotalOtherDeductions)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(journal.TotalNetSalary)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(journal.TotalEmployerCharges)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(journal.TotalEmployerCost)).Bold();
            });

            RecapBlock(col, "Détail des charges patronales", new (string Label, decimal Value)[]
            {
                ("CNSS patronale", journal.TotalCnssEmployer),
                ("Accident de travail", journal.TotalWorkAccident),
                ("TFP", journal.TotalTfp),
                ("FOPROLOS", journal.TotalFoprolos),
                ("CSS patronale", journal.TotalCssEmployer)
            });
        });

    private static byte[] BuildJournalAccountingPdf(PayrollJournalDto journal, AccountingReportHeader header) =>
        BuildReport(header, landscape: false, col =>
        {
            if (journal.IsProvisional)
                ProvisionalBanner(col);

            col.Item().PaddingBottom(6).Text(BuildAccountingOriginNotice(journal))
                .FontSize(8).Italic().FontColor(Colors.Grey.Darken2);

            if (journal.AccountingLines.Count == 0)
            {
                EmptyNotice(col, "Aucune ventilation comptable disponible pour ce cycle.");
                return;
            }

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(56);   // compte
                    c.RelativeColumn(3);    // libellé
                    c.ConstantColumn(84);   // débit
                    c.ConstantColumn(84);   // crédit
                });

                table.Header(h =>
                {
                    h.Cell().Element(HeadCell).Text("Compte").Bold();
                    h.Cell().Element(HeadCell).Text("Libellé").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Débit").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Crédit").Bold();
                });

                foreach (var line in journal.AccountingLines)
                {
                    var label = string.IsNullOrWhiteSpace(line.AccountLabel)
                        ? line.Label
                        : $"{line.AccountLabel} — {line.Label}";

                    table.Cell().Element(BodyCell).Text(line.AccountNumber);
                    table.Cell().Element(BodyCell).Text(PdfRenderHelpers.CleanTextForPdf(label));
                    table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(line.Debit));
                    table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(line.Credit));
                }

                table.Cell().ColumnSpan(2).Element(TotalCell).AlignRight().Text("TOTAUX").Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(journal.TotalDebit)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(journal.TotalCredit)).Bold();
            });

            if (!journal.IsBalanced)
            {
                col.Item().PaddingTop(8).Text("Contrôle : total débit ≠ total crédit — vérifier les totaux du cycle.")
                    .FontSize(9).Bold().FontColor(Colors.Red.Darken2);
            }
        });

    private static string BuildAccountingOriginNotice(PayrollJournalDto journal) =>
        journal.AccountingLinesArePosted
            ? $"Écriture comptabilisée n° {journal.AccountingEntryNumber} — journal {journal.AccountingJournalCode} du {ShortDate(journal.AccountingEntryDate ?? DateTime.Today)}."
            : "Ventilation simulée : le cycle n'est pas encore comptabilisé.";

    /// <summary>Encadré récapitulatif « libellé / montant » affiché sous un tableau d'état.</summary>
    private static void RecapBlock(ColumnDescriptor col, string title, IReadOnlyList<(string Label, decimal Value)> rows)
    {
        col.Item().PaddingTop(12).Text(title).FontSize(10).Bold().FontColor(Colors.Blue.Darken2);
        col.Item().PaddingTop(4).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn();
                c.ConstantColumn(110);
            });

            foreach (var (label, value) in rows)
            {
                table.Cell().Element(BodyCell).Text(label);
                table.Cell().Element(BodyCell).AlignRight().Text(Amount(value));
            }
        });
    }
}
