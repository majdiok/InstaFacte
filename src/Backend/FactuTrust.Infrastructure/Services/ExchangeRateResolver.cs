using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services;

/// <inheritdoc cref="IExchangeRateResolver"/>
public sealed class ExchangeRateResolver : IExchangeRateResolver
{
    private readonly ICurrencyRepository _currencies;
    private readonly ICurrentUser _currentUser;
    private readonly AccountingSettings _settings;

    public ExchangeRateResolver(
        ICurrencyRepository currencies,
        ICurrentUser currentUser,
        IOptions<AccountingSettings> settings)
    {
        _currencies = currencies;
        _currentUser = currentUser;
        _settings = settings.Value;
    }

    public async Task<Result<ResolvedExchangeRate>> ResolveAsync(
        string? currencyCode,
        DateTime entryDate,
        decimal? requestedRate,
        CancellationToken cancellationToken = default)
    {
        var code = (currencyCode ?? string.Empty).Trim().ToUpperInvariant();
        if (code.Length == 0)
            code = Money.DefaultCurrency;

        // Devise de tenue : aucune conversion. Un taux transmis est refusé plutôt qu'ignoré
        // silencieusement — sinon l'écran laisserait croire qu'il a été pris en compte.
        if (code == Money.DefaultCurrency)
        {
            if (requestedRate is not null && requestedRate.Value != 1m)
                return Result.Failure<ResolvedExchangeRate>(Error.Validation("ExchangeRate",
                    $"Une écriture en {Money.DefaultCurrency} n'a pas de taux de change."));

            return Result.Success(new ResolvedExchangeRate(code, 1m, 1m, false, true));
        }

        if (!_settings.MultiCurrencyEnabled)
            return Result.Failure<ResolvedExchangeRate>(Error.Validation("Currency",
                "La gestion multi-devises n'est pas activée."));

        var currency = await _currencies.GetByCodeAsync(code, cancellationToken);
        if (currency is null)
            return Result.Failure<ResolvedExchangeRate>(Error.Validation("Currency",
                $"La devise {code} n'existe pas dans le catalogue."));

        if (!currency.IsActive)
            return Result.Failure<ResolvedExchangeRate>(Error.Validation("Currency",
                $"La devise {code} est désactivée."));

        var reference = await FindReferenceRateAsync(currency, entryDate, cancellationToken);
        if (reference.IsFailure)
            return Result.Failure<ResolvedExchangeRate>(reference.Error);

        var referenceRate = reference.Value;

        if (requestedRate is null || requestedRate.Value == referenceRate)
            return Result.Success(new ResolvedExchangeRate(code, referenceRate, referenceRate, false, false));

        if (requestedRate.Value <= 0)
            return Result.Failure<ResolvedExchangeRate>(Error.Validation("ExchangeRate",
                "Le taux de change doit être strictement positif."));

        if (!_currentUser.HasPermission(Permissions.Accounting.ExchangeRateOverride))
            return Result.Failure<ResolvedExchangeRate>(Error.Forbidden(
                $"La saisie d'un taux différent de celui de la table ({referenceRate}) requiert une autorisation."));

        var tolerance = _settings.ExchangeRateOverrideTolerancePercent;
        var deviation = Math.Abs(requestedRate.Value - referenceRate) / referenceRate * 100m;
        if (deviation > tolerance)
            return Result.Failure<ResolvedExchangeRate>(Error.Validation("ExchangeRate",
                $"Le taux saisi s'écarte de {deviation:N2} % du taux de la table ({referenceRate}), "
                + $"au-delà de la tolérance de {tolerance:N2} %."));

        return Result.Success(new ResolvedExchangeRate(code, requestedRate.Value, referenceRate, true, false));
    }

    private async Task<Result<decimal>> FindReferenceRateAsync(
        Currency currency,
        DateTime entryDate,
        CancellationToken cancellationToken)
    {
        var fiscalYear = entryDate.Year;
        var rates = await _currencies.GetRatesAsync(currency.Id, fiscalYear, cancellationToken);

        var monthly = currency.RatePeriodicity == ExchangeRatePeriodicity.Mensuelle;
        var month = monthly ? entryDate.Month : (int?)null;
        var match = rates.FirstOrDefault(r => r.Month == month);

        if (match is null)
        {
            // Message actionnable : il désigne la période exacte à renseigner, pas un « taux manquant ».
            var period = monthly
                ? $"{entryDate:MM/yyyy}"
                : $"l'exercice {fiscalYear}";

            return Result.Failure<decimal>(Error.Validation("ExchangeRate",
                $"Aucun taux de change n'est configuré pour {currency.Code} sur {period}."));
        }

        return Result.Success(match.Rate);
    }
}
