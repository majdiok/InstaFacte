using System.Globalization;
using FactuTrust.Application.DTOs;
using FactuTrust.Infrastructure.Services.Templates;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Rendus PDF tabulaires des états comptables (journal, grand livre, balance, auxiliaires,
/// balance âgée, bilan, compte de résultat). Isolé des rendus historiques (facture, paie, TVA,
/// liasse NCT) pour garantir zéro régression.
/// </summary>
public partial class PdfService
{
    // Le dinar tunisien s'affiche « 30 000.000 » (séparateur de milliers = espace, décimale = point).
    private static readonly System.Globalization.NumberFormatInfo TnAmountFormat = new()
    {
        NumberGroupSeparator = " ",
        NumberDecimalSeparator = ".",
        NumberGroupSizes = new[] { 3 },
        NumberDecimalDigits = 3
    };

    private static readonly TextStyle ReportTextStyle = TextStyle.Default
        .FontFamily("Lato", "Arial", "Helvetica", "DejaVu Sans", "Liberation Sans", "Calibri", "Segoe UI")
        .FontSize(9);

    private static string Amount(decimal v) => v.ToString("#,##0.000", TnAmountFormat);
    private static string AmountOrBlank(decimal v) => v == 0m ? string.Empty : Amount(v);
    private static string ShortDate(DateTime d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    // ── Enveloppe commune (en-tête société + pied paginé) ──────────────────────────────────

    private static byte[] BuildReport(AccountingReportHeader header, bool landscape, Action<ColumnDescriptor> content)
    {
        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(landscape ? PageSizes.A4.Landscape() : PageSizes.A4);
                page.Margin(24);
                page.DefaultTextStyle(ReportTextStyle);
                page.Header().Element(c => ComposeReportHeader(c, header));
                page.Content().PaddingVertical(8).Column(content);
                page.Footer().Element(ComposeReportFooter);
            });
        }).GeneratePdf();
    }

    private static void ComposeReportHeader(IContainer container, AccountingReportHeader h)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(string.IsNullOrWhiteSpace(h.CompanyName) ? "Société" : h.CompanyName).FontSize(14).Bold();
                    if (!string.IsNullOrWhiteSpace(h.MatriculeFiscal))
                        c.Item().Text($"Matricule fiscal : {h.MatriculeFiscal}").FontSize(8).FontColor(Colors.Grey.Darken1);
                });
                row.ConstantItem(150).AlignRight().Text("exprimé en Dinars").FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
            });
            col.Item().PaddingTop(6).Text(h.Title).FontSize(13).Bold().FontColor(Colors.Blue.Darken2);
            if (!string.IsNullOrWhiteSpace(h.PeriodLabel))
                col.Item().Text(h.PeriodLabel).FontSize(9).FontColor(Colors.Grey.Darken2);
            col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Medium);
        });
    }

    private static void ComposeReportFooter(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
            col.Item().PaddingTop(2).Row(row =>
            {
                row.RelativeItem().Text(t =>
                {
                    t.Span("Édité le ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)).FontSize(8).FontColor(Colors.Grey.Darken1);
                });
                row.ConstantItem(120).AlignRight().Text(t =>
                {
                    t.Span("Page ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.Span(" / ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.TotalPages().FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        });
    }

    private static IContainer HeadCell(IContainer c) =>
        c.Background(Colors.Grey.Lighten2).PaddingVertical(3).PaddingHorizontal(4);

    private static IContainer BodyCell(IContainer c) =>
        c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2).PaddingHorizontal(4);

    private static IContainer TotalCell(IContainer c) =>
        c.Background(Colors.Grey.Lighten3).BorderTop(1).BorderColor(Colors.Grey.Medium).PaddingVertical(3).PaddingHorizontal(4);

    private static void EmptyNotice(ColumnDescriptor col, string message) =>
        col.Item().PaddingTop(24).AlignCenter().Text(message).FontColor(Colors.Grey.Darken1).Italic();

    // ── Journal (regroupé par code journal) ────────────────────────────────────────────────

    public Task<byte[]> GenerateJournalPdfAsync(IReadOnlyList<JournalEntryDto> entries, AccountingReportHeader header, CancellationToken cancellationToken = default)
    {
        var bytes = BuildReport(header, landscape: true, col =>
        {
            if (entries.Count == 0)
            {
                EmptyNotice(col, "Aucune écriture sur la période.");
                return;
            }

            decimal grandDebit = 0, grandCredit = 0;
            foreach (var group in entries.GroupBy(e => e.JournalCode).OrderBy(g => g.Key))
            {
                col.Item().PaddingTop(10).PaddingBottom(2).Text($"Journal {group.Key}").FontSize(11).Bold().FontColor(Colors.Blue.Darken2);
                decimal jDebit = 0, jCredit = 0;

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(62);   // date
                        c.ConstantColumn(42);   // pièce
                        c.ConstantColumn(80);   // compte
                        c.RelativeColumn(3);    // libellé
                        c.ConstantColumn(90);   // débit
                        c.ConstantColumn(90);   // crédit
                    });

                    table.Header(h =>
                    {
                        h.Cell().Element(HeadCell).Text("Date").Bold();
                        h.Cell().Element(HeadCell).Text("Pièce").Bold();
                        h.Cell().Element(HeadCell).Text("Compte").Bold();
                        h.Cell().Element(HeadCell).Text("Libellé").Bold();
                        h.Cell().Element(HeadCell).AlignRight().Text("Débit").Bold();
                        h.Cell().Element(HeadCell).AlignRight().Text("Crédit").Bold();
                    });

                    foreach (var entry in group.OrderBy(e => e.EntryDate).ThenBy(e => e.EntryNumber))
                    {
                        foreach (var line in entry.Lines)
                        {
                            table.Cell().Element(BodyCell).Text(ShortDate(entry.EntryDate));
                            table.Cell().Element(BodyCell).Text(entry.EntryNumber.ToString(CultureInfo.InvariantCulture));
                            table.Cell().Element(BodyCell).Text(line.AccountNumber);
                            table.Cell().Element(BodyCell).Text(PdfRenderHelpers.CleanTextForPdf(line.Label));
                            table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(line.Debit));
                            table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(line.Credit));
                            jDebit += line.Debit;
                            jCredit += line.Credit;
                        }
                    }

                    table.Cell().ColumnSpan(4).Element(TotalCell).AlignRight().Text($"Total journal {group.Key}").Bold();
                    table.Cell().Element(TotalCell).AlignRight().Text(Amount(jDebit)).Bold();
                    table.Cell().Element(TotalCell).AlignRight().Text(Amount(jCredit)).Bold();
                });

                grandDebit += jDebit;
                grandCredit += jCredit;
            }

            col.Item().PaddingTop(12).Row(row =>
            {
                row.RelativeItem().AlignRight().PaddingRight(10).Text("TOTAL GÉNÉRAL").Bold().FontSize(10);
                row.ConstantItem(90).AlignRight().Text(Amount(grandDebit)).Bold().FontSize(10);
                row.ConstantItem(90).AlignRight().Text(Amount(grandCredit)).Bold().FontSize(10);
            });
        });

        return Task.FromResult(bytes);
    }

    // ── Récapitulatifs de journaux (centralisateur / récapitulation / totaux) ──────────────

    public Task<byte[]> GenerateJournalSummaryPdfAsync(JournalSummaryDto summary, AccountingReportHeader header, CancellationToken cancellationToken = default)
    {
        var bytes = BuildReport(header, landscape: true, col =>
        {
            if (summary.JournalTotals.Count == 0)
            {
                EmptyNotice(col, "Aucun mouvement sur la période.");
                return;
            }

            if (summary.Grouping != JournalSummaryGrouping.Totals && summary.Cells.Count > 0)
            {
                var isMonthly = summary.Grouping == JournalSummaryGrouping.Month;
                col.Item().PaddingBottom(2)
                    .Text(isMonthly ? "Détail par mois" : "Détail par compte")
                    .FontSize(11).Bold().FontColor(Colors.Blue.Darken2);

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(60);   // journal
                        c.RelativeColumn(2);    // libellé journal
                        c.ConstantColumn(80);   // mois / compte
                        c.RelativeColumn(2);    // intitulé compte (vide en mensuel)
                        c.ConstantColumn(90);   // débit
                        c.ConstantColumn(90);   // crédit
                    });

                    table.Header(h =>
                    {
                        h.Cell().Element(HeadCell).Text("Journal").Bold();
                        h.Cell().Element(HeadCell).Text("Libellé").Bold();
                        h.Cell().Element(HeadCell).Text(isMonthly ? "Période" : "Compte").Bold();
                        h.Cell().Element(HeadCell).Text(isMonthly ? string.Empty : "Intitulé").Bold();
                        h.Cell().Element(HeadCell).AlignRight().Text("Débit").Bold();
                        h.Cell().Element(HeadCell).AlignRight().Text("Crédit").Bold();
                    });

                    foreach (var cell in summary.Cells)
                    {
                        table.Cell().Element(BodyCell).Text(cell.JournalCode);
                        table.Cell().Element(BodyCell).Text(PdfRenderHelpers.CleanTextForPdf(cell.JournalLabel));
                        table.Cell().Element(BodyCell).Text(isMonthly
                            ? $"{cell.Month:00}/{cell.Year}"
                            : cell.AccountNumber ?? string.Empty);
                        table.Cell().Element(BodyCell).Text(isMonthly
                            ? string.Empty
                            : PdfRenderHelpers.CleanTextForPdf(cell.AccountLabel ?? string.Empty));
                        table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(cell.Debit));
                        table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(cell.Credit));
                    }
                });
            }

            col.Item().PaddingTop(12).PaddingBottom(2)
                .Text("Totaux par journal").FontSize(11).Bold().FontColor(Colors.Blue.Darken2);

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(60);   // journal
                    c.RelativeColumn(3);    // libellé
                    c.ConstantColumn(70);   // écritures
                    c.ConstantColumn(90);   // débit
                    c.ConstantColumn(90);   // crédit
                });

                table.Header(h =>
                {
                    h.Cell().Element(HeadCell).Text("Journal").Bold();
                    h.Cell().Element(HeadCell).Text("Libellé").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Écritures").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Total débit").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Total crédit").Bold();
                });

                foreach (var t in summary.JournalTotals)
                {
                    table.Cell().Element(BodyCell).Text(t.JournalCode);
                    table.Cell().Element(BodyCell).Text(PdfRenderHelpers.CleanTextForPdf(t.JournalLabel));
                    table.Cell().Element(BodyCell).AlignRight().Text(t.EntryCount.ToString(CultureInfo.InvariantCulture));
                    table.Cell().Element(BodyCell).AlignRight().Text(Amount(t.Debit));
                    table.Cell().Element(BodyCell).AlignRight().Text(Amount(t.Credit));
                }

                table.Cell().ColumnSpan(3).Element(TotalCell).AlignRight().Text("TOTAL GÉNÉRAL").Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(summary.TotalDebit)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(summary.TotalCredit)).Bold();
            });

            if (!summary.IsBalanced)
            {
                col.Item().PaddingTop(8)
                    .Text("Contrôle : total débit ≠ total crédit — vérifier les écritures de la période.")
                    .FontSize(9).Bold().FontColor(Colors.Red.Darken2);
            }
        });

        return Task.FromResult(bytes);
    }

    // ── État de rapprochement bancaire ─────────────────────────────────────────────────────

    public Task<byte[]> GenerateBankReconciliationStatementPdfAsync(BankReconciliationStatementDto s, AccountingReportHeader header, CancellationToken cancellationToken = default)
    {
        var bytes = BuildReport(header, landscape: false, col =>
        {
            col.Item().PaddingBottom(6).Text($"{s.BankName} — {s.AccountNumber} (compte {s.ChartOfAccountNumber})")
                .FontSize(10).FontColor(Colors.Grey.Darken2);

            // Deux soldes de départ, côte à côte.
            col.Item().Row(row =>
            {
                row.RelativeItem().Text(t =>
                {
                    t.Span("Solde comptable : ").SemiBold();
                    t.Span(Amount(s.AccountingBalance));
                });
                row.RelativeItem().AlignRight().Text(t =>
                {
                    t.Span("Solde relevé : ").SemiBold();
                    t.Span(Amount(s.StatementClosingBalance));
                });
            });

            void ItemsBlock(string title, IReadOnlyList<BankReconciliationItemDto> items)
            {
                col.Item().PaddingTop(10).PaddingBottom(2).Text(title).FontSize(11).Bold().FontColor(Colors.Blue.Darken2);
                if (items.Count == 0)
                {
                    col.Item().Text("Aucun.").Italic().FontColor(Colors.Grey.Darken1);
                    return;
                }
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(62);   // date
                        c.ConstantColumn(90);   // référence
                        c.RelativeColumn(3);    // libellé
                        c.ConstantColumn(90);   // débit
                        c.ConstantColumn(90);   // crédit
                    });
                    table.Header(h =>
                    {
                        h.Cell().Element(HeadCell).Text("Date").Bold();
                        h.Cell().Element(HeadCell).Text("Référence").Bold();
                        h.Cell().Element(HeadCell).Text("Libellé").Bold();
                        h.Cell().Element(HeadCell).AlignRight().Text("Débit").Bold();
                        h.Cell().Element(HeadCell).AlignRight().Text("Crédit").Bold();
                    });
                    foreach (var i in items)
                    {
                        table.Cell().Element(BodyCell).Text(ShortDate(i.Date));
                        table.Cell().Element(BodyCell).Text(PdfRenderHelpers.CleanTextForPdf(i.Reference));
                        table.Cell().Element(BodyCell).Text(PdfRenderHelpers.CleanTextForPdf(i.Label));
                        table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(i.Debit));
                        table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(i.Credit));
                    }
                });
            }

            ItemsBlock("Écritures non pointées (chèques émis non débités, remises non créditées)", s.UnreconciledBookItems);
            ItemsBlock("Opérations du relevé non comptabilisées (frais, agios)", s.UnreconciledStatementItems);

            // Soldes corrigés et écart.
            col.Item().PaddingTop(14).LineHorizontal(1).LineColor(Colors.Grey.Medium);
            col.Item().PaddingTop(6).Row(row =>
            {
                row.RelativeItem().Text(t =>
                {
                    t.Span("Solde relevé corrigé : ").SemiBold();
                    t.Span(Amount(s.AdjustedStatementBalance));
                });
                row.RelativeItem().AlignRight().Text(t =>
                {
                    t.Span("Solde comptable corrigé : ").SemiBold();
                    t.Span(Amount(s.AdjustedAccountingBalance));
                });
            });
            col.Item().PaddingTop(6).AlignCenter().Text(t =>
            {
                t.Span("Écart : ").Bold().FontSize(12);
                t.Span(Amount(s.Difference)).Bold().FontSize(12)
                    .FontColor(s.IsReconciled ? Colors.Green.Darken2 : Colors.Red.Darken2);
                t.Span(s.IsReconciled ? "  — rapproché" : "  — à justifier").FontSize(10)
                    .FontColor(s.IsReconciled ? Colors.Green.Darken2 : Colors.Red.Darken2);
            });
        });

        return Task.FromResult(bytes);
    }

    // ── Balance détaillée (soldes + détail des mouvements) ─────────────────────────────────

    public Task<byte[]> GenerateDetailedBalancePdfAsync(DetailedBalanceDto balance, AccountingReportHeader header, CancellationToken cancellationToken = default)
    {
        var bytes = BuildReport(header, landscape: true, col =>
        {
            if (balance.Accounts.Count == 0)
            {
                EmptyNotice(col, "Aucun mouvement sur la période.");
                return;
            }

            foreach (var account in balance.Accounts)
            {
                var b = account.Balance;
                col.Item().PaddingTop(10).PaddingBottom(2).Row(row =>
                {
                    row.RelativeItem().Text($"{b.AccountNumber} — {PdfRenderHelpers.CleanTextForPdf(b.Label)}")
                        .FontSize(11).Bold().FontColor(Colors.Blue.Darken2);
                    row.ConstantItem(300).AlignRight()
                        .Text($"Ouverture {Amount(b.OpeningDebit - b.OpeningCredit)}  ·  "
                              + $"Clôture {Amount(b.ClosingDebit - b.ClosingCredit)}")
                        .FontSize(9).FontColor(Colors.Grey.Darken2);
                });

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(62);   // date
                        c.ConstantColumn(50);   // journal
                        c.ConstantColumn(42);   // pièce
                        c.RelativeColumn(3);    // libellé
                        c.ConstantColumn(90);   // débit
                        c.ConstantColumn(90);   // crédit
                    });

                    table.Header(h =>
                    {
                        h.Cell().Element(HeadCell).Text("Date").Bold();
                        h.Cell().Element(HeadCell).Text("Journal").Bold();
                        h.Cell().Element(HeadCell).Text("Pièce").Bold();
                        h.Cell().Element(HeadCell).Text("Libellé").Bold();
                        h.Cell().Element(HeadCell).AlignRight().Text("Débit").Bold();
                        h.Cell().Element(HeadCell).AlignRight().Text("Crédit").Bold();
                    });

                    foreach (var row in account.Rows)
                    {
                        table.Cell().Element(BodyCell).Text(ShortDate(row.EntryDate));
                        table.Cell().Element(BodyCell).Text(row.JournalCode);
                        table.Cell().Element(BodyCell).Text(row.PieceNumber.ToString(CultureInfo.InvariantCulture));
                        table.Cell().Element(BodyCell).Text(PdfRenderHelpers.CleanTextForPdf(row.Label));
                        table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(row.Debit));
                        table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(row.Credit));
                    }

                    table.Cell().ColumnSpan(4).Element(TotalCell).AlignRight().Text($"Mouvements {b.AccountNumber}").Bold();
                    table.Cell().Element(TotalCell).AlignRight().Text(Amount(b.MovementDebit)).Bold();
                    table.Cell().Element(TotalCell).AlignRight().Text(Amount(b.MovementCredit)).Bold();
                });
            }

            col.Item().PaddingTop(12).Row(row =>
            {
                row.RelativeItem().AlignRight().PaddingRight(10).Text("TOTAL GÉNÉRAL").Bold().FontSize(10);
                row.ConstantItem(90).AlignRight().Text(Amount(balance.TotalMovementDebit)).Bold().FontSize(10);
                row.ConstantItem(90).AlignRight().Text(Amount(balance.TotalMovementCredit)).Bold().FontSize(10);
            });
        });

        return Task.FromResult(bytes);
    }

    // ── Balance par période (12 colonnes mensuelles) ───────────────────────────────────────

    public Task<byte[]> GeneratePeriodicBalancePdfAsync(PeriodicBalanceDto balance, AccountingReportHeader header, CancellationToken cancellationToken = default)
    {
        var bytes = BuildReport(header, landscape: true, col =>
        {
            if (balance.Rows.Count == 0)
            {
                EmptyNotice(col, "Aucun mouvement sur l'exercice.");
                return;
            }

            // Douze paires débit/crédit tiendraient mal sur une page : on présente le mouvement net
            // du mois (débit − crédit), l'ouverture et la clôture encadrant les colonnes.
            col.Item().PaddingBottom(4)
                .Text("Mouvement net par mois (débit − crédit)")
                .FontSize(9).Italic().FontColor(Colors.Grey.Darken2);

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(60);           // compte
                    c.RelativeColumn(2);            // libellé
                    c.ConstantColumn(62);           // ouverture
                    for (var m = 0; m < 12; m++)
                        c.ConstantColumn(48);       // mois
                    c.ConstantColumn(62);           // clôture
                });

                table.Header(h =>
                {
                    h.Cell().Element(HeadCell).Text("Compte").Bold();
                    h.Cell().Element(HeadCell).Text("Libellé").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Ouv.").Bold();
                    for (var m = 1; m <= 12; m++)
                        h.Cell().Element(HeadCell).AlignRight().Text($"{m:00}").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Clôture").Bold();
                });

                foreach (var row in balance.Rows)
                {
                    table.Cell().Element(BodyCell).Text(row.AccountNumber);
                    table.Cell().Element(BodyCell).Text(PdfRenderHelpers.CleanTextForPdf(row.Label));
                    table.Cell().Element(BodyCell).AlignRight().Text(Amount(row.Opening));
                    for (var m = 0; m < 12; m++)
                    {
                        var net = row.MonthlyDebit[m] - row.MonthlyCredit[m];
                        table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(net)).FontSize(7);
                    }
                    table.Cell().Element(BodyCell).AlignRight().Text(Amount(row.Closing));
                }
            });

            col.Item().PaddingTop(12).Row(row =>
            {
                row.RelativeItem().AlignRight().PaddingRight(10).Text("TOTAL MOUVEMENTS").Bold().FontSize(10);
                row.ConstantItem(90).AlignRight().Text(Amount(balance.TotalDebit)).Bold().FontSize(10);
                row.ConstantItem(90).AlignRight().Text(Amount(balance.TotalCredit)).Bold().FontSize(10);
            });
        });

        return Task.FromResult(bytes);
    }

    // ── Grand livre général (comptes en séquence) ──────────────────────────────────────────

    public Task<byte[]> GenerateGeneralLedgerPdfAsync(GeneralLedgerDto ledger, AccountingReportHeader header, CancellationToken cancellationToken = default)
    {
        var bytes = BuildReport(header, landscape: true, col =>
        {
            if (ledger.Accounts.Count == 0)
            {
                EmptyNotice(col, "Aucun compte mouvementé sur la période.");
                return;
            }

            foreach (var account in ledger.Accounts)
            {
                col.Item().PaddingTop(10).PaddingBottom(2)
                    .Text($"{account.AccountNumber} — {PdfRenderHelpers.CleanTextForPdf(account.Label)}")
                    .FontSize(11).Bold().FontColor(Colors.Blue.Darken2);

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(62);   // date
                        c.ConstantColumn(50);   // journal
                        c.ConstantColumn(42);   // pièce
                        c.RelativeColumn(3);    // libellé
                        c.ConstantColumn(90);   // débit
                        c.ConstantColumn(90);   // crédit
                        c.ConstantColumn(90);   // solde
                    });

                    table.Header(h =>
                    {
                        h.Cell().Element(HeadCell).Text("Date").Bold();
                        h.Cell().Element(HeadCell).Text("Journal").Bold();
                        h.Cell().Element(HeadCell).Text("Pièce").Bold();
                        h.Cell().Element(HeadCell).Text("Libellé").Bold();
                        h.Cell().Element(HeadCell).AlignRight().Text("Débit").Bold();
                        h.Cell().Element(HeadCell).AlignRight().Text("Crédit").Bold();
                        h.Cell().Element(HeadCell).AlignRight().Text("Solde").Bold();
                    });

                    // Report à nouveau : première ligne de chaque compte, comme sur une édition légale.
                    table.Cell().ColumnSpan(4).Element(BodyCell).Text("Report à nouveau").Italic();
                    table.Cell().Element(BodyCell).AlignRight().Text(string.Empty);
                    table.Cell().Element(BodyCell).AlignRight().Text(string.Empty);
                    table.Cell().Element(BodyCell).AlignRight().Text(Amount(account.OpeningBalance)).Italic();

                    foreach (var row in account.Rows)
                    {
                        table.Cell().Element(BodyCell).Text(ShortDate(row.EntryDate));
                        table.Cell().Element(BodyCell).Text(row.JournalCode);
                        table.Cell().Element(BodyCell).Text(row.PieceNumber.ToString(CultureInfo.InvariantCulture));
                        table.Cell().Element(BodyCell).Text(PdfRenderHelpers.CleanTextForPdf(row.Label));
                        table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(row.Debit));
                        table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(row.Credit));
                        table.Cell().Element(BodyCell).AlignRight().Text(Amount(row.RunningBalance));
                    }

                    table.Cell().ColumnSpan(4).Element(TotalCell).AlignRight().Text($"Total {account.AccountNumber}").Bold();
                    table.Cell().Element(TotalCell).AlignRight().Text(Amount(account.TotalDebit)).Bold();
                    table.Cell().Element(TotalCell).AlignRight().Text(Amount(account.TotalCredit)).Bold();
                    table.Cell().Element(TotalCell).AlignRight().Text(Amount(account.ClosingBalance)).Bold();
                });
            }

            col.Item().PaddingTop(12).Row(row =>
            {
                row.RelativeItem().AlignRight().PaddingRight(10).Text("TOTAL GÉNÉRAL").Bold().FontSize(10);
                row.ConstantItem(90).AlignRight().Text(Amount(ledger.TotalDebit)).Bold().FontSize(10);
                row.ConstantItem(90).AlignRight().Text(Amount(ledger.TotalCredit)).Bold().FontSize(10);
                row.ConstantItem(90).AlignRight().Text(string.Empty);
            });
        });

        return Task.FromResult(bytes);
    }

    // ── Grand livre d'un compte ────────────────────────────────────────────────────────────

    public Task<byte[]> GenerateLedgerPdfAsync(string accountNumber, IReadOnlyList<LedgerRowDto> rows, AccountingReportHeader header, CancellationToken cancellationToken = default)
    {
        var bytes = BuildReport(header, landscape: true, col =>
        {
            if (rows.Count == 0)
            {
                EmptyNotice(col, "Aucun mouvement sur la période.");
                return;
            }

            decimal totalDebit = 0, totalCredit = 0;
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(62);   // date
                    c.ConstantColumn(55);   // journal
                    c.ConstantColumn(42);   // pièce
                    c.RelativeColumn(3);    // libellé
                    c.ConstantColumn(90);   // débit
                    c.ConstantColumn(90);   // crédit
                    c.ConstantColumn(95);   // solde
                });

                table.Header(h =>
                {
                    h.Cell().Element(HeadCell).Text("Date").Bold();
                    h.Cell().Element(HeadCell).Text("Journal").Bold();
                    h.Cell().Element(HeadCell).Text("Pièce").Bold();
                    h.Cell().Element(HeadCell).Text("Libellé").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Débit").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Crédit").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Solde").Bold();
                });

                foreach (var r in rows)
                {
                    table.Cell().Element(BodyCell).Text(ShortDate(r.EntryDate));
                    table.Cell().Element(BodyCell).Text(r.JournalCode);
                    table.Cell().Element(BodyCell).Text(r.PieceNumber.ToString(CultureInfo.InvariantCulture));
                    table.Cell().Element(BodyCell).Text(PdfRenderHelpers.CleanTextForPdf(r.Label));
                    table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(r.Debit));
                    table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(r.Credit));
                    table.Cell().Element(BodyCell).AlignRight().Text(Amount(r.RunningBalance));
                    totalDebit += r.Debit;
                    totalCredit += r.Credit;
                }

                table.Cell().ColumnSpan(4).Element(TotalCell).AlignRight().Text("TOTAUX").Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(totalDebit)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(totalCredit)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(totalDebit - totalCredit)).Bold();
            });
        });

        return Task.FromResult(bytes);
    }

    // ── Balance générale ───────────────────────────────────────────────────────────────────

    public Task<byte[]> GenerateBalancePdfAsync(IReadOnlyList<BalanceRowDto> rows, AccountingReportHeader header, CancellationToken cancellationToken = default)
    {
        var bytes = BuildReport(header, landscape: true, col =>
        {
            if (rows.Count == 0)
            {
                EmptyNotice(col, "Aucun compte mouvementé sur la période.");
                return;
            }

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(70);   // compte
                    c.RelativeColumn(2);    // libellé
                    c.RelativeColumn(1);    // ouv D
                    c.RelativeColumn(1);    // ouv C
                    c.RelativeColumn(1);    // mvt D
                    c.RelativeColumn(1);    // mvt C
                    c.RelativeColumn(1);    // clot D
                    c.RelativeColumn(1);    // clot C
                });

                table.Header(h =>
                {
                    h.Cell().Element(HeadCell).Text("Compte").Bold();
                    h.Cell().Element(HeadCell).Text("Libellé").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Ouv. D").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Ouv. C").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Mvt D").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Mvt C").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Clôt. D").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Clôt. C").Bold();
                });

                decimal od = 0, oc = 0, md = 0, mc = 0, cd = 0, cc = 0;
                foreach (var r in rows)
                {
                    table.Cell().Element(BodyCell).Text(r.AccountNumber);
                    table.Cell().Element(BodyCell).Text(PdfRenderHelpers.CleanTextForPdf(r.Label));
                    table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(r.OpeningDebit));
                    table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(r.OpeningCredit));
                    table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(r.MovementDebit));
                    table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(r.MovementCredit));
                    table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(r.ClosingDebit));
                    table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(r.ClosingCredit));
                    od += r.OpeningDebit; oc += r.OpeningCredit;
                    md += r.MovementDebit; mc += r.MovementCredit;
                    cd += r.ClosingDebit; cc += r.ClosingCredit;
                }

                table.Cell().ColumnSpan(2).Element(TotalCell).AlignRight().Text("TOTAUX").Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(od)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(oc)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(md)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(mc)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(cd)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(cc)).Bold();
            });
        });

        return Task.FromResult(bytes);
    }

    // ── Balance auxiliaire ─────────────────────────────────────────────────────────────────

    public Task<byte[]> GenerateAuxiliaryBalancePdfAsync(IReadOnlyList<AuxiliaryBalanceRowDto> rows, AccountingReportHeader header, CancellationToken cancellationToken = default)
    {
        var bytes = BuildReport(header, landscape: true, col =>
        {
            if (rows.Count == 0)
            {
                EmptyNotice(col, "Aucun tiers mouvementé sur la période.");
                return;
            }

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3);    // tiers
                    c.RelativeColumn(1);    // ouv D
                    c.RelativeColumn(1);    // ouv C
                    c.RelativeColumn(1);    // mvt D
                    c.RelativeColumn(1);    // mvt C
                    c.RelativeColumn(1);    // clot D
                    c.RelativeColumn(1);    // clot C
                });

                table.Header(h =>
                {
                    h.Cell().Element(HeadCell).Text("Tiers").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Ouv. D").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Ouv. C").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Mvt D").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Mvt C").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Clôt. D").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Clôt. C").Bold();
                });

                decimal od = 0, oc = 0, md = 0, mc = 0, cd = 0, cc = 0;
                foreach (var r in rows)
                {
                    table.Cell().Element(BodyCell).Text(PdfRenderHelpers.CleanTextForPdf(r.ThirdPartyName));
                    table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(r.OpeningDebit));
                    table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(r.OpeningCredit));
                    table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(r.MovementDebit));
                    table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(r.MovementCredit));
                    table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(r.ClosingDebit));
                    table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(r.ClosingCredit));
                    od += r.OpeningDebit; oc += r.OpeningCredit;
                    md += r.MovementDebit; mc += r.MovementCredit;
                    cd += r.ClosingDebit; cc += r.ClosingCredit;
                }

                table.Cell().Element(TotalCell).AlignRight().Text("TOTAUX").Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(od)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(oc)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(md)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(mc)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(cd)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(cc)).Bold();
            });
        });

        return Task.FromResult(bytes);
    }

    // ── Grand livre d'un tiers ─────────────────────────────────────────────────────────────

    public Task<byte[]> GenerateThirdPartyLedgerPdfAsync(ThirdPartyLedgerDto ledger, AccountingReportHeader header, CancellationToken cancellationToken = default)
    {
        var bytes = BuildReport(header, landscape: true, col =>
        {
            col.Item().PaddingBottom(2).Text(t =>
            {
                t.Span("Tiers : ").Bold();
                t.Span(PdfRenderHelpers.CleanTextForPdf(ledger.ThirdPartyName));
                t.Span($"    —    Solde d'ouverture : {Amount(ledger.OpeningBalance)}").FontColor(Colors.Grey.Darken2);
            });

            if (ledger.Rows.Count == 0)
            {
                EmptyNotice(col, "Aucun mouvement sur la période.");
                return;
            }

            decimal totalDebit = 0, totalCredit = 0;
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(62);   // date
                    c.ConstantColumn(50);   // journal
                    c.ConstantColumn(40);   // pièce
                    c.ConstantColumn(70);   // compte
                    c.RelativeColumn(3);    // libellé
                    c.ConstantColumn(85);   // débit
                    c.ConstantColumn(85);   // crédit
                    c.ConstantColumn(90);   // solde
                    c.ConstantColumn(50);   // lettrage
                });

                table.Header(h =>
                {
                    h.Cell().Element(HeadCell).Text("Date").Bold();
                    h.Cell().Element(HeadCell).Text("Journal").Bold();
                    h.Cell().Element(HeadCell).Text("Pièce").Bold();
                    h.Cell().Element(HeadCell).Text("Compte").Bold();
                    h.Cell().Element(HeadCell).Text("Libellé").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Débit").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Crédit").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Solde").Bold();
                    h.Cell().Element(HeadCell).Text("Lettr.").Bold();
                });

                foreach (var r in ledger.Rows)
                {
                    table.Cell().Element(BodyCell).Text(ShortDate(r.EntryDate));
                    table.Cell().Element(BodyCell).Text(r.JournalCode);
                    table.Cell().Element(BodyCell).Text(r.PieceNumber.ToString(CultureInfo.InvariantCulture));
                    table.Cell().Element(BodyCell).Text(r.AccountNumber);
                    table.Cell().Element(BodyCell).Text(PdfRenderHelpers.CleanTextForPdf(r.Label));
                    table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(r.Debit));
                    table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(r.Credit));
                    table.Cell().Element(BodyCell).AlignRight().Text(Amount(r.RunningBalance));
                    table.Cell().Element(BodyCell).Text(r.LetteringCode ?? string.Empty);
                    totalDebit += r.Debit;
                    totalCredit += r.Credit;
                }

                table.Cell().ColumnSpan(5).Element(TotalCell).AlignRight().Text("TOTAUX").Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(totalDebit)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(totalCredit)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(ledger.OpeningBalance + totalDebit - totalCredit)).Bold();
                table.Cell().Element(TotalCell).Text(string.Empty);
            });
        });

        return Task.FromResult(bytes);
    }

    // ── Balance âgée ───────────────────────────────────────────────────────────────────────

    public Task<byte[]> GenerateAgingPdfAsync(IReadOnlyList<AgingReportRowDto> rows, AccountingReportHeader header, CancellationToken cancellationToken = default)
    {
        var bytes = BuildReport(header, landscape: true, col =>
        {
            if (rows.Count == 0)
            {
                EmptyNotice(col, "Aucun solde à échéancer.");
                return;
            }

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3);    // tiers
                    c.RelativeColumn(1);    // total
                    c.RelativeColumn(1);    // non échu
                    c.RelativeColumn(1);    // 0-30
                    c.RelativeColumn(1);    // 31-60
                    c.RelativeColumn(1);    // 61-90
                    c.RelativeColumn(1);    // +90
                });

                table.Header(h =>
                {
                    h.Cell().Element(HeadCell).Text("Tiers").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Total").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("Non échu").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("0-30 j").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("31-60 j").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("61-90 j").Bold();
                    h.Cell().Element(HeadCell).AlignRight().Text("+90 j").Bold();
                });

                decimal t = 0, ne = 0, a = 0, b = 0, cc = 0, d = 0;
                foreach (var r in rows)
                {
                    table.Cell().Element(BodyCell).Text(PdfRenderHelpers.CleanTextForPdf(r.ThirdPartyName));
                    table.Cell().Element(BodyCell).AlignRight().Text(Amount(r.Total));
                    table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(r.NotYetDue));
                    table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(r.Days0To30));
                    table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(r.Days31To60));
                    table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(r.Days61To90));
                    table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(r.DaysOver90));
                    t += r.Total; ne += r.NotYetDue; a += r.Days0To30;
                    b += r.Days31To60; cc += r.Days61To90; d += r.DaysOver90;
                }

                table.Cell().Element(TotalCell).AlignRight().Text("TOTAUX").Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(t)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(ne)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(a)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(b)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(cc)).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(Amount(d)).Bold();
            });
        });

        return Task.FromResult(bytes);
    }

    // ── Bilan / Compte de résultat ─────────────────────────────────────────────────────────

    public Task<byte[]> GenerateBalanceSheetPdfAsync(BalanceSheetDto dto, AccountingReportHeader header, CancellationToken cancellationToken = default)
    {
        var bytes = BuildReport(header, landscape: false, col =>
        {
            ComposeStatementSection(col, "ACTIF", dto.Assets, "TOTAL ACTIF", dto.TotalAssets);
            ComposeStatementSection(col, "PASSIF", dto.Liabilities, "TOTAL PASSIF", dto.TotalLiabilities);
            col.Item().PaddingTop(10).Row(row =>
            {
                row.RelativeItem().Text("RÉSULTAT NET DE L'EXERCICE").Bold();
                row.ConstantItem(110).AlignRight().Text(Amount(dto.NetResult)).Bold();
            });
        });

        return Task.FromResult(bytes);
    }

    public Task<byte[]> GenerateIncomeStatementPdfAsync(IncomeStatementDto dto, AccountingReportHeader header, CancellationToken cancellationToken = default)
    {
        var bytes = BuildReport(header, landscape: false, col =>
        {
            ComposeStatementSection(col, "PRODUITS", dto.Revenue, "TOTAL PRODUITS", dto.TotalRevenue);
            ComposeStatementSection(col, "CHARGES", dto.Expenses, "TOTAL CHARGES", dto.TotalExpenses);
            col.Item().PaddingTop(10).Row(row =>
            {
                row.RelativeItem().Text("RÉSULTAT NET DE L'EXERCICE").Bold();
                row.ConstantItem(110).AlignRight().Text(Amount(dto.NetResult)).Bold();
            });
        });

        return Task.FromResult(bytes);
    }

    private static void ComposeStatementSection(ColumnDescriptor col, string title, IReadOnlyList<FinancialStatementLineDto> lines, string totalLabel, decimal total)
    {
        col.Item().PaddingTop(12).PaddingBottom(2).Text(title).FontSize(11).Bold().FontColor(Colors.Blue.Darken2);
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(70);   // compte
                c.RelativeColumn(3);    // libellé
                c.ConstantColumn(100);  // montant
                c.ConstantColumn(100);  // N-1
            });

            table.Header(h =>
            {
                h.Cell().Element(HeadCell).Text("Compte").Bold();
                h.Cell().Element(HeadCell).Text("Libellé").Bold();
                h.Cell().Element(HeadCell).AlignRight().Text("Montant").Bold();
                h.Cell().Element(HeadCell).AlignRight().Text("N-1").Bold();
            });

            foreach (var l in lines)
            {
                table.Cell().Element(BodyCell).Text(l.AccountNumber);
                table.Cell().Element(BodyCell).Text(PdfRenderHelpers.CleanTextForPdf(l.Label));
                table.Cell().Element(BodyCell).AlignRight().Text(Amount(l.Amount));
                table.Cell().Element(BodyCell).AlignRight().Text(l.PreviousYearAmount.HasValue ? Amount(l.PreviousYearAmount.Value) : string.Empty);
            }

            table.Cell().ColumnSpan(2).Element(TotalCell).AlignRight().Text(totalLabel).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Amount(total)).Bold();
            table.Cell().Element(TotalCell).Text(string.Empty);
        });
    }
}
