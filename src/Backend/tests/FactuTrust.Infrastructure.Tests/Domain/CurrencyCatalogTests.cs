using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>
/// Multi-devises, lot 1 : catalogue des devises et table des taux.
/// </summary>
public sealed class CurrencyTests
{
    [Theory]
    [InlineData("eur", "EUR")]
    [InlineData("  usd  ", "USD")]
    [InlineData("Chf", "CHF")]
    public void Create_NormalizesCode(string input, string expected)
    {
        var result = Currency.Create(input, "Devise", 2, ExchangeRatePeriodicity.Mensuelle);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("EU")]
    [InlineData("EURO")]
    [InlineData("E1R")]
    [InlineData("E-R")]
    public void Create_WithInvalidCode_Fails(string code)
    {
        var result = Currency.Create(code, "Devise", 2, ExchangeRatePeriodicity.Mensuelle);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Code", result.Error.Code);
    }

    [Fact]
    public void Create_TrimsLabel()
    {
        var result = Currency.Create("EUR", "  Euro  ", 2, ExchangeRatePeriodicity.Mensuelle);

        Assert.Equal("Euro", result.Value.Label);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankLabel_Fails(string label)
    {
        var result = Currency.Create("EUR", label, 2, ExchangeRatePeriodicity.Mensuelle);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Label", result.Error.Code);
    }

    [Fact]
    public void Create_WithTooLongLabel_Fails()
    {
        var result = Currency.Create("EUR", new string('X', Currency.MaxLabelLength + 1), 2, ExchangeRatePeriodicity.Mensuelle);

        Assert.True(result.IsFailure);
        Assert.Contains("60", result.Error.Description);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void Create_WithDecimalPlacesOutOfRange_Fails(int decimals)
    {
        // Money stocke 3 décimales : au-delà, la contre-valeur serait tronquée sans que personne
        // ne le voie.
        var result = Currency.Create("EUR", "Euro", decimals, ExchangeRatePeriodicity.Mensuelle);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.DecimalPlaces", result.Error.Code);
    }

    [Fact]
    public void Create_IsActiveAndNotFunctional()
    {
        var currency = Currency.Create("EUR", "Euro", 2, ExchangeRatePeriodicity.Mensuelle).Value;

        Assert.True(currency.IsActive);
        Assert.False(currency.IsFunctional);
    }

    [Fact]
    public void CreateFunctional_MarksTheCurrency()
    {
        var currency = Currency.CreateFunctional("TND", "Dinar Tunisien", 3).Value;

        Assert.True(currency.IsFunctional);
        Assert.Equal(ExchangeRatePeriodicity.Fixe, currency.RatePeriodicity);
    }

    [Fact]
    public void Update_DoesNotChangeCode()
    {
        var currency = Currency.Create("EUR", "Euro", 2, ExchangeRatePeriodicity.Mensuelle).Value;

        currency.Update("Euro (zone)", 3, ExchangeRatePeriodicity.Fixe);

        Assert.Equal("EUR", currency.Code);
        Assert.Equal("Euro (zone)", currency.Label);
        Assert.Equal(3, currency.DecimalPlaces);
        Assert.Equal(ExchangeRatePeriodicity.Fixe, currency.RatePeriodicity);
    }

    [Fact]
    public void Deactivate_IsRefusedOnFunctionalCurrency()
    {
        var currency = Currency.CreateFunctional("TND", "Dinar Tunisien", 3).Value;

        var result = currency.Deactivate();

        Assert.True(result.IsFailure);
        Assert.True(currency.IsActive);
    }

    [Fact]
    public void Deactivate_ThenActivate_RoundTrips()
    {
        var currency = Currency.Create("EUR", "Euro", 2, ExchangeRatePeriodicity.Mensuelle).Value;

        Assert.True(currency.Deactivate().IsSuccess);
        Assert.False(currency.IsActive);

        currency.Activate();
        Assert.True(currency.IsActive);
    }

    [Fact]
    public void Update_IsRefusedWhenChangingPeriodicityOfFunctionalCurrency()
    {
        var currency = Currency.CreateFunctional("TND", "Dinar Tunisien", 3).Value;

        var result = currency.Update("Dinar Tunisien", 3, ExchangeRatePeriodicity.Mensuelle);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.RatePeriodicity", result.Error.Code);
    }
}

public sealed class CurrencyExchangeRateTests
{
    private static readonly Guid AnyCurrency = Guid.NewGuid();

    [Fact]
    public void Create_WithMonth_StoresIt()
    {
        var rate = CurrencyExchangeRate.Create(AnyCurrency, 2026, 1, 3.31420m).Value;

        Assert.Equal(2026, rate.FiscalYear);
        Assert.Equal(1, rate.Month);
        Assert.Equal(3.31420m, rate.Rate);
    }

    [Fact]
    public void Create_WithoutMonth_IsTheFixedYearlyRate()
    {
        var rate = CurrencyExchangeRate.Create(AnyCurrency, 2026, null, 3.31420m).Value;

        Assert.Null(rate.Month);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public void Create_WithMonthOutOfRange_Fails(int month)
    {
        var result = CurrencyExchangeRate.Create(AnyCurrency, 2026, month, 3.31420m);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Month", result.Error.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3.5)]
    public void Create_WithNonPositiveRate_Fails(decimal rate)
    {
        // Un taux nul ou négatif produirait une contre-valeur nulle ou inversée, sans que
        // l'équilibre débit / crédit ne le signale.
        var result = CurrencyExchangeRate.Create(AnyCurrency, 2026, 1, rate);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Rate", result.Error.Code);
    }

    [Theory]
    [InlineData(1999)]
    [InlineData(2101)]
    public void Create_WithFiscalYearOutOfRange_Fails(int year)
    {
        var result = CurrencyExchangeRate.Create(AnyCurrency, year, 1, 3.31420m);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.FiscalYear", result.Error.Code);
    }

    [Fact]
    public void Create_WithoutCurrency_Fails()
    {
        var result = CurrencyExchangeRate.Create(Guid.Empty, 2026, 1, 3.31420m);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.CurrencyId", result.Error.Code);
    }

    [Fact]
    public void SetRate_RefusesNonPositiveAndKeepsPreviousValue()
    {
        var rate = CurrencyExchangeRate.Create(AnyCurrency, 2026, 1, 3.31420m).Value;

        var result = rate.SetRate(0m);

        Assert.True(result.IsFailure);
        Assert.Equal(3.31420m, rate.Rate);
    }

    [Fact]
    public void SetRate_UpdatesTheValue()
    {
        var rate = CurrencyExchangeRate.Create(AnyCurrency, 2026, 1, 3.31420m).Value;

        Assert.True(rate.SetRate(3.30200m).IsSuccess);
        Assert.Equal(3.30200m, rate.Rate);
    }

    /// <summary>
    /// Documente le sens du taux, qui est l'erreur classique et catastrophique du multi-devises :
    /// Rate est le nombre d'unités de devise de tenue pour UNE unité de devise étrangère. La
    /// contre-valeur se calcule donc par multiplication, jamais par division.
    /// </summary>
    [Fact]
    public void Rate_IsFunctionalUnitsPerForeignUnit()
    {
        var eurToTnd = CurrencyExchangeRate.Create(AnyCurrency, 2026, 1, 3.31420m).Value;

        var thousandEurosInDinars = 1000m * eurToTnd.Rate;

        Assert.Equal(3314.200m, thousandEurosInDinars);
    }
}
