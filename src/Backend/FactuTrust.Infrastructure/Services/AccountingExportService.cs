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

    private static string FormatDecimal(decimal value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture);

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
