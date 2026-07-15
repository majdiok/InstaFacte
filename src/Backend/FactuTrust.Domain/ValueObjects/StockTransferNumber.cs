using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.ValueObjects;

/// <summary>
/// Represents a unique, sequential stock transfer number.
/// Format: {PREFIX}-{YEAR}-{SEQUENCE}
/// Example: TR-2026-000001
/// </summary>
public sealed class StockTransferNumber : ValueObject
{
    public string Value { get; private set; }
    public string Prefix { get; private set; }
    public int Year { get; private set; }
    public int Sequence { get; private set; }

    private StockTransferNumber()
    {
        Value = string.Empty;
        Prefix = string.Empty;
        Year = 0;
        Sequence = 0;
    }

    private StockTransferNumber(string value, string prefix, int year, int sequence)
    {
        Value = value;
        Prefix = prefix;
        Year = year;
        Sequence = sequence;
    }

    public static StockTransferNumber Create(string prefix, int year, int sequence)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentException("Le préfixe est obligatoire", nameof(prefix));

        if (year < 2000 || year > 2100)
            throw new ArgumentException("Année invalide", nameof(year));

        if (sequence < 1)
            throw new ArgumentException("Le numéro de séquence doit être positif", nameof(sequence));

        var normalizedPrefix = prefix.Trim().ToUpperInvariant();
        var formattedSequence = sequence.ToString("D6");
        var value = $"{normalizedPrefix}-{year}-{formattedSequence}";

        return new StockTransferNumber(value, normalizedPrefix, year, sequence);
    }

    public static Result<StockTransferNumber> Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result.Failure<StockTransferNumber>(Error.Validation("StockTransferNumber", "Le numéro de transfert est obligatoire"));

        var parts = value.Split('-');
        if (parts.Length != 3)
            return Result.Failure<StockTransferNumber>(Error.Validation("StockTransferNumber",
                "Format de numéro de transfert invalide. Format attendu: PREFIX-ANNÉE-NUMÉRO"));

        if (!int.TryParse(parts[1], out var year))
            return Result.Failure<StockTransferNumber>(Error.Validation("StockTransferNumber", "Année invalide"));

        if (!int.TryParse(parts[2], out var sequence))
            return Result.Failure<StockTransferNumber>(Error.Validation("StockTransferNumber", "Numéro de séquence invalide"));

        try
        {
            return Result.Success(Create(parts[0], year, sequence));
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<StockTransferNumber>(Error.Validation("StockTransferNumber", ex.Message));
        }
    }

    public StockTransferNumber Next()
    {
        return Create(Prefix, Year, Sequence + 1);
    }

    public StockTransferNumber NextYear(int newYear)
    {
        return Create(Prefix, newYear, 1);
    }

    public override string ToString() => Value;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
