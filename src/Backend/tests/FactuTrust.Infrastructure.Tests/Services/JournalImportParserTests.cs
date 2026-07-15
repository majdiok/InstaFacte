using System.Text;
using FactuTrust.Application.DTOs;
using FactuTrust.Infrastructure.Services;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class JournalImportParserTests
{
    private readonly JournalImportParser _parser = new();

    private static byte[] Utf8(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void ParseCsv_BalancedEntry_GroupsLinesIntoOneEntry()
    {
        var csv =
            "journal,numero,date,compte,libelle,debit,credit\n" +
            "JV,1,2026-01-15,4111,Client X,100.000,0\n" +
            "JV,1,2026-01-15,707,Vente,0,100.000\n";

        var (entries, issues) = _parser.Parse(Utf8(csv), JournalImportFormat.Csv);

        Assert.Empty(issues);
        Assert.Single(entries);
        var e = entries[0];
        Assert.Equal("JV/1", e.Ref);
        Assert.Equal("JV", e.JournalCode);
        Assert.Equal(new DateTime(2026, 1, 15), e.EntryDate);
        Assert.Equal(2, e.Lines.Count);
        Assert.True(e.IsBalanced);
        Assert.Equal(100m, e.TotalDebit);
        Assert.Equal(100m, e.TotalCredit);
    }

    [Fact]
    public void ParseCsv_SemicolonDelimiter_CommaDecimals_Parsed()
    {
        var csv =
            "journal;numero;date;compte;libelle;debit;credit\n" +
            "JA;7;15/01/2026;6011;Achat;1500,000;0\n" +
            "JA;7;15/01/2026;4011;Fournisseur;0;1500,000\n";

        var (entries, issues) = _parser.Parse(Utf8(csv), JournalImportFormat.Csv);

        Assert.Empty(issues);
        Assert.Single(entries);
        Assert.Equal(1500m, entries[0].TotalDebit);
        Assert.True(entries[0].IsBalanced);
    }

    [Fact]
    public void ParseCsv_TwoPieces_ProducesTwoEntries()
    {
        var csv =
            "journal,numero,date,compte,libelle,debit,credit\n" +
            "JV,1,2026-01-15,4111,C,100,0\n" +
            "JV,1,2026-01-15,707,V,0,100\n" +
            "JV,2,2026-01-16,4111,C,50,0\n" +
            "JV,2,2026-01-16,707,V,0,50\n";

        var (entries, issues) = _parser.Parse(Utf8(csv), JournalImportFormat.Csv);

        Assert.Empty(issues);
        Assert.Equal(2, entries.Count);
    }

    [Fact]
    public void ParseCsv_Unbalanced_FlaggedNotBalanced()
    {
        var csv =
            "journal,numero,date,compte,libelle,debit,credit\n" +
            "JV,1,2026-01-15,4111,C,100,0\n" +
            "JV,1,2026-01-15,707,V,0,80\n";

        var (entries, _) = _parser.Parse(Utf8(csv), JournalImportFormat.Csv);

        Assert.Single(entries);
        Assert.False(entries[0].IsBalanced);
    }

    [Fact]
    public void ParseCsv_MissingAccountColumn_ReturnsHeaderIssue()
    {
        var csv =
            "journal,numero,date,libelle,debit,credit\n" +
            "JV,1,2026-01-15,C,100,0\n";

        var (entries, issues) = _parser.Parse(Utf8(csv), JournalImportFormat.Csv);

        Assert.Empty(entries);
        Assert.Contains(issues, i => i.Message.Contains("compte", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ParseFec_TabSeparated_GroupsByEcritureNum()
    {
        var header = string.Join('\t', new[]
        {
            "JournalCode", "JournalLib", "EcritureNum", "EcritureDate",
            "CompteNum", "CompteLib", "CompAuxNum", "CompAuxLib",
            "PieceRef", "PieceDate", "EcritureLib",
            "Debit", "Credit", "EcritureLet", "DateLet",
            "ValidDate", "Montantdevise", "Idevise"
        });
        string Line(string acct, string debit, string credit) => string.Join('\t', new[]
        {
            "JV", "Journal des Ventes", "5", "20260115",
            acct, "Libellé compte", "", "",
            "5", "20260115", "Vente client",
            debit, credit, "", "",
            "20260115", "", ""
        });

        var fec = header + "\n" + Line("4111", "100.00", "0.00") + "\n" + Line("707", "0.00", "100.00") + "\n";

        var (entries, issues) = _parser.Parse(Utf8(fec), JournalImportFormat.Fec);

        Assert.Empty(issues);
        Assert.Single(entries);
        Assert.Equal("JV/5", entries[0].Ref);
        Assert.Equal(2, entries[0].Lines.Count);
        Assert.True(entries[0].IsBalanced);
        Assert.Equal(100m, entries[0].TotalCredit);
    }

    [Fact]
    public void Parse_EmptyContent_ReturnsIssue()
    {
        var (entries, issues) = _parser.Parse(Array.Empty<byte>(), JournalImportFormat.Csv);

        Assert.Empty(entries);
        Assert.NotEmpty(issues);
    }

    [Fact]
    public void ParseCsv_BadDate_ReportsRowIssue()
    {
        var csv =
            "journal,numero,date,compte,libelle,debit,credit\n" +
            "JV,1,not-a-date,4111,C,100,0\n" +
            "JV,1,not-a-date,707,V,0,100\n";

        var (_, issues) = _parser.Parse(Utf8(csv), JournalImportFormat.Csv);

        Assert.Contains(issues, i => i.Message.Contains("Date illisible", StringComparison.OrdinalIgnoreCase));
    }
}
