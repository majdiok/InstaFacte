using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;

namespace FactuTrust.Infrastructure.Services;

public sealed class AccountingExportService : IAccountingExportService
{
    private const char Separator = ';';
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    public byte[] ExportJournalToCsv(IReadOnlyList<JournalEntryDto> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Date;Journal;N°Pièce;Compte;Libellé;Débit;Crédit");

        foreach (var entry in entries)
        {
            foreach (var line in entry.Lines)
            {
                sb.Append(FormatDate(entry.EntryDate)).Append(Separator);
                sb.Append(Escape(entry.JournalCode)).Append(Separator);
                sb.Append(entry.EntryNumber).Append(Separator);
                sb.Append(Escape(line.AccountNumber)).Append(Separator);
                sb.Append(Escape(line.Label)).Append(Separator);
                sb.Append(FormatDecimal(line.Debit)).Append(Separator);
                sb.AppendLine(FormatDecimal(line.Credit));
            }
        }

        return BuildBytes(sb);
    }

    /// <summary>
    /// Récapitulatif de journaux : les colonnes de détail suivent l'axe demandé (mois pour le
    /// centralisateur, compte pour la récapitulation). Les sous-totaux par journal et le total
    /// général closent le fichier — ils sont présents quel que soit l'axe.
    /// </summary>
    public byte[] ExportJournalSummaryToCsv(JournalSummaryDto summary)
    {
        var sb = new StringBuilder();

        if (summary.Grouping == JournalSummaryGrouping.Month)
        {
            sb.AppendLine("Journal;Libellé;Période;Débit;Crédit");
            foreach (var c in summary.Cells)
            {
                sb.Append(Escape(c.JournalCode)).Append(Separator);
                sb.Append(Escape(c.JournalLabel)).Append(Separator);
                sb.Append(Escape($"{c.Month:00}/{c.Year}")).Append(Separator);
                sb.Append(FormatDecimal(c.Debit)).Append(Separator);
                sb.AppendLine(FormatDecimal(c.Credit));
            }
        }
        else if (summary.Grouping == JournalSummaryGrouping.Account)
        {
            sb.AppendLine("Journal;Libellé;Compte;Intitulé;Débit;Crédit");
            foreach (var c in summary.Cells)
            {
                sb.Append(Escape(c.JournalCode)).Append(Separator);
                sb.Append(Escape(c.JournalLabel)).Append(Separator);
                sb.Append(Escape(c.AccountNumber ?? string.Empty)).Append(Separator);
                sb.Append(Escape(c.AccountLabel ?? string.Empty)).Append(Separator);
                sb.Append(FormatDecimal(c.Debit)).Append(Separator);
                sb.AppendLine(FormatDecimal(c.Credit));
            }
        }

        if (sb.Length > 0)
            sb.AppendLine();

        sb.AppendLine("Journal;Libellé;Écritures;Total débit;Total crédit");
        foreach (var totalRow in summary.JournalTotals)
        {
            sb.Append(Escape(totalRow.JournalCode)).Append(Separator);
            sb.Append(Escape(totalRow.JournalLabel)).Append(Separator);
            sb.Append(totalRow.EntryCount).Append(Separator);
            sb.Append(FormatDecimal(totalRow.Debit)).Append(Separator);
            sb.AppendLine(FormatDecimal(totalRow.Credit));
        }

        sb.Append("TOTAL GÉNÉRAL").Append(Separator).Append(Separator).Append(Separator);
        sb.Append(FormatDecimal(summary.TotalDebit)).Append(Separator);
        sb.AppendLine(FormatDecimal(summary.TotalCredit));

        return BuildBytes(sb);
    }

    public byte[] ExportLedgerToCsv(IReadOnlyList<LedgerRowDto> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Date;Journal;N°Pièce;Libellé;Débit;Crédit;Solde");

        foreach (var row in rows)
        {
            sb.Append(FormatDate(row.EntryDate)).Append(Separator);
            sb.Append(Escape(row.JournalCode)).Append(Separator);
            sb.Append(row.PieceNumber).Append(Separator);
            sb.Append(Escape(row.Label)).Append(Separator);
            sb.Append(FormatDecimal(row.Debit)).Append(Separator);
            sb.Append(FormatDecimal(row.Credit)).Append(Separator);
            sb.AppendLine(FormatDecimal(row.RunningBalance));
        }

        return BuildBytes(sb);
    }

    /// <summary>
    /// Grand livre général : les comptes en séquence, chacun précédé de son report à nouveau et
    /// suivi de son sous-total. Une colonne « Compte » rend le fichier exploitable en tableur.
    /// </summary>
    public byte[] ExportGeneralLedgerToCsv(GeneralLedgerDto ledger)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Compte;Intitulé;Date;Journal;N°Pièce;Libellé;Débit;Crédit;Solde");

        foreach (var account in ledger.Accounts)
        {
            sb.Append(Escape(account.AccountNumber)).Append(Separator);
            sb.Append(Escape(account.Label)).Append(Separator);
            sb.Append(Separator).Append(Separator).Append(Separator);
            sb.Append("Report à nouveau").Append(Separator);
            sb.Append(Separator).Append(Separator);
            sb.AppendLine(FormatDecimal(account.OpeningBalance));

            foreach (var row in account.Rows)
            {
                sb.Append(Escape(account.AccountNumber)).Append(Separator);
                sb.Append(Escape(account.Label)).Append(Separator);
                sb.Append(FormatDate(row.EntryDate)).Append(Separator);
                sb.Append(Escape(row.JournalCode)).Append(Separator);
                sb.Append(row.PieceNumber).Append(Separator);
                sb.Append(Escape(row.Label)).Append(Separator);
                sb.Append(FormatDecimal(row.Debit)).Append(Separator);
                sb.Append(FormatDecimal(row.Credit)).Append(Separator);
                sb.AppendLine(FormatDecimal(row.RunningBalance));
            }

            sb.Append(Escape(account.AccountNumber)).Append(Separator);
            sb.Append(Escape(account.Label)).Append(Separator);
            sb.Append(Separator).Append(Separator).Append(Separator);
            sb.Append("Total du compte").Append(Separator);
            sb.Append(FormatDecimal(account.TotalDebit)).Append(Separator);
            sb.Append(FormatDecimal(account.TotalCredit)).Append(Separator);
            sb.AppendLine(FormatDecimal(account.ClosingBalance));
        }

        sb.Append("TOTAL GÉNÉRAL").Append(Separator).Append(Separator).Append(Separator);
        sb.Append(Separator).Append(Separator).Append(Separator);
        sb.Append(FormatDecimal(ledger.TotalDebit)).Append(Separator);
        sb.Append(FormatDecimal(ledger.TotalCredit)).Append(Separator);
        sb.AppendLine();

        return BuildBytes(sb);
    }

    /// <summary>
    /// État de rapprochement bancaire : soldes (comptable / relevé), les deux blocs de suspens
    /// avec une colonne « Bloc », les soldes corrigés et l'écart.
    /// </summary>
    public byte[] ExportBankReconciliationStatementToCsv(BankReconciliationStatementDto s)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"État de rapprochement;{Escape(s.BankName)};{Escape(s.AccountNumber)};compte {Escape(s.ChartOfAccountNumber)}");
        sb.AppendLine($"Période;{FormatDate(s.PeriodStart)};{FormatDate(s.PeriodEnd)}");
        sb.AppendLine();
        sb.AppendLine("Solde comptable;" + FormatDecimal(s.AccountingBalance));
        sb.AppendLine("Solde relevé;" + FormatDecimal(s.StatementClosingBalance));
        sb.AppendLine();

        sb.AppendLine("Bloc;Date;Référence;Libellé;Débit;Crédit");
        foreach (var i in s.UnreconciledBookItems)
        {
            sb.Append("Écriture non pointée").Append(Separator);
            sb.Append(FormatDate(i.Date)).Append(Separator);
            sb.Append(Escape(i.Reference)).Append(Separator);
            sb.Append(Escape(i.Label)).Append(Separator);
            sb.Append(FormatDecimal(i.Debit)).Append(Separator);
            sb.AppendLine(FormatDecimal(i.Credit));
        }
        foreach (var i in s.UnreconciledStatementItems)
        {
            sb.Append("Relevé non comptabilisé").Append(Separator);
            sb.Append(FormatDate(i.Date)).Append(Separator);
            sb.Append(Escape(i.Reference)).Append(Separator);
            sb.Append(Escape(i.Label)).Append(Separator);
            sb.Append(FormatDecimal(i.Debit)).Append(Separator);
            sb.AppendLine(FormatDecimal(i.Credit));
        }

        sb.AppendLine();
        sb.AppendLine("Solde relevé corrigé;" + FormatDecimal(s.AdjustedStatementBalance));
        sb.AppendLine("Solde comptable corrigé;" + FormatDecimal(s.AdjustedAccountingBalance));
        sb.AppendLine("Écart;" + FormatDecimal(s.Difference));

        return BuildBytes(sb);
    }

    public byte[] ExportBalanceToCsv(IReadOnlyList<BalanceRowDto> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Compte;Libellé;Ouverture D;Ouverture C;Mouvement D;Mouvement C;Clôture D;Clôture C");

        foreach (var row in rows)
        {
            sb.Append(Escape(row.AccountNumber)).Append(Separator);
            sb.Append(Escape(row.Label)).Append(Separator);
            sb.Append(FormatDecimal(row.OpeningDebit)).Append(Separator);
            sb.Append(FormatDecimal(row.OpeningCredit)).Append(Separator);
            sb.Append(FormatDecimal(row.MovementDebit)).Append(Separator);
            sb.Append(FormatDecimal(row.MovementCredit)).Append(Separator);
            sb.Append(FormatDecimal(row.ClosingDebit)).Append(Separator);
            sb.AppendLine(FormatDecimal(row.ClosingCredit));
        }

        return BuildBytes(sb);
    }

    /// <summary>
    /// Balance détaillée : chaque compte est introduit par sa ligne de balance (type « B ») puis
    /// suivi de ses mouvements (type « M ») — une colonne « Type » rend le fichier filtrable.
    /// </summary>
    public byte[] ExportDetailedBalanceToCsv(DetailedBalanceDto balance)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Type;Compte;Libellé;Date;Journal;N°Pièce;Débit;Crédit;Solde");

        foreach (var account in balance.Accounts)
        {
            var b = account.Balance;
            sb.Append("B").Append(Separator);
            sb.Append(Escape(b.AccountNumber)).Append(Separator);
            sb.Append(Escape(b.Label)).Append(Separator);
            sb.Append(Separator).Append(Separator).Append(Separator);
            sb.Append(FormatDecimal(b.MovementDebit)).Append(Separator);
            sb.Append(FormatDecimal(b.MovementCredit)).Append(Separator);
            sb.AppendLine(FormatDecimal(b.ClosingDebit - b.ClosingCredit));

            foreach (var row in account.Rows)
            {
                sb.Append("M").Append(Separator);
                sb.Append(Escape(b.AccountNumber)).Append(Separator);
                sb.Append(Escape(row.Label)).Append(Separator);
                sb.Append(FormatDate(row.EntryDate)).Append(Separator);
                sb.Append(Escape(row.JournalCode)).Append(Separator);
                sb.Append(row.PieceNumber).Append(Separator);
                sb.Append(FormatDecimal(row.Debit)).Append(Separator);
                sb.Append(FormatDecimal(row.Credit)).Append(Separator);
                sb.AppendLine(FormatDecimal(row.RunningBalance));
            }
        }

        sb.Append("TOTAL GÉNÉRAL").Append(Separator).Append(Separator).Append(Separator);
        sb.Append(Separator).Append(Separator);
        sb.Append(FormatDecimal(balance.TotalMovementDebit)).Append(Separator);
        sb.Append(FormatDecimal(balance.TotalMovementCredit)).Append(Separator);
        sb.AppendLine();

        return BuildBytes(sb);
    }

    /// <summary>Balance par période : une colonne de débit et une de crédit par mois de l'exercice.</summary>
    public byte[] ExportPeriodicBalanceToCsv(PeriodicBalanceDto balance)
    {
        var sb = new StringBuilder();
        sb.Append("Compte;Libellé;Ouverture");
        for (var m = 1; m <= 12; m++)
            sb.Append(Separator).Append($"{m:00} D").Append(Separator).Append($"{m:00} C");
        sb.Append(Separator).AppendLine("Clôture");

        foreach (var row in balance.Rows)
        {
            sb.Append(Escape(row.AccountNumber)).Append(Separator);
            sb.Append(Escape(row.Label)).Append(Separator);
            sb.Append(FormatDecimal(row.Opening));
            for (var m = 0; m < 12; m++)
            {
                sb.Append(Separator).Append(FormatDecimal(row.MonthlyDebit[m]));
                sb.Append(Separator).Append(FormatDecimal(row.MonthlyCredit[m]));
            }
            sb.Append(Separator).AppendLine(FormatDecimal(row.Closing));
        }

        return BuildBytes(sb);
    }

    public byte[] ExportAuxiliaryBalanceToCsv(IReadOnlyList<AuxiliaryBalanceRowDto> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Tiers;Ouverture D;Ouverture C;Mouvement D;Mouvement C;Clôture D;Clôture C");

        foreach (var row in rows)
        {
            sb.Append(Escape(row.ThirdPartyName)).Append(Separator);
            sb.Append(FormatDecimal(row.OpeningDebit)).Append(Separator);
            sb.Append(FormatDecimal(row.OpeningCredit)).Append(Separator);
            sb.Append(FormatDecimal(row.MovementDebit)).Append(Separator);
            sb.Append(FormatDecimal(row.MovementCredit)).Append(Separator);
            sb.Append(FormatDecimal(row.ClosingDebit)).Append(Separator);
            sb.AppendLine(FormatDecimal(row.ClosingCredit));
        }

        return BuildBytes(sb);
    }

    public byte[] ExportThirdPartyLedgerToCsv(ThirdPartyLedgerDto ledger)
    {
        var sb = new StringBuilder();
        sb.Append("Tiers;").AppendLine(Escape(ledger.ThirdPartyName));
        sb.Append("Solde d'ouverture;").AppendLine(FormatDecimal(ledger.OpeningBalance));
        sb.AppendLine("Date;Journal;N°Pièce;Réf. pièce;Compte;Libellé;Débit;Crédit;Solde;Lettrage");

        foreach (var row in ledger.Rows)
        {
            sb.Append(FormatDate(row.EntryDate)).Append(Separator);
            sb.Append(Escape(row.JournalCode)).Append(Separator);
            sb.Append(row.PieceNumber).Append(Separator);
            sb.Append(Escape(row.PieceRef ?? string.Empty)).Append(Separator);
            sb.Append(Escape(row.AccountNumber)).Append(Separator);
            sb.Append(Escape(row.Label)).Append(Separator);
            sb.Append(FormatDecimal(row.Debit)).Append(Separator);
            sb.Append(FormatDecimal(row.Credit)).Append(Separator);
            sb.Append(FormatDecimal(row.RunningBalance)).Append(Separator);
            sb.AppendLine(Escape(row.LetteringCode ?? string.Empty));
        }

        return BuildBytes(sb);
    }

    private static string FormatDate(DateTime date) =>
        date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    // Le dinar tunisien s'exprime en millimes (3 décimales). L'export tabulaire (Excel) et l'écran
    // utilisent 3 décimales ; le CSV doit être cohérent pour ne pas tronquer les millimes.
    private static string FormatDecimal(decimal value) =>
        value.ToString("0.000", CultureInfo.InvariantCulture);

    public byte[] ExportBudgetReportToCsv(BudgetReportDto report)
    {
        var sb = new StringBuilder();
        sb.Append("Exercice;").AppendLine(report.FiscalYear.ToString(CultureInfo.InvariantCulture));
        sb.Append("Cumul jusqu'au mois;").AppendLine(report.ThroughMonth.ToString(CultureInfo.InvariantCulture));
        sb.Append("Budget initial validé;").AppendLine(report.IsValidated ? "Oui" : "Non");
        sb.Append("Code;Poste;Sens;Budget initial;Budget révisé;Réalisé;Écart;%");
        for (var m = 1; m <= 12; m++)
            sb.Append(Separator).Append("Réalisé M").Append(m.ToString("00", CultureInfo.InvariantCulture));
        sb.AppendLine();

        foreach (var row in report.Rows)
        {
            sb.Append(Escape(row.Code)).Append(Separator);
            sb.Append(Escape(row.Label)).Append(Separator);
            sb.Append(row.Kind == 0 ? "Charges" : "Produits").Append(Separator);
            sb.Append(FormatDecimal(row.PeriodInitial)).Append(Separator);
            sb.Append(FormatDecimal(row.PeriodRevised)).Append(Separator);
            sb.Append(FormatDecimal(row.PeriodActual)).Append(Separator);
            sb.Append(FormatDecimal(row.Variance)).Append(Separator);
            sb.Append(row.ConsumptionPercent.HasValue ? FormatDecimal(row.ConsumptionPercent.Value) : string.Empty);
            for (var m = 0; m < 12; m++)
                sb.Append(Separator).Append(FormatDecimal(row.MonthlyActual[m]));
            sb.AppendLine();
        }

        return BuildBytes(sb);
    }

    public byte[] ExportBudgetReportToExcel(BudgetReportDto report)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("État budgétaire");

        ws.Cell(1, 1).Value = $"État budgétaire {report.FiscalYear} — cumul jusqu'au mois {report.ThroughMonth}" +
                              (report.IsValidated ? string.Empty : " (budget initial non validé)");
        ws.Cell(1, 1).Style.Font.Bold = true;

        var headers = new List<string> { "Code", "Poste", "Sens", "Budget initial", "Budget révisé", "Réalisé", "Écart", "%" };
        for (var m = 1; m <= 12; m++)
            headers.Add($"Réalisé M{m:00}");
        for (var c = 0; c < headers.Count; c++)
            ws.Cell(3, c + 1).Value = headers[c];
        StyleHeaderRow(ws, headers.Count, headerRow: 3);

        var row = 4;
        foreach (var r in report.Rows)
        {
            ws.Cell(row, 1).Value = r.Code;
            ws.Cell(row, 2).Value = r.Label;
            ws.Cell(row, 3).Value = r.Kind == 0 ? "Charges" : "Produits";
            ws.Cell(row, 4).Value = r.PeriodInitial;
            ws.Cell(row, 5).Value = r.PeriodRevised;
            ws.Cell(row, 6).Value = r.PeriodActual;
            ws.Cell(row, 7).Value = r.Variance;
            if (r.ConsumptionPercent.HasValue)
                ws.Cell(row, 8).Value = r.ConsumptionPercent.Value;
            for (var m = 0; m < 12; m++)
                ws.Cell(row, 9 + m).Value = r.MonthlyActual[m];
            for (var c = 4; c <= 8 + 12; c++)
                ws.Cell(row, c).Style.NumberFormat.Format = "#,##0.000";
            if (r.IsOffPost)
                ws.Range(row, 1, row, headers.Count).Style.Font.Italic = true;
            row++;
        }

        // Totaux charges / produits
        ws.Cell(row, 1).Value = "TOTAL CHARGES";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 4).Value = report.Totals.ExpenseInitial;
        ws.Cell(row, 5).Value = report.Totals.ExpenseRevised;
        ws.Cell(row, 6).Value = report.Totals.ExpenseActual;
        row++;
        ws.Cell(row, 1).Value = "TOTAL PRODUITS";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 4).Value = report.Totals.RevenueInitial;
        ws.Cell(row, 5).Value = report.Totals.RevenueRevised;
        ws.Cell(row, 6).Value = report.Totals.RevenueActual;
        for (var r2 = row - 1; r2 <= row; r2++)
            for (var c = 4; c <= 6; c++)
            {
                ws.Cell(r2, c).Style.Font.Bold = true;
                ws.Cell(r2, c).Style.NumberFormat.Format = "#,##0.000";
            }

        ws.Columns().AdjustToContents();
        return WorkbookToBytes(wb);
    }

    private static string Escape(string field)
    {
        if (field.Contains(Separator) || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            return $"\"{field.Replace("\"", "\"\"")}\"";
        return field;
    }

    public byte[] ExportJournalToExcel(IReadOnlyList<JournalEntryDto> entries)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Journal");

        var headers = new[] { "Date", "Journal", "N\u00b0Pi\u00e8ce", "Compte", "Libell\u00e9", "D\u00e9bit", "Cr\u00e9dit" };
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];
        StyleHeaderRow(ws, headers.Length);

        var row = 2;
        foreach (var entry in entries)
        {
            foreach (var line in entry.Lines)
            {
                ws.Cell(row, 1).Value = entry.EntryDate;
                ws.Cell(row, 1).Style.NumberFormat.Format = "dd/MM/yyyy";
                ws.Cell(row, 2).Value = entry.JournalCode;
                ws.Cell(row, 3).Value = entry.EntryNumber;
                ws.Cell(row, 4).Value = line.AccountNumber;
                ws.Cell(row, 5).Value = line.Label;
                ws.Cell(row, 6).Value = line.Debit;
                ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.000";
                ws.Cell(row, 7).Value = line.Credit;
                ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0.000";
                row++;
            }
        }

        ws.Columns().AdjustToContents();
        return WorkbookToBytes(wb);
    }

    public byte[] ExportLedgerToExcel(IReadOnlyList<LedgerRowDto> rows, string accountNumber)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add($"Grand Livre {accountNumber}");

        var headers = new[] { "Date", "Journal", "N\u00b0Pi\u00e8ce", "Libell\u00e9", "D\u00e9bit", "Cr\u00e9dit", "Solde" };
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];
        StyleHeaderRow(ws, headers.Length);

        var row = 2;
        foreach (var r in rows)
        {
            ws.Cell(row, 1).Value = r.EntryDate;
            ws.Cell(row, 1).Style.NumberFormat.Format = "dd/MM/yyyy";
            ws.Cell(row, 2).Value = r.JournalCode;
            ws.Cell(row, 3).Value = r.PieceNumber;
            ws.Cell(row, 4).Value = r.Label;
            ws.Cell(row, 5).Value = r.Debit;
            ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.000";
            ws.Cell(row, 6).Value = r.Credit;
            ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.000";
            ws.Cell(row, 7).Value = r.RunningBalance;
            ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0.000";
            row++;
        }

        ws.Columns().AdjustToContents();
        return WorkbookToBytes(wb);
    }

    /// <summary>
    /// État de rapprochement bancaire : bloc de synthèse (soldes, corrigés, écart) et une feuille
    /// listant les suspens des deux côtés.
    /// </summary>
    public byte[] ExportBankReconciliationStatementToExcel(BankReconciliationStatementDto s)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("État de rapprochement");

        ws.Cell(1, 1).Value = "Banque";
        ws.Cell(1, 2).Value = $"{s.BankName} — {s.AccountNumber} (compte {s.ChartOfAccountNumber})";
        ws.Cell(2, 1).Value = "Période";
        ws.Cell(2, 2).Value = $"{s.PeriodStart:dd/MM/yyyy} → {s.PeriodEnd:dd/MM/yyyy}";
        ws.Cell(3, 1).Value = "Solde comptable";
        ws.Cell(3, 2).Value = s.AccountingBalance;
        ws.Cell(4, 1).Value = "Solde relevé";
        ws.Cell(4, 2).Value = s.StatementClosingBalance;
        for (var r = 3; r <= 4; r++)
            ws.Cell(r, 2).Style.NumberFormat.Format = "#,##0.000";

        var headers = new[] { "Bloc", "Date", "Référence", "Libellé", "Débit", "Crédit" };
        var row = 6;
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(row, c + 1).Value = headers[c];
        StyleHeaderRow(ws, headers.Length, row);
        row++;

        void WriteItems(string bloc, IReadOnlyList<BankReconciliationItemDto> items)
        {
            foreach (var i in items)
            {
                ws.Cell(row, 1).Value = bloc;
                ws.Cell(row, 2).Value = i.Date;
                ws.Cell(row, 2).Style.NumberFormat.Format = "dd/MM/yyyy";
                ws.Cell(row, 3).Value = i.Reference;
                ws.Cell(row, 4).Value = i.Label;
                ws.Cell(row, 5).Value = i.Debit;
                ws.Cell(row, 6).Value = i.Credit;
                for (var c = 5; c <= 6; c++)
                    ws.Cell(row, c).Style.NumberFormat.Format = "#,##0.000";
                row++;
            }
        }

        WriteItems("Écriture non pointée", s.UnreconciledBookItems);
        WriteItems("Relevé non comptabilisé", s.UnreconciledStatementItems);

        row++;
        ws.Cell(row, 1).Value = "Solde relevé corrigé";
        ws.Cell(row, 2).Value = s.AdjustedStatementBalance;
        ws.Cell(row + 1, 1).Value = "Solde comptable corrigé";
        ws.Cell(row + 1, 2).Value = s.AdjustedAccountingBalance;
        ws.Cell(row + 2, 1).Value = "Écart";
        ws.Cell(row + 2, 2).Value = s.Difference;
        for (var r = row; r <= row + 2; r++)
        {
            ws.Cell(r, 1).Style.Font.Bold = true;
            ws.Cell(r, 2).Style.NumberFormat.Format = "#,##0.000";
        }

        ws.Columns().AdjustToContents();
        return WorkbookToBytes(wb);
    }

    /// <summary>
    /// Récapitulatif de journaux : une feuille de détail selon l'axe (absente en mode « Totaux »)
    /// et une feuille de sous-totaux par journal close par le total général.
    /// </summary>
    public byte[] ExportJournalSummaryToExcel(JournalSummaryDto summary)
    {
        using var wb = new XLWorkbook();

        if (summary.Grouping == JournalSummaryGrouping.Month)
        {
            var ws = wb.Worksheets.Add("Centralisateur");
            var headers = new[] { "Journal", "Libellé", "Période", "Débit", "Crédit" };
            for (var c = 0; c < headers.Length; c++)
                ws.Cell(1, c + 1).Value = headers[c];
            StyleHeaderRow(ws, headers.Length);

            var row = 2;
            foreach (var cell in summary.Cells)
            {
                ws.Cell(row, 1).Value = cell.JournalCode;
                ws.Cell(row, 2).Value = cell.JournalLabel;
                ws.Cell(row, 3).Value = $"{cell.Month:00}/{cell.Year}";
                ws.Cell(row, 4).Value = cell.Debit;
                ws.Cell(row, 5).Value = cell.Credit;
                for (var c = 4; c <= 5; c++)
                    ws.Cell(row, c).Style.NumberFormat.Format = "#,##0.000";
                row++;
            }

            ws.Columns().AdjustToContents();
        }
        else if (summary.Grouping == JournalSummaryGrouping.Account)
        {
            var ws = wb.Worksheets.Add("Récapitulation");
            var headers = new[] { "Journal", "Libellé", "Compte", "Intitulé", "Débit", "Crédit" };
            for (var c = 0; c < headers.Length; c++)
                ws.Cell(1, c + 1).Value = headers[c];
            StyleHeaderRow(ws, headers.Length);

            var row = 2;
            foreach (var cell in summary.Cells)
            {
                ws.Cell(row, 1).Value = cell.JournalCode;
                ws.Cell(row, 2).Value = cell.JournalLabel;
                ws.Cell(row, 3).Value = cell.AccountNumber ?? string.Empty;
                ws.Cell(row, 4).Value = cell.AccountLabel ?? string.Empty;
                ws.Cell(row, 5).Value = cell.Debit;
                ws.Cell(row, 6).Value = cell.Credit;
                for (var c = 5; c <= 6; c++)
                    ws.Cell(row, c).Style.NumberFormat.Format = "#,##0.000";
                row++;
            }

            ws.Columns().AdjustToContents();
        }

        var totalsSheet = wb.Worksheets.Add("Totaux journaux");
        var totalHeaders = new[] { "Journal", "Libellé", "Écritures", "Total débit", "Total crédit" };
        for (var c = 0; c < totalHeaders.Length; c++)
            totalsSheet.Cell(1, c + 1).Value = totalHeaders[c];
        StyleHeaderRow(totalsSheet, totalHeaders.Length);

        var totalRow = 2;
        foreach (var t in summary.JournalTotals)
        {
            totalsSheet.Cell(totalRow, 1).Value = t.JournalCode;
            totalsSheet.Cell(totalRow, 2).Value = t.JournalLabel;
            totalsSheet.Cell(totalRow, 3).Value = t.EntryCount;
            totalsSheet.Cell(totalRow, 4).Value = t.Debit;
            totalsSheet.Cell(totalRow, 5).Value = t.Credit;
            for (var c = 4; c <= 5; c++)
                totalsSheet.Cell(totalRow, c).Style.NumberFormat.Format = "#,##0.000";
            totalRow++;
        }

        totalsSheet.Cell(totalRow, 1).Value = "TOTAL GÉNÉRAL";
        totalsSheet.Cell(totalRow, 1).Style.Font.Bold = true;
        totalsSheet.Cell(totalRow, 4).Value = summary.TotalDebit;
        totalsSheet.Cell(totalRow, 5).Value = summary.TotalCredit;
        for (var c = 4; c <= 5; c++)
        {
            totalsSheet.Cell(totalRow, c).Style.Font.Bold = true;
            totalsSheet.Cell(totalRow, c).Style.NumberFormat.Format = "#,##0.000";
        }

        totalsSheet.Columns().AdjustToContents();
        return WorkbookToBytes(wb);
    }

    /// <summary>Grand livre général : une feuille unique, comptes en séquence avec report et sous-total.</summary>
    public byte[] ExportGeneralLedgerToExcel(GeneralLedgerDto ledger)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Grand livre");

        var headers = new[] { "Compte", "Intitulé", "Date", "Journal", "N°Pièce", "Libellé", "Débit", "Crédit", "Solde" };
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];
        StyleHeaderRow(ws, headers.Length);

        var row = 2;
        foreach (var account in ledger.Accounts)
        {
            ws.Cell(row, 1).Value = account.AccountNumber;
            ws.Cell(row, 2).Value = account.Label;
            ws.Cell(row, 6).Value = "Report à nouveau";
            ws.Cell(row, 9).Value = account.OpeningBalance;
            ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0.000";
            ws.Row(row).Style.Font.Italic = true;
            row++;

            foreach (var r in account.Rows)
            {
                ws.Cell(row, 1).Value = account.AccountNumber;
                ws.Cell(row, 2).Value = account.Label;
                ws.Cell(row, 3).Value = r.EntryDate;
                ws.Cell(row, 3).Style.DateFormat.Format = "dd/MM/yyyy";
                ws.Cell(row, 4).Value = r.JournalCode;
                ws.Cell(row, 5).Value = r.PieceNumber;
                ws.Cell(row, 6).Value = r.Label;
                ws.Cell(row, 7).Value = r.Debit;
                ws.Cell(row, 8).Value = r.Credit;
                ws.Cell(row, 9).Value = r.RunningBalance;
                for (var c = 7; c <= 9; c++)
                    ws.Cell(row, c).Style.NumberFormat.Format = "#,##0.000";
                row++;
            }

            ws.Cell(row, 1).Value = account.AccountNumber;
            ws.Cell(row, 6).Value = "Total du compte";
            ws.Cell(row, 7).Value = account.TotalDebit;
            ws.Cell(row, 8).Value = account.TotalCredit;
            ws.Cell(row, 9).Value = account.ClosingBalance;
            for (var c = 7; c <= 9; c++)
                ws.Cell(row, c).Style.NumberFormat.Format = "#,##0.000";
            ws.Row(row).Style.Font.Bold = true;
            row++;
        }

        ws.Cell(row, 1).Value = "TOTAL GÉNÉRAL";
        ws.Cell(row, 7).Value = ledger.TotalDebit;
        ws.Cell(row, 8).Value = ledger.TotalCredit;
        for (var c = 7; c <= 8; c++)
            ws.Cell(row, c).Style.NumberFormat.Format = "#,##0.000";
        ws.Row(row).Style.Font.Bold = true;

        ws.Columns().AdjustToContents();
        return WorkbookToBytes(wb);
    }

    public byte[] ExportBalanceToExcel(IReadOnlyList<BalanceRowDto> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Balance");

        var headers = new[] { "Compte", "Libell\u00e9", "Ouverture D", "Ouverture C", "Mouvement D", "Mouvement C", "Cl\u00f4ture D", "Cl\u00f4ture C" };
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];
        StyleHeaderRow(ws, headers.Length);

        var row = 2;
        foreach (var r in rows)
        {
            ws.Cell(row, 1).Value = r.AccountNumber;
            ws.Cell(row, 2).Value = r.Label;
            ws.Cell(row, 3).Value = r.OpeningDebit;
            ws.Cell(row, 4).Value = r.OpeningCredit;
            ws.Cell(row, 5).Value = r.MovementDebit;
            ws.Cell(row, 6).Value = r.MovementCredit;
            ws.Cell(row, 7).Value = r.ClosingDebit;
            ws.Cell(row, 8).Value = r.ClosingCredit;
            for (var c = 3; c <= 8; c++)
                ws.Cell(row, c).Style.NumberFormat.Format = "#,##0.000";
            row++;
        }

        // Totals row
        ws.Cell(row, 1).Value = "TOTAUX";
        ws.Cell(row, 1).Style.Font.Bold = true;
        for (var c = 3; c <= 8; c++)
        {
            ws.Cell(row, c).FormulaA1 = $"SUM({ws.Cell(2, c).Address}:{ws.Cell(row - 1, c).Address})";
            ws.Cell(row, c).Style.Font.Bold = true;
            ws.Cell(row, c).Style.NumberFormat.Format = "#,##0.000";
        }

        ws.Columns().AdjustToContents();
        return WorkbookToBytes(wb);
    }

    /// <summary>Balance détaillée : ligne de balance en gras, mouvements en retrait.</summary>
    public byte[] ExportDetailedBalanceToExcel(DetailedBalanceDto balance)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Balance détaillée");

        var headers = new[] { "Type", "Compte", "Libellé", "Date", "Journal", "N°Pièce", "Débit", "Crédit", "Solde" };
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];
        StyleHeaderRow(ws, headers.Length);

        var row = 2;
        foreach (var account in balance.Accounts)
        {
            var b = account.Balance;
            ws.Cell(row, 1).Value = "B";
            ws.Cell(row, 2).Value = b.AccountNumber;
            ws.Cell(row, 3).Value = b.Label;
            ws.Cell(row, 7).Value = b.MovementDebit;
            ws.Cell(row, 8).Value = b.MovementCredit;
            ws.Cell(row, 9).Value = b.ClosingDebit - b.ClosingCredit;
            for (var c = 7; c <= 9; c++)
                ws.Cell(row, c).Style.NumberFormat.Format = "#,##0.000";
            ws.Row(row).Style.Font.Bold = true;
            row++;

            foreach (var r in account.Rows)
            {
                ws.Cell(row, 1).Value = "M";
                ws.Cell(row, 2).Value = b.AccountNumber;
                ws.Cell(row, 3).Value = r.Label;
                ws.Cell(row, 4).Value = r.EntryDate;
                ws.Cell(row, 4).Style.DateFormat.Format = "dd/MM/yyyy";
                ws.Cell(row, 5).Value = r.JournalCode;
                ws.Cell(row, 6).Value = r.PieceNumber;
                ws.Cell(row, 7).Value = r.Debit;
                ws.Cell(row, 8).Value = r.Credit;
                ws.Cell(row, 9).Value = r.RunningBalance;
                for (var c = 7; c <= 9; c++)
                    ws.Cell(row, c).Style.NumberFormat.Format = "#,##0.000";
                row++;
            }
        }

        ws.Cell(row, 1).Value = "TOTAL GÉNÉRAL";
        ws.Cell(row, 7).Value = balance.TotalMovementDebit;
        ws.Cell(row, 8).Value = balance.TotalMovementCredit;
        for (var c = 7; c <= 8; c++)
            ws.Cell(row, c).Style.NumberFormat.Format = "#,##0.000";
        ws.Row(row).Style.Font.Bold = true;

        ws.Columns().AdjustToContents();
        return WorkbookToBytes(wb);
    }

    /// <summary>Balance par période : 12 paires de colonnes mensuelles encadrées par les soldes.</summary>
    public byte[] ExportPeriodicBalanceToExcel(PeriodicBalanceDto balance)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add($"Balance {balance.FiscalYear}");

        ws.Cell(1, 1).Value = "Compte";
        ws.Cell(1, 2).Value = "Libellé";
        ws.Cell(1, 3).Value = "Ouverture";
        var column = 4;
        for (var m = 1; m <= 12; m++)
        {
            ws.Cell(1, column++).Value = $"{m:00} D";
            ws.Cell(1, column++).Value = $"{m:00} C";
        }
        ws.Cell(1, column).Value = "Clôture";
        StyleHeaderRow(ws, column);

        var row = 2;
        foreach (var r in balance.Rows)
        {
            ws.Cell(row, 1).Value = r.AccountNumber;
            ws.Cell(row, 2).Value = r.Label;
            ws.Cell(row, 3).Value = r.Opening;
            column = 4;
            for (var m = 0; m < 12; m++)
            {
                ws.Cell(row, column++).Value = r.MonthlyDebit[m];
                ws.Cell(row, column++).Value = r.MonthlyCredit[m];
            }
            ws.Cell(row, column).Value = r.Closing;
            for (var c = 3; c <= column; c++)
                ws.Cell(row, c).Style.NumberFormat.Format = "#,##0.000";
            row++;
        }

        ws.SheetView.FreezeRows(1);
        ws.SheetView.FreezeColumns(2);
        ws.Columns().AdjustToContents();
        return WorkbookToBytes(wb);
    }

    public byte[] ExportAuxiliaryBalanceToExcel(IReadOnlyList<AuxiliaryBalanceRowDto> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Balance auxiliaire");

        var headers = new[] { "Tiers", "Ouverture D", "Ouverture C", "Mouvement D", "Mouvement C", "Clôture D", "Clôture C" };
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];
        StyleHeaderRow(ws, headers.Length);

        var row = 2;
        foreach (var r in rows)
        {
            ws.Cell(row, 1).Value = r.ThirdPartyName;
            ws.Cell(row, 2).Value = r.OpeningDebit;
            ws.Cell(row, 3).Value = r.OpeningCredit;
            ws.Cell(row, 4).Value = r.MovementDebit;
            ws.Cell(row, 5).Value = r.MovementCredit;
            ws.Cell(row, 6).Value = r.ClosingDebit;
            ws.Cell(row, 7).Value = r.ClosingCredit;
            for (var c = 2; c <= 7; c++)
                ws.Cell(row, c).Style.NumberFormat.Format = "#,##0.000";
            row++;
        }

        ws.Cell(row, 1).Value = "TOTAUX";
        ws.Cell(row, 1).Style.Font.Bold = true;
        for (var c = 2; c <= 7; c++)
        {
            ws.Cell(row, c).FormulaA1 = $"SUM({ws.Cell(2, c).Address}:{ws.Cell(row - 1, c).Address})";
            ws.Cell(row, c).Style.Font.Bold = true;
            ws.Cell(row, c).Style.NumberFormat.Format = "#,##0.000";
        }

        ws.Columns().AdjustToContents();
        return WorkbookToBytes(wb);
    }

    public byte[] ExportThirdPartyLedgerToExcel(ThirdPartyLedgerDto ledger)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Grand livre tiers");

        ws.Cell(1, 1).Value = "Tiers";
        ws.Cell(1, 2).Value = ledger.ThirdPartyName;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Value = "Solde d'ouverture";
        ws.Cell(2, 2).Value = ledger.OpeningBalance;
        ws.Cell(2, 2).Style.NumberFormat.Format = "#,##0.000";
        ws.Cell(2, 1).Style.Font.Bold = true;

        var headers = new[] { "Date", "Journal", "N°Pièce", "Réf. pièce", "Compte", "Libellé", "Débit", "Crédit", "Solde", "Lettrage" };
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(4, c + 1).Value = headers[c];
        StyleHeaderRow(ws, headers.Length, headerRow: 4);

        var row = 5;
        foreach (var r in ledger.Rows)
        {
            ws.Cell(row, 1).Value = r.EntryDate;
            ws.Cell(row, 1).Style.NumberFormat.Format = "dd/MM/yyyy";
            ws.Cell(row, 2).Value = r.JournalCode;
            ws.Cell(row, 3).Value = r.PieceNumber;
            ws.Cell(row, 4).Value = r.PieceRef ?? string.Empty;
            ws.Cell(row, 5).Value = r.AccountNumber;
            ws.Cell(row, 6).Value = r.Label;
            ws.Cell(row, 7).Value = r.Debit;
            ws.Cell(row, 8).Value = r.Credit;
            ws.Cell(row, 9).Value = r.RunningBalance;
            ws.Cell(row, 10).Value = r.LetteringCode ?? string.Empty;
            for (var c = 7; c <= 9; c++)
                ws.Cell(row, c).Style.NumberFormat.Format = "#,##0.000";
            row++;
        }

        ws.Columns().AdjustToContents();
        return WorkbookToBytes(wb);
    }

    public byte[] ExportAgingToCsv(IReadOnlyList<AgingReportRowDto> rows, string kindLabel)
    {
        var sb = new StringBuilder();
        sb.Append("Balance âgée;").AppendLine(Escape(kindLabel));
        sb.AppendLine("Tiers;Total;Non échu;0-30 j;31-60 j;61-90 j;+90 j");

        foreach (var r in rows)
        {
            sb.Append(Escape(r.ThirdPartyName)).Append(Separator);
            sb.Append(FormatDecimal(r.Total)).Append(Separator);
            sb.Append(FormatDecimal(r.NotYetDue)).Append(Separator);
            sb.Append(FormatDecimal(r.Days0To30)).Append(Separator);
            sb.Append(FormatDecimal(r.Days31To60)).Append(Separator);
            sb.Append(FormatDecimal(r.Days61To90)).Append(Separator);
            sb.AppendLine(FormatDecimal(r.DaysOver90));
        }

        return BuildBytes(sb);
    }

    public byte[] ExportAgingToExcel(IReadOnlyList<AgingReportRowDto> rows, string kindLabel)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Balance âgée");

        ws.Cell(1, 1).Value = $"Balance âgée — {kindLabel}";
        ws.Cell(1, 1).Style.Font.Bold = true;

        var headers = new[] { "Tiers", "Total", "Non échu", "0-30 j", "31-60 j", "61-90 j", "+90 j" };
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(3, c + 1).Value = headers[c];
        StyleHeaderRow(ws, headers.Length, headerRow: 3);

        var row = 4;
        foreach (var r in rows)
        {
            ws.Cell(row, 1).Value = r.ThirdPartyName;
            ws.Cell(row, 2).Value = r.Total;
            ws.Cell(row, 3).Value = r.NotYetDue;
            ws.Cell(row, 4).Value = r.Days0To30;
            ws.Cell(row, 5).Value = r.Days31To60;
            ws.Cell(row, 6).Value = r.Days61To90;
            ws.Cell(row, 7).Value = r.DaysOver90;
            for (var c = 2; c <= 7; c++)
                ws.Cell(row, c).Style.NumberFormat.Format = "#,##0.000";
            row++;
        }

        ws.Cell(row, 1).Value = "TOTAUX";
        ws.Cell(row, 1).Style.Font.Bold = true;
        for (var c = 2; c <= 7; c++)
        {
            ws.Cell(row, c).FormulaA1 = $"SUM({ws.Cell(4, c).Address}:{ws.Cell(row - 1, c).Address})";
            ws.Cell(row, c).Style.Font.Bold = true;
            ws.Cell(row, c).Style.NumberFormat.Format = "#,##0.000";
        }

        ws.Columns().AdjustToContents();
        return WorkbookToBytes(wb);
    }

    public byte[] ExportBalanceSheetToCsv(BalanceSheetDto dto)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Section;Compte;Libellé;Montant;N-1");
        AppendStatementCsvSection(sb, "ACTIF", dto.Assets);
        AppendStatementCsvTotal(sb, "TOTAL ACTIF", dto.TotalAssets);
        AppendStatementCsvSection(sb, "PASSIF", dto.Liabilities);
        AppendStatementCsvTotal(sb, "TOTAL PASSIF", dto.TotalLiabilities);
        AppendStatementCsvTotal(sb, "RÉSULTAT NET", dto.NetResult);
        return BuildBytes(sb);
    }

    public byte[] ExportBalanceSheetToExcel(BalanceSheetDto dto)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Bilan");
        var row = BuildStatementSheetHeader(ws, "Bilan");
        row = AppendStatementExcelSection(ws, row, "ACTIF", dto.Assets);
        row = AppendStatementExcelTotal(ws, row, "TOTAL ACTIF", dto.TotalAssets);
        row++;
        row = AppendStatementExcelSection(ws, row, "PASSIF", dto.Liabilities);
        row = AppendStatementExcelTotal(ws, row, "TOTAL PASSIF", dto.TotalLiabilities);
        AppendStatementExcelTotal(ws, row, "RÉSULTAT NET", dto.NetResult);
        ws.Columns().AdjustToContents();
        return WorkbookToBytes(wb);
    }

    public byte[] ExportIncomeStatementToCsv(IncomeStatementDto dto)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Section;Compte;Libellé;Montant;N-1");
        AppendStatementCsvSection(sb, "PRODUITS", dto.Revenue);
        AppendStatementCsvTotal(sb, "TOTAL PRODUITS", dto.TotalRevenue);
        AppendStatementCsvSection(sb, "CHARGES", dto.Expenses);
        AppendStatementCsvTotal(sb, "TOTAL CHARGES", dto.TotalExpenses);
        AppendStatementCsvTotal(sb, "RÉSULTAT NET", dto.NetResult);
        return BuildBytes(sb);
    }

    public byte[] ExportIncomeStatementToExcel(IncomeStatementDto dto)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Compte de résultat");
        var row = BuildStatementSheetHeader(ws, "Compte de résultat");
        row = AppendStatementExcelSection(ws, row, "PRODUITS", dto.Revenue);
        row = AppendStatementExcelTotal(ws, row, "TOTAL PRODUITS", dto.TotalRevenue);
        row++;
        row = AppendStatementExcelSection(ws, row, "CHARGES", dto.Expenses);
        row = AppendStatementExcelTotal(ws, row, "TOTAL CHARGES", dto.TotalExpenses);
        AppendStatementExcelTotal(ws, row, "RÉSULTAT NET", dto.NetResult);
        ws.Columns().AdjustToContents();
        return WorkbookToBytes(wb);
    }

    private void AppendStatementCsvSection(StringBuilder sb, string section, IReadOnlyList<FinancialStatementLineDto> lines)
    {
        foreach (var l in lines)
        {
            sb.Append(Escape(section)).Append(Separator);
            sb.Append(Escape(l.AccountNumber)).Append(Separator);
            sb.Append(Escape(l.Label)).Append(Separator);
            sb.Append(FormatDecimal(l.Amount)).Append(Separator);
            sb.AppendLine(l.PreviousYearAmount.HasValue ? FormatDecimal(l.PreviousYearAmount.Value) : string.Empty);
        }
    }

    private static void AppendStatementCsvTotal(StringBuilder sb, string label, decimal amount)
    {
        sb.Append(Separator).Append(Separator);
        sb.Append(Escape(label)).Append(Separator);
        sb.Append(FormatDecimal(amount)).AppendLine(";");
    }

    private static int BuildStatementSheetHeader(IXLWorksheet ws, string title)
    {
        ws.Cell(1, 1).Value = title;
        ws.Cell(1, 1).Style.Font.Bold = true;
        var headers = new[] { "Section", "Compte", "Libellé", "Montant", "N-1" };
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(3, c + 1).Value = headers[c];
        StyleHeaderRow(ws, headers.Length, headerRow: 3);
        return 4;
    }

    private static int AppendStatementExcelSection(IXLWorksheet ws, int row, string section, IReadOnlyList<FinancialStatementLineDto> lines)
    {
        foreach (var l in lines)
        {
            ws.Cell(row, 1).Value = section;
            ws.Cell(row, 2).Value = l.AccountNumber;
            ws.Cell(row, 3).Value = l.Label;
            ws.Cell(row, 4).Value = l.Amount;
            ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.000";
            if (l.PreviousYearAmount.HasValue)
            {
                ws.Cell(row, 5).Value = l.PreviousYearAmount.Value;
                ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.000";
            }
            row++;
        }
        return row;
    }

    private static int AppendStatementExcelTotal(IXLWorksheet ws, int row, string label, decimal amount)
    {
        ws.Cell(row, 3).Value = label;
        ws.Cell(row, 4).Value = amount;
        ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.000";
        ws.Range(row, 3, row, 4).Style.Font.Bold = true;
        return row + 1;
    }

    private static string TaxpayerLabel(int kind) => kind == 1 ? "IRPP (BIC)" : "IS (sociétés)";

    public byte[] ExportFiscalResultToCsv(FiscalResultDeclarationDto d)
    {
        var c = d.Computation;
        var sb = new StringBuilder();
        sb.Append("Détermination du résultat fiscal — Exercice;").AppendLine(d.FiscalYear.ToString(CultureInfo.InvariantCulture));
        sb.Append("Type de contribuable;").AppendLine(Escape(TaxpayerLabel(d.TaxpayerKind)));
        sb.AppendLine();
        sb.AppendLine("RÉINTÉGRATIONS;Montant");
        foreach (var l in d.Adjustments.Where(a => a.Kind == 0))
            sb.Append(Escape(l.Label)).Append(Separator).AppendLine(FormatDecimal(l.Amount));
        sb.Append("Total des réintégrations;").AppendLine(FormatDecimal(c.TotalReintegrations));
        sb.AppendLine();
        sb.AppendLine("DÉDUCTIONS;Montant");
        foreach (var l in d.Adjustments.Where(a => a.Kind == 1))
            sb.Append(Escape(l.Label)).Append(Separator).AppendLine(FormatDecimal(l.Amount));
        sb.Append("Total des déductions;").AppendLine(FormatDecimal(c.TotalDeductions));
        sb.AppendLine();
        sb.AppendLine("CALCUL;Montant");
        foreach (var (label, value) in FiscalComputationRows(d))
            sb.Append(Escape(label)).Append(Separator).AppendLine(FormatDecimal(value));

        return BuildBytes(sb);
    }

    public byte[] ExportFiscalResultToExcel(FiscalResultDeclarationDto d)
    {
        using var wb = new XLWorkbook();
        BuildFiscalResultSheet(wb, d);
        wb.Worksheets.First().Columns().AdjustToContents();
        return WorkbookToBytes(wb);
    }

    public byte[] ExportConsolidatedLiasseToExcel(ConsolidatedLiasseDto d)
    {
        using var wb = new XLWorkbook();

        AddNctSheet(wb, "Bilan", d.FinancialStatements.BalanceSheet.Assets, d.FinancialStatements.BalanceSheet.EquityAndLiabilities);
        AddNctSheet(wb, "Résultat", d.FinancialStatements.IncomeStatement.Lines, null);
        BuildFiscalResultSheet(wb, d.FiscalResult);
        AddFiscalTableSheet(wb, "Amortissements", d.AmortizationTable, "Dotation exercice", "VNC");
        AddFiscalTableSheet(wb, "Provisions", d.ProvisionsTable, "Montant", "N-1");

        foreach (var ws in wb.Worksheets)
            ws.Columns().AdjustToContents();
        return WorkbookToBytes(wb);
    }

    private static IEnumerable<(string Label, decimal Value)> FiscalComputationRows(FiscalResultDeclarationDto d)
    {
        var c = d.Computation;
        yield return ("Résultat comptable net", c.AccountingResult);
        yield return ("+ Réintégrations", c.TotalReintegrations);
        yield return ("- Déductions", c.TotalDeductions);
        yield return ("Résultat fiscal avant reports", c.ResultBeforeCarryForward);
        yield return ("- Déficits imputés", c.DeficitsImputed);
        yield return ("- Amortissements différés imputés", c.DeferredDepreciationImputed);
        yield return ("Résultat fiscal imposable", c.TaxableResult);
        yield return (d.TaxpayerKind == 1 ? "IRPP (barème)" : "IS (résultat × taux)", c.TaxOnResult);
        yield return ("Minimum d'impôt", c.MinimumTax);
        yield return ("Impôt dû", c.TaxDue);
        yield return ("Contribution sociale de solidarité (CSS)", c.Css);
        yield return ("Total impôt dû", c.TotalTaxDue);
        yield return ("- Acomptes provisionnels", c.AcomptesPaid);
        yield return ("- Retenues à la source subies", c.WithholdingSuffered);
        yield return ("- Crédit d'impôt antérieur", c.PriorTaxCredit);
        yield return ("Net à payer", c.NetToPay);
        yield return ("Crédit d'impôt à reporter", c.CreditToCarry);
    }

    private static void BuildFiscalResultSheet(XLWorkbook wb, FiscalResultDeclarationDto d)
    {
        var ws = wb.Worksheets.Add("Détermination fiscale");
        ws.Cell(1, 1).Value = $"Détermination du résultat fiscal — Exercice {d.FiscalYear} — {TaxpayerLabel(d.TaxpayerKind)}";
        ws.Cell(1, 1).Style.Font.Bold = true;

        var row = 3;
        ws.Cell(row, 1).Value = "RÉINTÉGRATIONS";
        ws.Cell(row, 2).Value = "Montant";
        StyleHeaderRow(ws, 2, row);
        row++;
        foreach (var l in d.Adjustments.Where(a => a.Kind == 0))
        {
            ws.Cell(row, 1).Value = l.Label;
            ws.Cell(row, 2).Value = l.Amount;
            ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.000";
            row++;
        }
        ws.Cell(row, 1).Value = "Total des réintégrations";
        ws.Cell(row, 2).Value = d.Computation.TotalReintegrations;
        ws.Range(row, 1, row, 2).Style.Font.Bold = true;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.000";
        row += 2;

        ws.Cell(row, 1).Value = "DÉDUCTIONS";
        ws.Cell(row, 2).Value = "Montant";
        StyleHeaderRow(ws, 2, row);
        row++;
        foreach (var l in d.Adjustments.Where(a => a.Kind == 1))
        {
            ws.Cell(row, 1).Value = l.Label;
            ws.Cell(row, 2).Value = l.Amount;
            ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.000";
            row++;
        }
        ws.Cell(row, 1).Value = "Total des déductions";
        ws.Cell(row, 2).Value = d.Computation.TotalDeductions;
        ws.Range(row, 1, row, 2).Style.Font.Bold = true;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.000";
        row += 2;

        ws.Cell(row, 1).Value = "CALCUL DE L'IMPÔT";
        ws.Cell(row, 2).Value = "Montant";
        StyleHeaderRow(ws, 2, row);
        row++;
        foreach (var (label, value) in FiscalComputationRows(d))
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 2).Value = value;
            ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.000";
            row++;
        }
    }

    private static void AddNctSheet(XLWorkbook wb, string name, IReadOnlyList<NctLineDto> primary, IReadOnlyList<NctLineDto>? secondary)
    {
        var ws = wb.Worksheets.Add(name);
        var headers = new[] { "Rubrique", "Exercice", "N-1" };
        for (var col = 0; col < headers.Length; col++)
            ws.Cell(1, col + 1).Value = headers[col];
        StyleHeaderRow(ws, headers.Length);

        var row = 2;
        void Emit(IReadOnlyList<NctLineDto> lines)
        {
            foreach (var l in lines)
            {
                ws.Cell(row, 1).Value = new string(' ', l.Level * 2) + l.Label;
                ws.Cell(row, 2).Value = l.Amount;
                ws.Cell(row, 3).Value = l.PreviousAmount;
                ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.000";
                ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.000";
                if (l.IsSubtotal)
                    ws.Range(row, 1, row, 3).Style.Font.Bold = true;
                row++;
            }
        }
        Emit(primary);
        if (secondary is not null)
        {
            row++;
            Emit(secondary);
        }
    }

    private static void AddFiscalTableSheet(XLWorkbook wb, string name, IReadOnlyList<FiscalTableRowDto> rows, string amountHeader, string previousHeader)
    {
        var ws = wb.Worksheets.Add(name);
        var headers = new[] { "Code", "Libellé", amountHeader, previousHeader };
        for (var col = 0; col < headers.Length; col++)
            ws.Cell(1, col + 1).Value = headers[col];
        StyleHeaderRow(ws, headers.Length);

        var row = 2;
        foreach (var r in rows)
        {
            ws.Cell(row, 1).Value = r.Code;
            ws.Cell(row, 2).Value = r.Label;
            ws.Cell(row, 3).Value = r.Amount;
            ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.000";
            if (r.PreviousAmount.HasValue)
            {
                ws.Cell(row, 4).Value = r.PreviousAmount.Value;
                ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.000";
            }
            row++;
        }
    }

    private static void StyleHeaderRow(IXLWorksheet ws, int colCount, int headerRow = 1)
    {
        for (var c = 1; c <= colCount; c++)
        {
            ws.Cell(headerRow, c).Style.Font.Bold = true;
            ws.Cell(headerRow, c).Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");
        }
    }

    private static byte[] WorkbookToBytes(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static byte[] BuildBytes(StringBuilder sb)
    {
        var csvBytes = Encoding.UTF8.GetBytes(sb.ToString());
        var result = new byte[Utf8Bom.Length + csvBytes.Length];
        Buffer.BlockCopy(Utf8Bom, 0, result, 0, Utf8Bom.Length);
        Buffer.BlockCopy(csvBytes, 0, result, Utf8Bom.Length, csvBytes.Length);
        return result;
    }
}
