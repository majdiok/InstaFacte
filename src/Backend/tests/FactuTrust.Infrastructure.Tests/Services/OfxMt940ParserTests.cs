using System.Text;
using FactuTrust.Infrastructure.Services;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>Lot I : parsers de relevés bancaires OFX (SGML/XML) et MT940 (SWIFT).</summary>
public sealed class OfxMt940ParserTests
{
    // ── OFX ──────────────────────────────────────────────────────────────────

    private const string SampleOfx = """
OFXHEADER:100
DATA:OFXSGML
VERSION:102
<OFX>
<BANKMSGSRSV1><STMTTRNRS><STMTRS>
<CURDEF>TND
<BANKACCTFROM><BANKID>12345<ACCTID>12345678901234567890<ACCTTYPE>CHECKING</BANKACCTFROM>
<BANKTRANLIST>
<STMTTRN><TRNTYPE>CREDIT<DTPOSTED>20240531<TRNAMT>15000.00<FITID>OP1<NAME>VIREMENT CLIENT SOCIETE ALPHA</STMTTRN>
<STMTTRN><TRNTYPE>DEBIT<DTPOSTED>20240530<TRNAMT>-8450.00<FITID>OP2<NAME>CHEQUE 254781</STMTTRN>
</BANKTRANLIST>
<LEDGERBAL><BALAMT>6600.00<DTASOF>20240531</LEDGERBAL>
</STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>
""";

    [Fact]
    public void Ofx_ParsesLines_SignsBalancesAndAccount()
    {
        var result = new OfxBankStatementParser().Parse(Encoding.UTF8.GetBytes(SampleOfx));

        Assert.DoesNotContain(result.Issues, i => i.IsBlocking);
        Assert.Equal(2, result.Lines.Count);

        var credit = result.Lines.Single(l => l.Reference == "OP1");
        Assert.False(credit.IsDebit);
        Assert.Equal(15000m, credit.Amount);
        Assert.Equal(new DateTime(2024, 5, 31), credit.TransactionDate);
        Assert.Contains("ALPHA", credit.Description);

        var debit = result.Lines.Single(l => l.Reference == "OP2");
        Assert.True(debit.IsDebit);
        Assert.Equal(8450m, debit.Amount);

        Assert.Equal("12345678901234567890", result.DetectedRib);
        Assert.Equal(6600m, result.ClosingBalance);
    }

    [Fact]
    public void Ofx_Malformed_ReturnsBlockingIssue()
    {
        var result = new OfxBankStatementParser().Parse(Encoding.UTF8.GetBytes("ceci n'est pas un fichier OFX"));

        Assert.Empty(result.Lines);
        Assert.Contains(result.Issues, i => i.IsBlocking);
    }

    // ── MT940 ────────────────────────────────────────────────────────────────

    private const string SampleMt940 = """
:20:REF12345
:25:12345678901234567890
:28C:00001/001
:60F:C240501TND1000,00
:61:2405310531C15000,00NTRFOP1//BANKREF1
:86:VIREMENT CLIENT SOCIETE ALPHA
:61:2405300530D8450,00NCHKOP2//BANKREF2
:86:CHEQUE N 254781
:62F:C240531TND6600,00
""";

    [Fact]
    public void Mt940_ParsesLines_SignsBalancesAndAccount()
    {
        var result = new Mt940BankStatementParser().Parse(Encoding.UTF8.GetBytes(SampleMt940));

        Assert.DoesNotContain(result.Issues, i => i.IsBlocking);
        Assert.Equal(2, result.Lines.Count);

        var credit = result.Lines[0];
        Assert.False(credit.IsDebit);
        Assert.Equal(15000m, credit.Amount);
        Assert.Equal(new DateTime(2024, 5, 31), credit.TransactionDate);
        Assert.Contains("VIREMENT CLIENT", credit.Description);

        var debit = result.Lines[1];
        Assert.True(debit.IsDebit);
        Assert.Equal(8450m, debit.Amount);
        Assert.Contains("CHEQUE", debit.Description);

        Assert.Equal(1000m, result.OpeningBalance);
        Assert.Equal(6600m, result.ClosingBalance);
        Assert.Equal("12345678901234567890", result.DetectedRib);
    }

    [Fact]
    public void Mt940_Malformed_ReturnsBlockingIssue()
    {
        var result = new Mt940BankStatementParser().Parse(Encoding.UTF8.GetBytes("ligne quelconque sans tag SWIFT"));

        Assert.Empty(result.Lines);
        Assert.Contains(result.Issues, i => i.IsBlocking);
    }
}
