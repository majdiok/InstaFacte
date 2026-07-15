using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using UglyToad.PdfPig;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>Test golden sur le relevé BIAT JANVIER 26.pdf (texte natif).</summary>
public sealed class BankStatementGoldenPdfTests
{
  private static string FixturePath =>
    Path.Combine(AppContext.BaseDirectory, "Fixtures", "bank-statements", "biat-janvier-2026.pdf");

  [Fact]
  public void GoldenPdf_ExtractsNativeText()
  {
    var path = ResolveFixture();
    using var doc = PdfDocument.Open(path);
    var text = string.Join('\n', doc.GetPages().Select(p => p.Text));
    Assert.True(BankStatementTextQuality.IsSufficient(text));
    Assert.Contains("RIB", text, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void GoldenPdf_TextParser_ExtractsOperationsAndBalances()
  {
    var path = ResolveFixture();
    var content = File.ReadAllBytes(path);
    var extract = BankStatementPdfRawTextExtractor.Extract(content);
    Assert.True(extract.Success, extract.ErrorMessage);

    var parser = new TunisianBankStatementTextParser();
    var (header, lines, issues) = parser.Parse(extract.Text);

    Assert.Equal("08503000231001399743", header.RibDigits);
    Assert.Equal(17837.479m, header.OpeningBalance);
    Assert.Equal(new DateTime(2026, 1, 31), header.StatementDate);
    Assert.Equal(18732.846m, header.ClosingBalance);
    Assert.Equal(199195.843m, header.TotalDebits);
    Assert.Equal(180462.997m, header.TotalCredits);

    Assert.True(lines.Count > 350, $"Expected >350 lines, got {lines.Count}");
    Assert.Contains(lines, l => l.Description.Contains("VIREMENT TN MEME BQ", StringComparison.Ordinal));

    var sumDebit = lines.Where(l => l.IsDebit).Sum(l => l.Amount);
    var sumCredit = lines.Where(l => !l.IsDebit).Sum(l => l.Amount);
    Assert.InRange(sumDebit, 198500m, 200000m);
    Assert.InRange(sumCredit, 162000m, 163000m);
    Assert.InRange(sumCredit + header.OpeningBalance!.Value, 180000m, 181000m);

    var computed = sumDebit - sumCredit - header.OpeningBalance!.Value;
    Assert.InRange(computed, header.ClosingBalance!.Value - 1m, header.ClosingBalance.Value + 1m);

    Assert.DoesNotContain(issues, i => i.IsBlocking);
  }

  private static string ResolveFixture()
  {
    if (File.Exists(FixturePath))
      return FixturePath;

    var fromSource = Path.GetFullPath(Path.Combine(
      AppContext.BaseDirectory,
      "..", "..", "..",
      "Fixtures", "bank-statements", "biat-janvier-2026.pdf"));

    if (File.Exists(fromSource))
      return fromSource;

    var fromArchive = @"c:\Solution\archive\JANVIER 26.pdf";
    if (File.Exists(fromArchive))
      return fromArchive;

    throw new FileNotFoundException("Fixture biat-janvier-2026.pdf introuvable.");
  }
}
