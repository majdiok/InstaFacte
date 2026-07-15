using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.ValueObjects;

/// <summary>
/// Represents a monetary value in Tunisian Dinar (TND).
/// TND uses 3 decimal places (millimes).
/// </summary>
public sealed class Money : ValueObject
{
    public const string DefaultCurrency = "TND";
    public const int DecimalPlaces = 3;

    public decimal Amount { get; private set; }
    public string Currency { get; private set; }

    // Required for EF Core
    private Money()
    {
        Amount = 0;
        Currency = DefaultCurrency;
    }

    private Money(decimal amount, string currency)
    {
        Amount = Math.Round(amount, DecimalPlaces);
        Currency = currency;
    }

    public static Money Create(decimal amount, string currency = DefaultCurrency)
    {
        if (amount < 0)
            throw new ArgumentException("Le montant ne peut pas être négatif", nameof(amount));

        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("La devise est obligatoire", nameof(currency));

        return new Money(amount, currency.ToUpperInvariant());
    }

    public static Money Zero(string currency = DefaultCurrency) => new(0, currency);

    /// <summary>
    /// Amount may be negative (e.g. fiscal stamp on credit notes, or net document totals).
    /// </summary>
    public static Money FromSignedAmount(decimal amount, string currency = DefaultCurrency)
    {
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("La devise est obligatoire", nameof(currency));

        return new Money(Math.Round(amount, DecimalPlaces), currency.ToUpperInvariant());
    }

    public static Money FromMillimes(long millimes, string currency = DefaultCurrency)
    {
        return new Money(millimes / 1000m, currency);
    }

    public long ToMillimes() => (long)(Amount * 1000);

    public Money Add(Money other)
    {
        ValidateSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        ValidateSameCurrency(other);
        var result = Amount - other.Amount;
        if (result < 0)
            throw new InvalidOperationException("Le résultat ne peut pas être négatif");
        return new Money(result, Currency);
    }

    public Money Multiply(decimal factor)
    {
        if (factor < 0)
            throw new ArgumentException("Le facteur ne peut pas être négatif", nameof(factor));
        return new Money(Amount * factor, Currency);
    }

    public Money ApplyPercentage(decimal percentage)
    {
        return new Money(Amount * (percentage / 100m), Currency);
    }

    private void ValidateSameCurrency(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException($"Impossible d'opérer sur des devises différentes : {Currency} et {other.Currency}");
    }

    public static Money operator +(Money left, Money right) => left.Add(right);
    public static Money operator -(Money left, Money right) => left.Subtract(right);
    public static Money operator *(Money left, decimal right) => left.Multiply(right);

    public override string ToString() => $"{Amount:N3} {Currency}";

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}
