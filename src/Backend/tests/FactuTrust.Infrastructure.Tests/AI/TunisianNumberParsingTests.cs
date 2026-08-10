using FactuTrust.Application.Common;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class TunisianNumberParsingTests
{
    /// <summary>
    /// Les six cas « golden » historiques du parseur de factures InstaFact, rejoués sur la nouvelle
    /// API. Ils scellent le comportement déplacé : toute dérive ici casserait la lecture des
    /// factures natives.
    /// </summary>
    [Theory]
    [InlineData("1,250.000", 1250.000)]
    [InlineData("+3700.000", 3700.000)]
    [InlineData("-1.000", -1.000)]
    [InlineData("650,000", 650.000)]
    [InlineData("1 250,500", 1250.500)]
    [InlineData("2,500.000", 2500.000)]
    public void ParseDecimal_MatchesLegacyInstaFactBehaviour(string raw, double expected)
    {
        Assert.Equal((decimal)expected, TunisianNumberParsing.ParseDecimal(raw));
    }

    /// <summary>
    /// Ambiguïté ASSUMÉE et documentée : une virgule suivie de 1 à 3 chiffres est un séparateur
    /// décimal (convention TND), jamais un séparateur de milliers. « 1,250 » vaut donc 1 dinar
    /// 250 millimes. Ce test existe pour que le choix soit visible et délibéré.
    /// </summary>
    [Theory]
    [InlineData("1,250", 1.250)]
    [InlineData("650,000", 650.000)]
    public void ParseDecimal_CommaWithUpToThreeDigits_IsReadAsMillimes(string raw, double expected)
    {
        Assert.Equal((decimal)expected, TunisianNumberParsing.ParseDecimal(raw));
    }

    [Theory]
    [InlineData("19%", 19)]
    [InlineData("19 %", 19)]
    [InlineData("1 000,50 TND", 1000.50)]
    [InlineData("~278,000", 278.000)]
    [InlineData("12 dinars", 12)]
    [InlineData("7,477 DT", 7.477)]
    public void ParseDecimalLenient_StripsLlmNoise(string raw, double expected)
    {
        Assert.Equal((decimal)expected, TunisianNumberParsing.ParseDecimalLenient(raw));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("n/a")]
    public void ParseDecimalLenient_Unusable_ReturnsNull(string? raw)
    {
        Assert.Null(TunisianNumberParsing.ParseDecimalLenient(raw));
    }

    /// <summary>Espaces insécable (U+00A0) et fine insécable (U+202F), courants en typographie française.</summary>
    [Theory]
    [InlineData("1 234,567", 1234.567)]
    [InlineData("1 234,567", 1234.567)]
    public void ParseDecimalLenient_HandlesNonBreakingSpaces(string raw, double expected)
    {
        Assert.Equal((decimal)expected, TunisianNumberParsing.ParseDecimalLenient(raw));
    }

    [Theory]
    [InlineData("19.0", 19)]
    [InlineData("18.6", 19)]
    [InlineData("6.5", 7)]
    [InlineData("-6.5", -7)]
    public void ParseInt32Lenient_RoundsAwayFromZero(string raw, int expected)
    {
        Assert.Equal(expected, TunisianNumberParsing.ParseInt32Lenient(raw));
    }

    [Fact]
    public void ToInt32OrNull_OutOfRange_ReturnsNull()
    {
        Assert.Null(TunisianNumberParsing.ToInt32OrNull(decimal.MaxValue));
        Assert.Null(TunisianNumberParsing.ToInt32OrNull(decimal.MinValue));
        Assert.Null(TunisianNumberParsing.ToInt32OrNull(null));
    }
}
