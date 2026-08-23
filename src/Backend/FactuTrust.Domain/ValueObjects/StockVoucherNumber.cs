using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.ValueObjects;

/// <summary>
/// Sequential stock voucher number. Format: {PREFIX}-{YEAR}-{SEQUENCE} (BE-2026-000001 / BS-2026-000001).
/// </summary>
public sealed class StockVoucherNumber : ValueObject
{
    public string Value { get; private set; }
    public string Prefix { get; private set; }
    public int Year { get; private set; }
    public int Sequence { get; private set; }

    private StockVoucherNumber()
    {
        Value = string.Empty;
        Prefix = string.Empty;
        Year = 0;
        Sequence = 0;
    }

    private StockVoucherNumber(string value, string prefix, int year, int sequence)
    {
        Value = value;
        Prefix = prefix;
        Year = year;
        Sequence = sequence;
    }

    public static StockVoucherNumber Create(string prefix, int year, int sequence)
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

        return new StockVoucherNumber(value, normalizedPrefix, year, sequence);
    }

    public static Result<StockVoucherNumber> Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result.Failure<StockVoucherNumber>(Error.Validation("StockVoucherNumber",
                "Le numéro de bon de stock est obligatoire"));

        var parts = value.Split('-');
        if (parts.Length != 3)
            return Result.Failure<StockVoucherNumber>(Error.Validation("StockVoucherNumber",
                "Format de numéro invalide. Format attendu: PREFIX-ANNÉE-NUMÉRO"));

        if (!int.TryParse(parts[1], out var year))
            return Result.Failure<StockVoucherNumber>(Error.Validation("StockVoucherNumber", "Année invalide"));

        if (!int.TryParse(parts[2], out var sequence))
            return Result.Failure<StockVoucherNumber>(Error.Validation("StockVoucherNumber",
                "Numéro de séquence invalide"));

        try
        {
            return Result.Success(Create(parts[0], year, sequence));
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<StockVoucherNumber>(Error.Validation("StockVoucherNumber", ex.Message));
        }
    }

    public override string ToString() => Value;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
