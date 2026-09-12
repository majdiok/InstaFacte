using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Multi-devises, lot 2 : résolution serveur du taux et contrôle de la surcharge.
///
/// <para>
/// Le taux détermine le montant en dinar porté en comptabilité. Ces tests verrouillent le fait
/// qu'un client ne peut pas le fixer librement : il faut la permission dédiée <b>et</b> rester dans
/// la tolérance configurée.
/// </para>
/// </summary>
public sealed class ExchangeRateResolverTests
{
    private const string OverridePermission = "accounting:exchange_rate_override";

    private static readonly DateTime January = new(2026, 1, 15);
    private static readonly DateTime February = new(2026, 2, 10);

    private static Currency Eur(ExchangeRatePeriodicity periodicity = ExchangeRatePeriodicity.Mensuelle) =>
        Currency.Create("EUR", "Euro", 2, periodicity).Value;

    private static ExchangeRateResolver Build(
        Currency? currency,
        IEnumerable<CurrencyExchangeRate>? rates = null,
        bool multiCurrencyEnabled = true,
        bool hasOverridePermission = false,
        decimal tolerancePercent = 5m)
    {
        var repo = new Mock<ICurrencyRepository>();
        repo.Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);
        repo.Setup(r => r.GetRatesAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((rates ?? Array.Empty<CurrencyExchangeRate>()).ToList());

        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.HasPermission(It.IsAny<string>()))
            .Returns((string p) => hasOverridePermission && p == OverridePermission);

        var settings = Options.Create(new AccountingSettings
        {
            MultiCurrencyEnabled = multiCurrencyEnabled,
            ExchangeRateOverrideTolerancePercent = tolerancePercent
        });

        return new ExchangeRateResolver(repo.Object, user.Object, settings);
    }

    private static CurrencyExchangeRate Rate(Currency currency, int? month, decimal rate) =>
        CurrencyExchangeRate.Create(currency.Id, 2026, month, rate).Value;

    // ── Devise de tenue ────────────────────────────────────────────────────

    [Fact]
    public async Task FunctionalCurrency_ResolvesToRateOne()
    {
        var resolver = Build(currency: null);

        var result = await resolver.ResolveAsync(Money.DefaultCurrency, January, null);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsFunctional);
        Assert.Equal(1m, result.Value.Rate);
        Assert.False(result.Value.IsOverridden);
    }

    [Fact]
    public async Task EmptyCurrency_IsTreatedAsFunctional()
    {
        var resolver = Build(currency: null);

        var result = await resolver.ResolveAsync(null, January, null);

        Assert.True(result.Value.IsFunctional);
    }

    [Fact]
    public async Task FunctionalCurrency_WithRate_IsRefusedRatherThanIgnored()
    {
        // Accepter puis ignorer laisserait croire à l'utilisateur que son taux a été pris en compte.
        var resolver = Build(currency: null);

        var result = await resolver.ResolveAsync(Money.DefaultCurrency, January, 3.3m);

        Assert.True(result.IsFailure);
    }

    // ── Garde-fous de catalogue ────────────────────────────────────────────

    [Fact]
    public async Task ForeignCurrency_IsRefusedWhenFeatureIsDisabled()
    {
        var eur = Eur();
        var resolver = Build(eur, new[] { Rate(eur, 1, 3.31420m) }, multiCurrencyEnabled: false);

        var result = await resolver.ResolveAsync("EUR", January, null);

        Assert.True(result.IsFailure);
        Assert.Contains("multi-devises", result.Error.Description);
    }

    [Fact]
    public async Task UnknownCurrency_IsRefused()
    {
        var resolver = Build(currency: null);

        var result = await resolver.ResolveAsync("XXX", January, null);

        Assert.True(result.IsFailure);
        Assert.Contains("n'existe pas", result.Error.Description);
    }

    [Fact]
    public async Task InactiveCurrency_IsRefused()
    {
        var eur = Eur();
        eur.Deactivate();
        var resolver = Build(eur, new[] { Rate(eur, 1, 3.31420m) });

        var result = await resolver.ResolveAsync("EUR", January, null);

        Assert.True(result.IsFailure);
        Assert.Contains("désactivée", result.Error.Description);
    }

    [Fact]
    public async Task MissingRate_IsRefusedWithTheExactPeriod()
    {
        var eur = Eur();
        // Un taux existe en janvier, mais l'écriture est de février.
        var resolver = Build(eur, new[] { Rate(eur, 1, 3.31420m) });

        var result = await resolver.ResolveAsync("EUR", February, null);

        Assert.True(result.IsFailure);
        Assert.Contains("02/2026", result.Error.Description);
    }

    // ── Résolution ─────────────────────────────────────────────────────────

    [Fact]
    public async Task MonthlyCurrency_PicksTheRateOfTheEntryMonth()
    {
        var eur = Eur();
        var resolver = Build(eur, new[] { Rate(eur, 1, 3.31420m), Rate(eur, 2, 3.30200m) });

        var january = await resolver.ResolveAsync("EUR", January, null);
        var february = await resolver.ResolveAsync("EUR", February, null);

        Assert.Equal(3.31420m, january.Value.Rate);
        Assert.Equal(3.30200m, february.Value.Rate);
    }

    [Fact]
    public async Task FixedCurrency_PicksTheYearlyRate()
    {
        var eur = Eur(ExchangeRatePeriodicity.Fixe);
        // Le taux annuel est stocké avec Month = null ; les taux mensuels sont ignorés.
        var resolver = Build(eur, new[] { Rate(eur, null, 3.35000m), Rate(eur, 2, 3.30200m) });

        var result = await resolver.ResolveAsync("EUR", February, null);

        Assert.Equal(3.35000m, result.Value.Rate);
    }

    [Fact]
    public async Task LowercaseCurrency_IsNormalized()
    {
        var eur = Eur();
        var resolver = Build(eur, new[] { Rate(eur, 1, 3.31420m) });

        var result = await resolver.ResolveAsync("  eur ", January, null);

        Assert.True(result.IsSuccess);
        Assert.Equal("EUR", result.Value.CurrencyCode);
    }

    // ── Surcharge du taux ──────────────────────────────────────────────────

    [Fact]
    public async Task RequestedRateEqualToReference_IsNotAnOverride()
    {
        var eur = Eur();
        var resolver = Build(eur, new[] { Rate(eur, 1, 3.31420m) }, hasOverridePermission: false);

        var result = await resolver.ResolveAsync("EUR", January, 3.31420m);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsOverridden);
    }

    [Fact]
    public async Task OverrideWithoutPermission_IsForbidden()
    {
        var eur = Eur();
        var resolver = Build(eur, new[] { Rate(eur, 1, 3.31420m) }, hasOverridePermission: false);

        var result = await resolver.ResolveAsync("EUR", January, 3.32000m);

        Assert.True(result.IsFailure);
        Assert.Contains("autorisation", result.Error.Description);
    }

    [Fact]
    public async Task OverrideWithinTolerance_IsAcceptedAndFlagged()
    {
        var eur = Eur();
        var resolver = Build(eur, new[] { Rate(eur, 1, 3.31420m) }, hasOverridePermission: true);

        var result = await resolver.ResolveAsync("EUR", January, 3.35000m);

        Assert.True(result.IsSuccess);
        Assert.Equal(3.35000m, result.Value.Rate);
        Assert.Equal(3.31420m, result.Value.ReferenceRate);
        Assert.True(result.Value.IsOverridden);
    }

    [Fact]
    public async Task OverrideBeyondTolerance_IsRefusedEvenWithPermission()
    {
        // Une faute de frappe (33,1420 au lieu de 3,31420) multiplierait la contre-valeur par dix :
        // la borne la transforme en refus plutôt qu'en écriture fausse.
        var eur = Eur();
        var resolver = Build(eur, new[] { Rate(eur, 1, 3.31420m) }, hasOverridePermission: true);

        var result = await resolver.ResolveAsync("EUR", January, 33.14200m);

        Assert.True(result.IsFailure);
        Assert.Contains("tolérance", result.Error.Description);
    }

    [Fact]
    public async Task NonPositiveRequestedRate_IsRefused()
    {
        var eur = Eur();
        var resolver = Build(eur, new[] { Rate(eur, 1, 3.31420m) }, hasOverridePermission: true);

        Assert.True((await resolver.ResolveAsync("EUR", January, 0m)).IsFailure);
        Assert.True((await resolver.ResolveAsync("EUR", January, -3.3m)).IsFailure);
    }

    [Fact]
    public async Task ToleranceIsConfigurable()
    {
        var eur = Eur();
        // 3,35 s'écarte de ~1,08 % de 3,31420 : accepté à 5 %, refusé à 1 %.
        var permissive = Build(eur, new[] { Rate(eur, 1, 3.31420m) }, hasOverridePermission: true, tolerancePercent: 5m);
        var strict = Build(eur, new[] { Rate(eur, 1, 3.31420m) }, hasOverridePermission: true, tolerancePercent: 1m);

        Assert.True((await permissive.ResolveAsync("EUR", January, 3.35000m)).IsSuccess);
        Assert.True((await strict.ResolveAsync("EUR", January, 3.35000m)).IsFailure);
    }
}
