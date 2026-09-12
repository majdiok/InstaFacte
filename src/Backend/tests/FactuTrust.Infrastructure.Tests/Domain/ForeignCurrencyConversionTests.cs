using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Accounting;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>
/// Multi-devises, lot 2 : conversion des lignes saisies en devise vers la devise de tenue.
/// </summary>
public sealed class ForeignCurrencyConversionTests
{
    private static JournalLineInput Line(string account, decimal debitCurrency, decimal creditCurrency) =>
        new(account, account, 0m, 0m, null, ThirdPartyKind.None, debitCurrency, creditCurrency);

    /// <summary>
    /// Le sens du taux est l'erreur classique et catastrophique du multi-devises : 1 EUR vaut
    /// 3,31420 TND, donc 1 000 EUR valent 3 314,200 TND — et surtout pas 301,730.
    /// </summary>
    [Fact]
    public void Convert_MultipliesByTheRate()
    {
        var lines = new[] { Line("607", 1000m, 0m), Line("4011", 0m, 1000m) };

        var result = ForeignCurrencyConversion.Convert(lines, 3.31420m);

        Assert.True(result.IsSuccess);
        Assert.Equal(3314.200m, result.Value[0].Debit);
        Assert.Equal(3314.200m, result.Value[1].Credit);
    }

    [Fact]
    public void Convert_KeepsTheCurrencyAmounts()
    {
        var lines = new[] { Line("607", 1000m, 0m), Line("4011", 0m, 1000m) };

        var result = ForeignCurrencyConversion.Convert(lines, 3.31420m);

        Assert.Equal(1000m, result.Value[0].DebitInCurrency);
        Assert.Equal(1000m, result.Value[1].CreditInCurrency);
    }

    [Fact]
    public void Convert_RefusesNonPositiveRate()
    {
        var lines = new[] { Line("607", 1000m, 0m), Line("4011", 0m, 1000m) };

        Assert.True(ForeignCurrencyConversion.Convert(lines, 0m).IsFailure);
        Assert.True(ForeignCurrencyConversion.Convert(lines, -3.3m).IsFailure);
    }

    [Fact]
    public void Convert_RefusesEntryUnbalancedInCurrency()
    {
        var lines = new[] { Line("607", 1000m, 0m), Line("4011", 0m, 900m) };

        var result = ForeignCurrencyConversion.Convert(lines, 3.31420m);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Balance", result.Error.Code);
    }

    [Fact]
    public void Convert_RefusesLineWithBothSides()
    {
        var lines = new[] { Line("607", 500m, 500m), Line("4011", 0m, 0m) };

        Assert.True(ForeignCurrencyConversion.Convert(lines, 3.31420m).IsFailure);
    }

    /// <summary>
    /// Cœur du lot 2 : l'arrondi au millime se fait ligne à ligne, donc une écriture parfaitement
    /// équilibrée en devise peut ne plus l'être une fois convertie. Ici 33,33 + 33,33 + 33,34 = 100
    /// en euros, mais les trois contre-valeurs arrondies ne retombent pas sur celle de la ligne de
    /// contrepartie. Le résidu doit être absorbé, sans quoi l'écriture serait rejetée.
    /// </summary>
    [Fact]
    public void Convert_AbsorbsRoundingResidual()
    {
        var lines = new[]
        {
            Line("607", 33.33m, 0m),
            Line("6071", 33.33m, 0m),
            Line("6072", 33.34m, 0m),
            Line("4011", 0m, 100m)
        };

        var result = ForeignCurrencyConversion.Convert(lines, 3.31420m);

        Assert.True(result.IsSuccess);
        var totalDebit = result.Value.Sum(l => l.Debit);
        var totalCredit = result.Value.Sum(l => l.Credit);
        Assert.Equal(totalDebit, totalCredit);
    }

    [Fact]
    public void Convert_ImputesResidualOnTheLargestLineOfTheShortSide()
    {
        // Deux débits qui, arrondis, totalisent 1 millime de plus que le crédit unique.
        var lines = new[]
        {
            Line("607", 0.0005m, 0m),
            Line("6071", 0.0005m, 0m),
            Line("4011", 0m, 0.001m)
        };

        var result = ForeignCurrencyConversion.Convert(lines, 1m);

        Assert.True(result.IsSuccess);
        Assert.Equal(result.Value.Sum(l => l.Debit), result.Value.Sum(l => l.Credit));

        // Aucun montant ne devient négatif : le résidu est toujours ajouté, jamais retranché.
        Assert.All(result.Value, l =>
        {
            Assert.True(l.Debit >= 0);
            Assert.True(l.Credit >= 0);
        });
    }

    [Fact]
    public void Convert_WithRateOne_IsIdentity()
    {
        var lines = new[] { Line("607", 1234.567m, 0m), Line("4011", 0m, 1234.567m) };

        var result = ForeignCurrencyConversion.Convert(lines, 1m);

        Assert.Equal(1234.567m, result.Value[0].Debit);
        Assert.Equal(1234.567m, result.Value[1].Credit);
    }

    [Fact]
    public void Convert_RefusesFewerThanTwoLines()
    {
        var lines = new[] { Line("607", 1000m, 0m) };

        Assert.True(ForeignCurrencyConversion.Convert(lines, 3.31420m).IsFailure);
    }

    /// <summary>
    /// Scénario des captures : 1 000 EUR comptabilisés en janvier à 3,31420 puis réglés en février
    /// à 3,30200 laissent 12,200 TND d'écart de change, alors que le compte est soldé en euros.
    /// </summary>
    [Fact]
    public void Convert_TwoRatesOnTheSameAmount_ProduceTheExchangeDifference()
    {
        var january = ForeignCurrencyConversion.Convert(
            new[] { Line("607", 1000m, 0m), Line("4011", 0m, 1000m) }, 3.31420m).Value;

        var february = ForeignCurrencyConversion.Convert(
            new[] { Line("4011", 1000m, 0m), Line("532", 0m, 1000m) }, 3.30200m).Value;

        var debt = january[1].Credit;      // 3 314,200
        var settlement = february[0].Debit; // 3 302,000

        Assert.Equal(12.200m, debt - settlement);
    }
}
