using FactuTrust.Infrastructure.Services;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class TunisianBankAmountParsingTests
{
  [Theory]
  [InlineData("17.837,479", 17837.479)]
  [InlineData("3.000,000", 3000.000)]
  [InlineData("2,202", 2.202)]
  [InlineData("0,417", 0.417)]
  [InlineData("199.195,843", 199195.843)]
  [InlineData("18.732,846", 18732.846)]
  public void TryParseTunisianBankAmount_ParsesFormats(string input, decimal expected)
  {
    Assert.True(TunisianBankAmountParsing.TryParseTunisianBankAmount(input, out var amount));
    Assert.Equal(expected, amount);
  }

  [Theory]
  [InlineData("17.837,479")]
  [InlineData("3.000,000")]
  [InlineData("2,202")]
  [InlineData("18.732,846")]
  public void TryParseStrictAmount_AcceptsValidTokens(string input)
  {
    Assert.True(TunisianBankAmountParsing.TryParseStrictAmount(input, out _));
  }

  [Theory]
  [InlineData("18.732,846199.195,843")]
  [InlineData("06852401311220252,202")]
  [InlineData("")]
  [InlineData("abc")]
  public void TryParseStrictAmount_RejectsInvalidTokens(string input)
  {
    Assert.False(TunisianBankAmountParsing.TryParseStrictAmount(input, out _));
  }

  [Theory]
  [InlineData(3000.000, true)]
  [InlineData(50_000_000, true)]
  [InlineData(50_000_001, false)]
  [InlineData(0, false)]
  public void IsPlausibleOperationAmount_ValidatesRange(decimal amount, bool expected)
  {
    Assert.Equal(expected, TunisianBankAmountParsing.IsPlausibleOperationAmount(amount));
  }

  [Theory]
  [InlineData("")]
  [InlineData("abc")]
  public void TryParseTunisianBankAmount_Invalid_ReturnsFalse(string input)
  {
    Assert.False(TunisianBankAmountParsing.TryParseTunisianBankAmount(input, out _));
  }
}
