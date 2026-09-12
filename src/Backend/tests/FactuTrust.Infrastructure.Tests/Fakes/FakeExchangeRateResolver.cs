using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Infrastructure.Tests.Fakes;

/// <summary>
/// Double de test du résolveur de taux.
///
/// <para>
/// Par défaut il répond « devise de tenue, taux 1 », c'est-à-dire exactement le comportement
/// mono-devise : les tests d'écriture existants passent donc sans être modifiés. Renseigner
/// <see cref="Rate"/> pour simuler une écriture en devise, ou <see cref="Failure"/> pour simuler
/// une devise sans taux configuré.
/// </para>
/// </summary>
public sealed class FakeExchangeRateResolver : IExchangeRateResolver
{
    /// <summary>Taux à renvoyer. Null = devise de tenue, taux 1.</summary>
    public ResolvedExchangeRate? Rate { get; set; }

    /// <summary>Erreur à renvoyer, prioritaire sur <see cref="Rate"/>.</summary>
    public Error? Failure { get; set; }

    /// <summary>Dernier appel reçu, pour les assertions.</summary>
    public (string? CurrencyCode, DateTime EntryDate, decimal? RequestedRate)? LastCall { get; private set; }

    public Task<Result<ResolvedExchangeRate>> ResolveAsync(
        string? currencyCode,
        DateTime entryDate,
        decimal? requestedRate,
        CancellationToken cancellationToken = default)
    {
        LastCall = (currencyCode, entryDate, requestedRate);

        if (Failure is not null)
            return Task.FromResult(Result.Failure<ResolvedExchangeRate>(Failure));

        var resolved = Rate ?? new ResolvedExchangeRate(Money.DefaultCurrency, 1m, 1m, false, true);
        return Task.FromResult(Result.Success(resolved));
    }
}
