using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Taux de change d'une devise pour un exercice, et pour un mois lorsque la devise est configurée
/// en <see cref="ExchangeRatePeriodicity.Mensuelle"/>.
///
/// <para>
/// <b>Sens du taux — à ne jamais inverser.</b> <see cref="Rate"/> est le nombre d'unités de devise
/// <i>fonctionnelle</i> pour <b>une</b> unité de devise étrangère. Pour 1 EUR = 3,31420 TND, on
/// stocke <c>3.31420</c> sur la devise EUR. La contre-valeur se calcule donc
/// <c>MillimeRounding.Round(montantEnDevise * Rate)</c>, jamais par division.
/// </para>
/// </summary>
public sealed class CurrencyExchangeRate : Entity
{
    public const int MinFiscalYear = 2000;
    public const int MaxFiscalYear = 2100;

    public Guid CurrencyId { get; private set; }
    public Currency? Currency { get; private set; }

    public int FiscalYear { get; private set; }

    /// <summary>Mois de 1 à 12, ou <c>null</c> pour un taux fixe couvrant tout l'exercice.</summary>
    public int? Month { get; private set; }

    /// <summary>Unités de devise fonctionnelle pour une unité de devise étrangère. Strictement positif.</summary>
    public decimal Rate { get; private set; }

    private CurrencyExchangeRate() { }

    public static Result<CurrencyExchangeRate> Create(Guid currencyId, int fiscalYear, int? month, decimal rate)
    {
        if (currencyId == Guid.Empty)
            return Result.Failure<CurrencyExchangeRate>(Error.Validation("CurrencyId", "La devise est obligatoire"));

        var yearResult = ValidateFiscalYear(fiscalYear);
        if (yearResult.IsFailure)
            return Result.Failure<CurrencyExchangeRate>(yearResult.Error);

        var monthResult = ValidateMonth(month);
        if (monthResult.IsFailure)
            return Result.Failure<CurrencyExchangeRate>(monthResult.Error);

        var rateResult = ValidateRate(rate);
        if (rateResult.IsFailure)
            return Result.Failure<CurrencyExchangeRate>(rateResult.Error);

        return Result.Success(new CurrencyExchangeRate
        {
            CurrencyId = currencyId,
            FiscalYear = fiscalYear,
            Month = month,
            Rate = rate
        });
    }

    public Result SetRate(decimal rate)
    {
        var rateResult = ValidateRate(rate);
        if (rateResult.IsFailure)
            return rateResult;

        Rate = rate;
        return Result.Success();
    }

    private static Result ValidateFiscalYear(int fiscalYear)
    {
        if (fiscalYear is < MinFiscalYear or > MaxFiscalYear)
            return Result.Failure(Error.Validation("FiscalYear",
                $"L'exercice doit être compris entre {MinFiscalYear} et {MaxFiscalYear}."));

        return Result.Success();
    }

    private static Result ValidateMonth(int? month)
    {
        if (month is not null && month is < 1 or > 12)
            return Result.Failure(Error.Validation("Month", "Le mois doit être compris entre 1 et 12."));

        return Result.Success();
    }

    private static Result ValidateRate(decimal rate)
    {
        if (rate <= 0)
            return Result.Failure(Error.Validation("Rate", "Le taux de change doit être strictement positif."));

        return Result.Success();
    }
}
