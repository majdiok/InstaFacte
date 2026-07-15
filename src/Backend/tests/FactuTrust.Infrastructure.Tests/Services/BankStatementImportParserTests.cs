using System.Text;
using ClosedXML.Excel;
using FactuTrust.Application.DTOs;
using FactuTrust.Infrastructure.Services;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// C7 : parsing des fichiers de relevés bancaires (CSV / Excel) — colonnes debit/credit ou
/// montant signé, formats de dates et décimales FR/TN, anomalies par ligne.
/// </summary>
public sealed class BankStatementImportParserTests
{
    private readonly BankStatementImportParser _parser = new();

    private static byte[] Utf8(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void Parse_CsvWithDebitCredit_ReturnsLines()
    {
        var csv = Utf8(
            "date,libelle,reference,debit,credit\n" +
            "2026-05-03,Virement client Alpha,VIR-001,0,1500.000\n" +
            "2026-05-05,Frais bancaires,FRAIS,25.500,0\n");

        var (lines, issues) = _parser.Parse(csv, BankStatementFileFormat.Csv);

        Assert.Empty(issues);
        Assert.Equal(2, lines.Count);
        Assert.False(lines[0].IsDebit);
        Assert.Equal(1500.000m, lines[0].Amount);
        Assert.Equal("VIR-001", lines[0].Reference);
        Assert.True(lines[1].IsDebit);
        Assert.Equal(25.500m, lines[1].Amount);
    }

    [Fact]
    public void Parse_CsvWithSignedAmount_MapsSignToDirection()
    {
        // Convention : positif = crédit (encaissement), négatif = débit.
        var csv = Utf8(
            "date;libelle;montant\n" +
            "03/05/2026;Virement reçu;1 234,500\n" +
            "05/05/2026;Prélèvement STEG;-89,900\n");

        var (lines, issues) = _parser.Parse(csv, BankStatementFileFormat.Csv);

        Assert.Empty(issues);
        Assert.Equal(2, lines.Count);
        Assert.False(lines[0].IsDebit);
        Assert.Equal(1234.500m, lines[0].Amount);
        Assert.Equal(new DateTime(2026, 5, 3), lines[0].TransactionDate);
        Assert.True(lines[1].IsDebit);
        Assert.Equal(89.900m, lines[1].Amount);
    }

    [Fact]
    public void Parse_MissingAmountColumns_ReportsHeaderIssue()
    {
        var csv = Utf8("date,libelle\n2026-05-03,Sans montant\n");

        var (lines, issues) = _parser.Parse(csv, BankStatementFileFormat.Csv);

        Assert.Empty(lines);
        Assert.Contains(issues, i => i.Message.Contains("montant", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_UnreadableDate_ReportsLineIssueAndSkips()
    {
        var csv = Utf8(
            "date,libelle,montant\n" +
            "pas-une-date,Opération,100\n" +
            "2026-05-05,Opération valide,-50\n");

        var (lines, issues) = _parser.Parse(csv, BankStatementFileFormat.Csv);

        var line = Assert.Single(lines);
        Assert.Equal("Opération valide", line.Description);
        Assert.Contains(issues, i => i.Ref == "ligne 2" && i.Message.Contains("Date illisible"));
    }

    [Fact]
    public void Parse_ZeroSignedAmount_ReportsIssue()
    {
        var csv = Utf8("date,libelle,montant\n2026-05-03,Nulle,0\n");

        var (lines, issues) = _parser.Parse(csv, BankStatementFileFormat.Csv);

        Assert.Empty(lines);
        Assert.Contains(issues, i => i.Message.Contains("nul", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_DebitAndCreditOnSameLine_ReportsIssue()
    {
        var csv = Utf8("date,libelle,debit,credit\n2026-05-03,Ambiguë,10,20\n");

        var (lines, issues) = _parser.Parse(csv, BankStatementFileFormat.Csv);

        Assert.Empty(lines);
        Assert.Contains(issues, i => i.Message.Contains("simultanément"));
    }

    [Fact]
    public void Parse_EmptyFile_ReportsIssue()
    {
        var (lines, issues) = _parser.Parse(Array.Empty<byte>(), BankStatementFileFormat.Csv);

        Assert.Empty(lines);
        Assert.Contains(issues, i => i.Message.Contains("vide"));
    }

    [Fact]
    public void Parse_Excel_ReadsRowsAndDates()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Relevé");
        sheet.Cell(1, 1).Value = "Date";
        sheet.Cell(1, 2).Value = "Libellé";
        sheet.Cell(1, 3).Value = "Débit";
        sheet.Cell(1, 4).Value = "Crédit";
        sheet.Cell(2, 1).Value = new DateTime(2026, 5, 3);
        sheet.Cell(2, 2).Value = "Virement client";
        sheet.Cell(2, 4).Value = 750.250;
        sheet.Cell(3, 1).Value = new DateTime(2026, 5, 7);
        sheet.Cell(3, 2).Value = "Retrait";
        sheet.Cell(3, 3).Value = 100;
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);

        var (lines, issues) = _parser.Parse(ms.ToArray(), BankStatementFileFormat.Excel);

        Assert.Empty(issues);
        Assert.Equal(2, lines.Count);
        Assert.Equal(new DateTime(2026, 5, 3), lines[0].TransactionDate);
        Assert.False(lines[0].IsDebit);
        Assert.Equal(750.250m, lines[0].Amount);
        Assert.True(lines[1].IsDebit);
    }
}
