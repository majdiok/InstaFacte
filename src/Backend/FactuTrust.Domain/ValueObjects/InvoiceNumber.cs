using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.ValueObjects;

/// <summary>
/// Represents a unique, sequential invoice number.
/// Format: {PREFIX}-{YEAR}-{SEQUENCE}
/// Example: FAC-2026-000001
/// </summary>
public sealed class InvoiceNumber : ValueObject
{
    public string Value { get; private set; }
    public string Prefix { get; private set; }
    public int Year { get; private set; }
    public int Sequence { get; private set; }

    // Required for EF Core
    private InvoiceNumber()
    {
        Value = string.Empty;
        Prefix = string.Empty;
        Year = 0;
        Sequence = 0;
    }

    private InvoiceNumber(string value, string prefix, int year, int sequence)
    {
        Value = value;
        Prefix = prefix;
        Year = year;
        Sequence = sequence;
    }

    public static InvoiceNumber Create(string prefix, int year, int sequence)
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

        return new InvoiceNumber(value, normalizedPrefix, year, sequence);
    }

    public static Result<InvoiceNumber> Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result.Failure<InvoiceNumber>(Error.Validation("InvoiceNumber", "Le numéro de facture est obligatoire"));

        var parts = value.Split('-');
        if (parts.Length != 3)
            return Result.Failure<InvoiceNumber>(Error.Validation("InvoiceNumber", 
                "Format de numéro de facture invalide. Format attendu: PREFIX-ANNÉE-NUMÉRO"));

        if (!int.TryParse(parts[1], out var year))
            return Result.Failure<InvoiceNumber>(Error.Validation("InvoiceNumber", "Année invalide"));

        if (!int.TryParse(parts[2], out var sequence))
            return Result.Failure<InvoiceNumber>(Error.Validation("InvoiceNumber", "Numéro de séquence invalide"));

        try
        {
            return Result.Success(Create(parts[0], year, sequence));
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<InvoiceNumber>(Error.Validation("InvoiceNumber", ex.Message));
        }
    }

    public InvoiceNumber Next()
    {
        return Create(Prefix, Year, Sequence + 1);
    }

    public InvoiceNumber NextYear(int newYear)
    {
        return Create(Prefix, newYear, 1);
    }

    public override string ToString() => Value;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
