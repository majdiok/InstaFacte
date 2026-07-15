using FactuTrust.Infrastructure.Services;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class TunisianBankStatementTextParserTests
{
  private readonly TunisianBankStatementTextParser _parser = new();

  [Fact]
  public void Parse_SampleBiatLine_ExtractsOperation()
  {
    const string sample = """
      MONASTIR 31 01 2026
      SOLDE AU 31 12 2025 17.837,479
      02 01 VIREMENT TN MEME BQ 0639194 02012026 3.000,000
      02 01 PAIEMENT EFFET 06852401 31122025 2,202
      """;

    var (_, lines, issues) = _parser.Parse(sample);

    Assert.Empty(issues.Where(i => i.IsBlocking));
    Assert.Equal(2, lines.Count);
    Assert.Equal("VIREMENT TN MEME BQ", lines[0].Description);
    Assert.Equal(3000.000m, lines[0].Amount);
    Assert.Equal("0639194", lines[0].Reference);
    Assert.False(lines[0].IsDebit);
    Assert.True(lines[1].IsDebit);
  }

  [Fact]
  public void Parse_ExtractsRibAndOpeningBalance()
  {
    const string sample = """
      RIB : 08 503 00023 10 01399 7 43 STE PERFECT COMPUTER SERVICES
      MONASTIR 31 01 2026
      SOLDE AU 31 12 2025 17.837,479
      05 01 VIREMENT TN AUTRE BQ 00000105 06012026 797,300
      """;

    var (header, lines, _) = _parser.Parse(sample);

    Assert.Equal("08503000231001399743", header.RibDigits);
    Assert.Equal(17837.479m, header.OpeningBalance);
    Assert.Equal(new DateTime(2026, 1, 31), header.StatementDate);
    Assert.Single(lines);
  }

  [Fact]
  public void Parse_Footer_ExtractsThreeTotals()
  {
    const string sample = """
      MONASTIR 31 01 2026
      SOLDE AU 31 12 2025 17.837,479
      DINAR TUNISIEN18.732,846199.195,843180.462,997ecteur Agence
      """;

    var (header, _, _) = _parser.Parse(sample);

    Assert.Equal(18732.846m, header.ClosingBalance);
    Assert.Equal(199195.843m, header.TotalDebits);
    Assert.Equal(180462.997m, header.TotalCredits);
  }

  [Fact]
  public void Parse_Header_StatementDate_IgnoresAgencyFragment()
  {
    const string sample = """
      RIB : 08 503 00023 10 01399 7 43 STE PERFECT COMPUTER SERVICES
      ecteur Agence : HATEM ELHANI23 10 01399 7
      MONASTIR31 01 2026
      SOLDE AU 31 12 2025 17.837,479
      """;

    var (header, _, _) = _parser.Parse(sample);

    Assert.Equal(new DateTime(2026, 1, 31), header.StatementDate);
  }

  [Fact]
  public void Parse_GluedOperation_VirementMemeBq()
  {
    const string sample = """
      MONASTIR 31 01 2026
      SOLDE AU 31 12 2025 17.837,479
      02 01VIREMENT TN MEME BQ    0639194020120263.000,000
      """;

    var (_, lines, _) = _parser.Parse(sample);

    Assert.Single(lines);
    Assert.Equal("0639194", lines[0].Reference);
    Assert.Equal(3000.000m, lines[0].Amount);
    Assert.Equal(new DateTime(2026, 1, 2), lines[0].ValueDate);
  }
}
