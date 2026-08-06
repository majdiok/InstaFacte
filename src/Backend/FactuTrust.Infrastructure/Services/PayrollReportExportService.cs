using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using FactuTrust.Application.Common.Enums;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Exports CSV / Excel des états de contrôle paie. Mêmes conventions que les exports
/// comptables : CSV séparé par « ; » en UTF-8 avec BOM, Excel avec en-tête grisé et
/// montants au format millime.
/// </summary>
public sealed class PayrollReportExportService : IPayrollReportExportService
{
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];
    private const string AmountFormat = "#,##0.000";

    // ── Livre de paie ──────────────────────────────────────────────────────────────────────

    private static readonly string[] BookHeaders =
    {
        "Matricule", "Salarié", "CIN", "N° CNSS", "Catégorie", "Échelon", "Date d'embauche", "Mois",
        "Brut", "Brut CNSSable", "CNSS salarié", "Frais professionnels", "Déductions familiales",
        "Net imposable", "IRPP", "Régul. IRPP", "CSS", "Régul. CSS", "Autres retenues",
        "Indemnités non imposables", "Net à payer"
    };

    public byte[] ExportPayrollBookToCsv(PayrollBookDto book)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(';', BookHeaders));

        foreach (var line in book.Lines)
        {
            sb.Append(Escape(line.EmployeeNumber)).Append(';');
            sb.Append(Escape(line.EmployeeName)).Append(';');
            sb.Append(Escape(line.Cin ?? string.Empty)).Append(';');
            sb.Append(Escape(line.CnssNumber ?? string.Empty)).Append(';');
            sb.Append(Escape(line.Category ?? string.Empty)).Append(';');
            sb.Append(Escape(line.Echelon ?? string.Empty)).Append(';');
            sb.Append(Date(line.HireDate)).Append(';');
            sb.Append(line.MonthsCount.ToString(CultureInfo.InvariantCulture)).Append(';');
            sb.Append(Money(line.GrossSalary)).Append(';');
            sb.Append(Money(line.CnssableGross)).Append(';');
            sb.Append(Money(line.CnssEmployee)).Append(';');
            sb.Append(Money(line.ProfessionalExpenses)).Append(';');
            sb.Append(Money(line.FamilyDeductions)).Append(';');
            sb.Append(Money(line.NetTaxable)).Append(';');
            sb.Append(Money(line.Irpp)).Append(';');
            sb.Append(Money(line.IrppRegularization)).Append(';');
            sb.Append(Money(line.Css)).Append(';');
            sb.Append(Money(line.CssRegularization)).Append(';');
            sb.Append(Money(line.OtherDeductions)).Append(';');
            sb.Append(Money(line.NonTaxableAllowances)).Append(';');
            sb.Append(Money(line.NetSalary));
            sb.AppendLine();
        }

        sb.AppendLine();
        // 21 colonnes alignées sur l'en-tête : les 7 premières sont vides hors libellé TOTAL.
        sb.Append("TOTAL;;;;;;;");
        sb.Append(book.Lines.Sum(l => l.MonthsCount).ToString(CultureInfo.InvariantCulture)).Append(';');
        sb.Append(Money(book.TotalGross)).Append(';');
        sb.Append(Money(book.TotalCnssableGross)).Append(';');
        sb.Append(Money(book.TotalCnssEmployee)).Append(';');
        sb.Append(Money(book.TotalProfessionalExpenses)).Append(';');
        sb.Append(Money(book.TotalFamilyDeductions)).Append(';');
        sb.Append(Money(book.TotalNetTaxable)).Append(';');
        sb.Append(Money(book.TotalIrpp)).Append(';');
        sb.Append(Money(book.TotalIrppRegularization)).Append(';');
        sb.Append(Money(book.TotalCss)).Append(';');
        sb.Append(Money(book.TotalCssRegularization)).Append(';');
        sb.Append(Money(book.TotalOtherDeductions)).Append(';');
        sb.Append(Money(book.TotalNonTaxableAllowances)).Append(';');
        sb.Append(Money(book.TotalNetSalary));
        sb.AppendLine();

        AppendCsvEmployerCharges(sb, book.TotalCnssEmployer, book.TotalWorkAccident, book.TotalTfp,
            book.TotalFoprolos, book.TotalCssEmployer, book.TotalEmployerCharges, book.TotalEmployerCost);

        return ToBytes(sb);
    }

    public byte[] ExportPayrollBookToExcel(PayrollBookDto book)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Livre de paie");

        var row = WriteSheetTitle(ws, "Livre de paie simplifié", book.PeriodLabel, book.IsProvisional, BookHeaders.Length);

        for (var c = 0; c < BookHeaders.Length; c++)
            ws.Cell(row, c + 1).Value = BookHeaders[c];
        StyleHeaderRow(ws, BookHeaders.Length, row);
        row++;

        foreach (var line in book.Lines)
        {
            var col = 1;
            ws.Cell(row, col++).Value = line.EmployeeNumber;
            ws.Cell(row, col++).Value = line.EmployeeName;
            ws.Cell(row, col++).Value = line.Cin ?? string.Empty;
            ws.Cell(row, col++).Value = line.CnssNumber ?? string.Empty;
            ws.Cell(row, col++).Value = line.Category ?? string.Empty;
            ws.Cell(row, col++).Value = line.Echelon ?? string.Empty;
            if (line.HireDate.HasValue)
            {
                ws.Cell(row, col).Value = line.HireDate.Value;
                ws.Cell(row, col).Style.NumberFormat.Format = "dd/MM/yyyy";
            }
            col++;
            ws.Cell(row, col++).Value = line.MonthsCount;
            WriteAmounts(ws, row, col,
                line.GrossSalary, line.CnssableGross, line.CnssEmployee, line.ProfessionalExpenses,
                line.FamilyDeductions, line.NetTaxable, line.Irpp, line.IrppRegularization,
                line.Css, line.CssRegularization, line.OtherDeductions, line.NonTaxableAllowances,
                line.NetSalary);
            row++;
        }

        ws.Cell(row, 1).Value = "TOTAL";
        ws.Cell(row, 8).Value = book.Lines.Sum(l => l.MonthsCount);
        WriteAmounts(ws, row, 9,
            book.TotalGross, book.TotalCnssableGross, book.TotalCnssEmployee, book.TotalProfessionalExpenses,
            book.TotalFamilyDeductions, book.TotalNetTaxable, book.TotalIrpp, book.TotalIrppRegularization,
            book.TotalCss, book.TotalCssRegularization, book.TotalOtherDeductions, book.TotalNonTaxableAllowances,
            book.TotalNetSalary);
        StyleTotalRow(ws, BookHeaders.Length, row);
        row += 2;

        WriteEmployerChargesBlock(ws, ref row, book.TotalCnssEmployer, book.TotalWorkAccident,
            book.TotalTfp, book.TotalFoprolos, book.TotalCssEmployer, book.TotalEmployerCharges,
            book.TotalEmployerCost);

        if (book.MissingMonths.Count > 0)
        {
            row++;
            ws.Cell(row, 1).Value = $"Mois sans cycle éligible : {string.Join(", ", book.MissingMonths)}";
        }

        ws.Columns().AdjustToContents();
        return WorkbookToBytes(wb);
    }

    // ── Journal de paie ────────────────────────────────────────────────────────────────────

    private static readonly string[] JournalEmployeeHeaders =
    {
        "Matricule", "Salarié", "N° CNSS", "Brut", "Brut CNSSable", "CNSS salarié",
        "Frais professionnels", "Déductions familiales", "Net imposable", "IRPP", "Régul. IRPP",
        "CSS", "Régul. CSS", "Autres retenues", "Indemnités non imposables", "Net à payer",
        "CNSS patronale", "Accident de travail", "TFP", "FOPROLOS", "CSS patronale",
        "Charges patronales", "Coût employeur"
    };

    private static readonly string[] JournalAccountingHeaders =
    {
        "Compte", "Libellé compte", "Libellé écriture", "Débit", "Crédit"
    };

    public byte[] ExportPayrollJournalToCsv(PayrollJournalDto journal, PayrollJournalView view) =>
        view == PayrollJournalView.Accounting
            ? BuildJournalAccountingCsv(journal)
            : BuildJournalEmployeeCsv(journal);

    public byte[] ExportPayrollJournalToExcel(PayrollJournalDto journal, PayrollJournalView view) =>
        view == PayrollJournalView.Accounting
            ? BuildJournalAccountingExcel(journal)
            : BuildJournalEmployeeExcel(journal);

    private static byte[] BuildJournalEmployeeCsv(PayrollJournalDto journal)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(';', JournalEmployeeHeaders));

        foreach (var line in journal.Lines)
        {
            sb.Append(Escape(line.EmployeeNumber)).Append(';');
            sb.Append(Escape(line.EmployeeName)).Append(';');
            sb.Append(Escape(line.CnssNumber ?? string.Empty)).Append(';');
            sb.Append(Money(line.GrossSalary)).Append(';');
            sb.Append(Money(line.CnssableGross)).Append(';');
            sb.Append(Money(line.CnssEmployee)).Append(';');
            sb.Append(Money(line.ProfessionalExpenses)).Append(';');
            sb.Append(Money(line.FamilyDeductions)).Append(';');
            sb.Append(Money(line.MonthlyNetTaxable)).Append(';');
            sb.Append(Money(line.Irpp)).Append(';');
            sb.Append(Money(line.IrppRegularization)).Append(';');
            sb.Append(Money(line.Css)).Append(';');
            sb.Append(Money(line.CssRegularization)).Append(';');
            sb.Append(Money(line.OtherDeductions)).Append(';');
            sb.Append(Money(line.NonTaxableAllowances)).Append(';');
            sb.Append(Money(line.NetSalary)).Append(';');
            sb.Append(Money(line.CnssEmployer)).Append(';');
            sb.Append(Money(line.WorkAccidentContribution)).Append(';');
            sb.Append(Money(line.Tfp)).Append(';');
            sb.Append(Money(line.Foprolos)).Append(';');
            sb.Append(Money(line.CssEmployer)).Append(';');
            sb.Append(Money(line.TotalEmployerCharges)).Append(';');
            sb.Append(Money(line.TotalCost));
            sb.AppendLine();
        }

        sb.AppendLine();
        sb.Append("TOTAL;;;");
        sb.Append(Money(journal.TotalGross)).Append(';');
        sb.Append(Money(journal.TotalCnssableGross)).Append(';');
        sb.Append(Money(journal.TotalCnssEmployee)).Append(';');
        sb.Append(Money(journal.TotalProfessionalExpenses)).Append(';');
        sb.Append(Money(journal.TotalFamilyDeductions)).Append(';');
        sb.Append(Money(journal.TotalNetTaxable)).Append(';');
        sb.Append(Money(journal.TotalIrpp)).Append(';');
        sb.Append(Money(journal.TotalIrppRegularization)).Append(';');
        sb.Append(Money(journal.TotalCss)).Append(';');
        sb.Append(Money(journal.TotalCssRegularization)).Append(';');
        sb.Append(Money(journal.TotalOtherDeductions)).Append(';');
        sb.Append(Money(journal.TotalNonTaxableAllowances)).Append(';');
        sb.Append(Money(journal.TotalNetSalary)).Append(';');
        sb.Append(Money(journal.TotalCnssEmployer)).Append(';');
        sb.Append(Money(journal.TotalWorkAccident)).Append(';');
        sb.Append(Money(journal.TotalTfp)).Append(';');
        sb.Append(Money(journal.TotalFoprolos)).Append(';');
        sb.Append(Money(journal.TotalCssEmployer)).Append(';');
        sb.Append(Money(journal.TotalEmployerCharges)).Append(';');
        sb.Append(Money(journal.TotalEmployerCost));
        sb.AppendLine();

        return ToBytes(sb);
    }

    private static byte[] BuildJournalAccountingCsv(PayrollJournalDto journal)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(';', JournalAccountingHeaders));

        foreach (var line in journal.AccountingLines)
        {
            sb.Append(Escape(line.AccountNumber)).Append(';');
            sb.Append(Escape(line.AccountLabel)).Append(';');
            sb.Append(Escape(line.Label)).Append(';');
            sb.Append(Money(line.Debit)).Append(';');
            sb.Append(Money(line.Credit));
            sb.AppendLine();
        }

        sb.AppendLine();
        sb.Append("TOTAL;;;");
        sb.Append(Money(journal.TotalDebit)).Append(';');
        sb.Append(Money(journal.TotalCredit));
        sb.AppendLine();

        if (!journal.IsBalanced)
            sb.AppendLine("Contrôle;Total débit different du total crédit;;;");

        return ToBytes(sb);
    }

    private static byte[] BuildJournalEmployeeExcel(PayrollJournalDto journal)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Journal de paie");

        var row = WriteSheetTitle(ws, "Journal de paie — par salarié", journal.PeriodLabel, journal.IsProvisional, JournalEmployeeHeaders.Length);

        for (var c = 0; c < JournalEmployeeHeaders.Length; c++)
            ws.Cell(row, c + 1).Value = JournalEmployeeHeaders[c];
        StyleHeaderRow(ws, JournalEmployeeHeaders.Length, row);
        row++;

        foreach (var line in journal.Lines)
        {
            ws.Cell(row, 1).Value = line.EmployeeNumber;
            ws.Cell(row, 2).Value = line.EmployeeName;
            ws.Cell(row, 3).Value = line.CnssNumber ?? string.Empty;
            WriteAmounts(ws, row, 4,
                line.GrossSalary, line.CnssableGross, line.CnssEmployee, line.ProfessionalExpenses,
                line.FamilyDeductions, line.MonthlyNetTaxable, line.Irpp, line.IrppRegularization,
                line.Css, line.CssRegularization, line.OtherDeductions, line.NonTaxableAllowances,
                line.NetSalary, line.CnssEmployer, line.WorkAccidentContribution, line.Tfp,
                line.Foprolos, line.CssEmployer, line.TotalEmployerCharges, line.TotalCost);
            row++;
        }

        ws.Cell(row, 1).Value = "TOTAL";
        WriteAmounts(ws, row, 4,
            journal.TotalGross, journal.TotalCnssableGross, journal.TotalCnssEmployee,
            journal.TotalProfessionalExpenses, journal.TotalFamilyDeductions, journal.TotalNetTaxable,
            journal.TotalIrpp, journal.TotalIrppRegularization, journal.TotalCss,
            journal.TotalCssRegularization, journal.TotalOtherDeductions, journal.TotalNonTaxableAllowances,
            journal.TotalNetSalary, journal.TotalCnssEmployer, journal.TotalWorkAccident,
            journal.TotalTfp, journal.TotalFoprolos, journal.TotalCssEmployer,
            journal.TotalEmployerCharges, journal.TotalEmployerCost);
        StyleTotalRow(ws, JournalEmployeeHeaders.Length, row);

        ws.Columns().AdjustToContents();
        return WorkbookToBytes(wb);
    }

    private static byte[] BuildJournalAccountingExcel(PayrollJournalDto journal)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Ventilation comptable");

        var row = WriteSheetTitle(ws, "Journal de paie — ventilation comptable", journal.PeriodLabel, journal.IsProvisional, JournalAccountingHeaders.Length);

        ws.Cell(row, 1).Value = journal.AccountingLinesArePosted
            ? $"Écriture comptabilisée n° {journal.AccountingEntryNumber} — journal {journal.AccountingJournalCode}"
            : "Ventilation simulée : le cycle n'est pas encore comptabilisé.";
        ws.Cell(row, 1).Style.Font.Italic = true;
        row += 2;

        for (var c = 0; c < JournalAccountingHeaders.Length; c++)
            ws.Cell(row, c + 1).Value = JournalAccountingHeaders[c];
        StyleHeaderRow(ws, JournalAccountingHeaders.Length, row);
        row++;

        foreach (var line in journal.AccountingLines)
        {
            ws.Cell(row, 1).Value = line.AccountNumber;
            ws.Cell(row, 2).Value = line.AccountLabel;
            ws.Cell(row, 3).Value = line.Label;
            WriteAmounts(ws, row, 4, line.Debit, line.Credit);
            row++;
        }

        ws.Cell(row, 1).Value = "TOTAL";
        WriteAmounts(ws, row, 4, journal.TotalDebit, journal.TotalCredit);
        StyleTotalRow(ws, JournalAccountingHeaders.Length, row);

        if (!journal.IsBalanced)
        {
            row += 2;
            ws.Cell(row, 1).Value = "Contrôle : total débit différent du total crédit.";
            ws.Cell(row, 1).Style.Font.Bold = true;
        }

        ws.Columns().AdjustToContents();
        return WorkbookToBytes(wb);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────────────────

    private static void AppendCsvEmployerCharges(
        StringBuilder sb, decimal cnssEmployer, decimal workAccident, decimal tfp,
        decimal foprolos, decimal cssEmployer, decimal employerCharges, decimal employerCost)
    {
        sb.AppendLine();
        sb.AppendLine("Charges patronales;Montant");
        sb.Append("CNSS patronale;").AppendLine(Money(cnssEmployer));
        sb.Append("Accident de travail;").AppendLine(Money(workAccident));
        sb.Append("TFP;").AppendLine(Money(tfp));
        sb.Append("FOPROLOS;").AppendLine(Money(foprolos));
        sb.Append("CSS patronale;").AppendLine(Money(cssEmployer));
        sb.Append("Total charges patronales;").AppendLine(Money(employerCharges));
        sb.Append("Coût employeur;").AppendLine(Money(employerCost));
    }

    private static void WriteEmployerChargesBlock(
        IXLWorksheet ws, ref int row, decimal cnssEmployer, decimal workAccident, decimal tfp,
        decimal foprolos, decimal cssEmployer, decimal employerCharges, decimal employerCost)
    {
        ws.Cell(row, 1).Value = "Charges patronales";
        ws.Cell(row, 1).Style.Font.Bold = true;
        row++;

        var rows = new (string Label, decimal Value)[]
        {
            ("CNSS patronale", cnssEmployer),
            ("Accident de travail", workAccident),
            ("TFP", tfp),
            ("FOPROLOS", foprolos),
            ("CSS patronale", cssEmployer),
            ("Total charges patronales", employerCharges),
            ("Coût employeur", employerCost)
        };

        foreach (var (label, value) in rows)
        {
            ws.Cell(row, 1).Value = label;
            WriteAmounts(ws, row, 2, value);
            row++;
        }
    }

    /// <summary>Titre, période et éventuel avertissement provisoire. Retourne la ligne d'en-tête du tableau.</summary>
    private static int WriteSheetTitle(IXLWorksheet ws, string title, string periodLabel, bool isProvisional, int colCount)
    {
        ws.Cell(1, 1).Value = title;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 13;
        ws.Range(1, 1, 1, Math.Max(colCount, 1)).Merge();

        ws.Cell(2, 1).Value = periodLabel;
        ws.Cell(2, 1).Style.Font.Italic = true;

        if (!isProvisional)
            return 4;

        ws.Cell(3, 1).Value = "ÉTAT PROVISOIRE — inclut des cycles calculés non validés";
        ws.Cell(3, 1).Style.Font.Bold = true;
        ws.Cell(3, 1).Style.Font.FontColor = XLColor.FromHtml("#B91C1C");
        return 5;
    }

    private static void WriteAmounts(IXLWorksheet ws, int row, int firstColumn, params decimal[] values)
    {
        for (var i = 0; i < values.Length; i++)
        {
            var cell = ws.Cell(row, firstColumn + i);
            cell.Value = values[i];
            cell.Style.NumberFormat.Format = AmountFormat;
        }
    }

    private static void StyleHeaderRow(IXLWorksheet ws, int colCount, int headerRow)
    {
        for (var c = 1; c <= colCount; c++)
        {
            ws.Cell(headerRow, c).Style.Font.Bold = true;
            ws.Cell(headerRow, c).Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");
        }
    }

    private static void StyleTotalRow(IXLWorksheet ws, int colCount, int totalRow)
    {
        for (var c = 1; c <= colCount; c++)
        {
            ws.Cell(totalRow, c).Style.Font.Bold = true;
            ws.Cell(totalRow, c).Style.Border.TopBorder = XLBorderStyleValues.Thin;
        }
    }

    private static byte[] WorkbookToBytes(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static byte[] ToBytes(StringBuilder sb)
    {
        var csvBytes = Encoding.UTF8.GetBytes(sb.ToString());
        var result = new byte[Utf8Bom.Length + csvBytes.Length];
        Buffer.BlockCopy(Utf8Bom, 0, result, 0, Utf8Bom.Length);
        Buffer.BlockCopy(csvBytes, 0, result, Utf8Bom.Length, csvBytes.Length);
        return result;
    }

    private static string Money(decimal amount) => amount.ToString("0.000", CultureInfo.InvariantCulture);

    private static string Date(DateTime? value) =>
        value?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (value.Contains(';') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";

        return value;
    }
}
