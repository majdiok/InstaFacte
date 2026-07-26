using System.Globalization;
using FactuTrust.Application.Accounting;
using FactuTrust.Application.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FactuTrust.Infrastructure.Services;

public partial class PdfService
{
    /// <summary>Chemin legacy : toutes les sections + notes agrégées (comportement historique).</summary>
    public Task<byte[]> GenerateNctLiassePdfAsync(NctFinancialStatementsDto dto, string companyName, CancellationToken cancellationToken = default)
    {
        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(32);
                page.DefaultTextStyle(t => t.FontSize(9).FontColor(Colors.Grey.Darken3));

                page.Header().Column(header =>
                {
                    header.Item().Text(string.IsNullOrWhiteSpace(companyName) ? "Société" : companyName).FontSize(15).Bold();
                    header.Item().Text($"États financiers NCT — Exercice {dto.FiscalYear}").FontSize(12).FontColor(Colors.Blue.Darken2);
                    header.Item().PaddingBottom(6).Text("Système comptable des entreprises (Normes Comptables Tunisiennes)").FontSize(9).FontColor(Colors.Grey.Darken1);
                    header.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingVertical(8).Column(col =>
                {
                    NctSectionTitle(col, "BILAN — ACTIF");
                    NctTable(col, dto.BalanceSheet.Assets);
                    NctSectionTitle(col, "BILAN — CAPITAUX PROPRES ET PASSIFS");
                    NctTable(col, dto.BalanceSheet.EquityAndLiabilities);
                    col.Item().PaddingTop(2).Text(dto.BalanceSheet.IsBalanced ? "Bilan équilibré." : "Écart Actif / Passif détecté.")
                        .FontSize(8).FontColor(dto.BalanceSheet.IsBalanced ? Colors.Green.Darken1 : Colors.Red.Darken1);

                    NctSectionTitle(col, "COMPTE DE RÉSULTAT");
                    NctTable(col, dto.IncomeStatement.Lines);

                    NctSectionTitle(col, "TABLEAU DE FLUX DE TRÉSORERIE (méthode indirecte)");
                    NctTable(col, dto.CashFlow.Lines);

                    NctSectionTitle(col, "VARIATION DES CAPITAUX PROPRES");
                    NctTable(col, dto.EquityChanges.Lines);

                    foreach (var note in dto.Notes)
                    {
                        NctSectionTitle(col, note.Title);
                        if (!string.IsNullOrWhiteSpace(note.Description))
                            col.Item().PaddingBottom(4).Text(note.Description).FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
                        if (note.Lines.Count > 0)
                            NctTable(col, note.Lines);
                    }
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text(t =>
                    {
                        t.Span("Généré le ").FontSize(8).FontColor(Colors.Grey.Darken1);
                        t.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)).FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                    row.ConstantItem(120).AlignRight().Text(t =>
                    {
                        t.CurrentPageNumber().FontSize(8);
                        t.Span(" / ").FontSize(8);
                        t.TotalPages().FontSize(8);
                    });
                });
            });
        }).GeneratePdf();

        return Task.FromResult(bytes);
    }

    /// <summary>Chemin dialogue : sections conditionnelles + notes détaillées.</summary>
    public Task<byte[]> GenerateNctLiassePdfAsync(NctLiasseExportView exportView, string companyName, CancellationToken cancellationToken = default)
    {
        var dto = exportView.Statements;
        var options = exportView.Options;
        var nLabel = options.CurrentPeriodLabel;
        var n1Label = options.PreviousPeriodLabel;

        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(32);
                page.DefaultTextStyle(t => t.FontSize(9).FontColor(Colors.Grey.Darken3));

                page.Header().Column(header =>
                {
                    header.Item().Text(string.IsNullOrWhiteSpace(companyName) ? "Société" : companyName).FontSize(15).Bold();
                    header.Item().Text($"États financiers NCT — Exercice {dto.FiscalYear}").FontSize(12).FontColor(Colors.Blue.Darken2);
                    header.Item().Text($"Arrêtée au {nLabel}").FontSize(9).FontColor(Colors.Grey.Darken1);
                    header.Item().PaddingBottom(6).Text("Système comptable des entreprises (Normes Comptables Tunisiennes)").FontSize(9).FontColor(Colors.Grey.Darken1);
                    header.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingVertical(8).Column(col =>
                {
                    if (exportView.IncludeAssets)
                    {
                        NctSectionTitle(col, "BILAN — ACTIF");
                        NctTable(col, dto.BalanceSheet.Assets, nLabel, n1Label);
                    }

                    if (exportView.IncludeLiabilities)
                    {
                        NctSectionTitle(col, "BILAN — CAPITAUX PROPRES ET PASSIFS");
                        NctTable(col, dto.BalanceSheet.EquityAndLiabilities, nLabel, n1Label);
                        col.Item().PaddingTop(2).Text(dto.BalanceSheet.IsBalanced ? "Bilan équilibré." : "Écart Actif / Passif détecté.")
                            .FontSize(8).FontColor(dto.BalanceSheet.IsBalanced ? Colors.Green.Darken1 : Colors.Red.Darken1);

                        NctSectionTitle(col, "VARIATION DES CAPITAUX PROPRES");
                        NctTable(col, dto.EquityChanges.Lines, nLabel, n1Label);
                    }

                    if (exportView.IncludeIncomeStatement)
                    {
                        NctSectionTitle(col, "COMPTE DE RÉSULTAT");
                        NctTable(col, dto.IncomeStatement.Lines, nLabel, n1Label);
                    }

                    if (exportView.IncludeCashFlow)
                    {
                        NctSectionTitle(col, "TABLEAU DE FLUX DE TRÉSORERIE (méthode indirecte)");
                        NctTable(col, dto.CashFlow.Lines, nLabel, n1Label);
                    }

                    RenderDetailedNotesFamily(col, exportView.DetailedNotes, NctAnnexFamily.Actif, "NOTES ACTIF BILAN", nLabel, n1Label);
                    RenderDetailedNotesFamily(col, exportView.DetailedNotes, NctAnnexFamily.Passif, "NOTES PASSIF BILAN", nLabel, n1Label);
                    RenderDetailedNotesFamily(col, exportView.DetailedNotes, NctAnnexFamily.IncomeStatement, "NOTES COMPTE DE RÉSULTAT", nLabel, n1Label);
                    RenderDetailedNotesFamily(col, exportView.DetailedNotes, NctAnnexFamily.CashFlow, "NOTES FLUX DE TRÉSORERIE", nLabel, n1Label);
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text(t =>
                    {
                        t.Span("Généré le ").FontSize(8).FontColor(Colors.Grey.Darken1);
                        t.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)).FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                    row.ConstantItem(120).AlignRight().Text(t =>
                    {
                        t.CurrentPageNumber().FontSize(8);
                        t.Span(" / ").FontSize(8);
                        t.TotalPages().FontSize(8);
                    });
                });
            });
        }).GeneratePdf();

        return Task.FromResult(bytes);
    }

    private static void RenderDetailedNotesFamily(
        ColumnDescriptor col,
        IReadOnlyList<NctDetailedNoteDto> notes,
        NctAnnexFamily family,
        string familyTitle,
        string nLabel,
        string n1Label)
    {
        var familyNotes = notes.Where(n => n.Family == family).OrderBy(n => n.Number).ToList();
        if (familyNotes.Count == 0)
            return;

        NctSectionTitle(col, familyTitle);
        foreach (var note in familyNotes)
        {
            col.Item().PaddingTop(8).PaddingBottom(2)
                .Text($"Note: {note.Number}  {note.Title}")
                .FontSize(10).Bold();
            // Texte narratif saisi par le comptable (personnalisation d'annexe), s'il existe.
            if (!string.IsNullOrWhiteSpace(note.Description))
                col.Item().PaddingBottom(3).Text(note.Description).FontSize(8).Italic().FontColor(Colors.Grey.Darken2);
            NctDetailedNoteTable(col, note, nLabel, n1Label);
        }
    }

    private static void NctDetailedNoteTable(
        ColumnDescriptor col,
        NctDetailedNoteDto note,
        string nLabel,
        string n1Label)
    {
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(1.4f);
                c.RelativeColumn(3.2f);
                c.RelativeColumn(1.7f);
                c.RelativeColumn(1.7f);
            });

            table.Header(h =>
            {
                h.Cell().Text("N° compte").FontSize(8).Bold().FontColor(Colors.Grey.Darken1);
                h.Cell().Text("Intitulé").FontSize(8).Bold().FontColor(Colors.Grey.Darken1);
                h.Cell().AlignRight().Text(nLabel).FontSize(8).Bold().FontColor(Colors.Grey.Darken1);
                h.Cell().AlignRight().Text(n1Label).FontSize(8).Bold().FontColor(Colors.Grey.Darken1);
            });

            foreach (var line in note.Lines)
            {
                table.Cell().PaddingVertical(2).Text(line.AccountNumber).FontSize(9);
                table.Cell().PaddingVertical(2).Text(line.Label).FontSize(9);
                table.Cell().PaddingVertical(2).AlignRight()
                    .Text(line.Amount.ToString("N3", CultureInfo.InvariantCulture)).FontSize(9);
                table.Cell().PaddingVertical(2).AlignRight()
                    .Text(line.PreviousAmount.ToString("N3", CultureInfo.InvariantCulture)).FontSize(9)
                    .FontColor(Colors.Grey.Darken1);
            }

            var bg = Colors.Grey.Lighten3;
            table.Cell().Background(bg).PaddingVertical(2).Text(string.Empty);
            table.Cell().Background(bg).PaddingVertical(2).Text("TOTAL").FontSize(9).Bold();
            table.Cell().Background(bg).PaddingVertical(2).AlignRight()
                .Text(note.Total.ToString("N3", CultureInfo.InvariantCulture)).FontSize(9).Bold();
            table.Cell().Background(bg).PaddingVertical(2).AlignRight()
                .Text(note.PreviousTotal.ToString("N3", CultureInfo.InvariantCulture)).FontSize(9).Bold();
        });
    }

    private static void NctSectionTitle(ColumnDescriptor col, string title)
    {
        col.Item().PaddingTop(12).PaddingBottom(3).Text(title).FontSize(11).Bold().FontColor(Colors.Blue.Darken2);
    }

    private static void NctTable(ColumnDescriptor col, IReadOnlyList<NctLineDto> lines)
        => NctTable(col, lines, "Exercice N", "Exercice N-1");

    private static void NctTable(ColumnDescriptor col, IReadOnlyList<NctLineDto> lines, string nLabel, string n1Label)
    {
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(4);
                c.RelativeColumn(2);
                c.RelativeColumn(2);
            });

            table.Header(h =>
            {
                h.Cell().Text("Rubrique").FontSize(8).Bold().FontColor(Colors.Grey.Darken1);
                h.Cell().AlignRight().Text(nLabel).FontSize(8).Bold().FontColor(Colors.Grey.Darken1);
                h.Cell().AlignRight().Text(n1Label).FontSize(8).Bold().FontColor(Colors.Grey.Darken1);
            });

            foreach (var line in lines)
            {
                var bg = line.IsSubtotal ? Colors.Grey.Lighten3 : Colors.White;
                var labelCell = table.Cell().Background(bg).PaddingVertical(2).PaddingLeft(6 + line.Level * 12f)
                    .Text(line.Label).FontSize(9);
                if (line.IsSubtotal) labelCell.Bold();

                var nCell = table.Cell().Background(bg).PaddingVertical(2).AlignRight()
                    .Text(line.Amount.ToString("N3", CultureInfo.InvariantCulture)).FontSize(9);
                if (line.IsSubtotal) nCell.Bold();

                table.Cell().Background(bg).PaddingVertical(2).AlignRight()
                    .Text(line.PreviousAmount.ToString("N3", CultureInfo.InvariantCulture)).FontSize(9).FontColor(Colors.Grey.Darken1);
            }
        });
    }
}
