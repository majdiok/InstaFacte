using System.Text.RegularExpressions;
using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.ValueObjects;

/// <summary>
/// Represents a unique, sequential cash operation number.
/// Format: {PREFIX}-YYYY-NNNNNN where PREFIX is DEP (debit) or ENC (credit).
/// Examples: DEP-2026-000001, ENC-2026-000001
/// </summary>
public sealed partial class CashOperationNumber : ValueObject
{
    public const string DebitPrefix = "DEP";
    public const string CreditPrefix = "ENC";

    public string Value { get; private set; }
    public string PrefixValue { get; private set; }
    public int Year { get; private set; }
    public int Sequence { get; private set; }

    private CashOperationNumber()
    {
        Value = string.Empty;
        PrefixValue = string.Empty;
        Year = 0;
        Sequence = 0;
    }

    private CashOperationNumber(string value, string prefixValue, int year, int sequence)
    {
        Value = value;
        PrefixValue = prefixValue;
        Year = year;
        Sequence = sequence;
    }

    public static Result<CashOperationNumber> Create(string prefix, int year, int sequence)
    {
        if (prefix != DebitPrefix && prefix != CreditPrefix)
            return Result.Failure<CashOperationNumber>(Error.Validation("Prefix", $"Le préfixe doit être '{DebitPrefix}' ou '{CreditPrefix}'"));

        if (year < 2000 || year > 2100)
            return Result.Failure<CashOperationNumber>(Error.Validation("Year", "L'année doit être comprise entre 2000 et 2100"));

        if (sequence < 1 || sequence > 999999)
            return Result.Failure<CashOperationNumber>(Error.Validation("Sequence", "Le numéro de séquence doit être entre 1 et 999999"));

        var formattedSequence = sequence.ToString("D6");
        var value = $"{prefix}-{year}-{formattedSequence}";
        return Result.Success(new CashOperationNumber(value, prefix, year, sequence));
    }

    public static Result<CashOperationNumber> Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result.Failure<CashOperationNumber>(Error.Validation("CashOperationNumber", "Le numéro d'opération caisse est obligatoire"));

        var match = CashOperationNumberRegex().Match(value.Trim().ToUpperInvariant());
        if (!match.Success)
            return Result.Failure<CashOperationNumber>(Error.Validation(
                "CashOperationNumber",
                "Format invalide. Attendu : DEP-YYYY-NNNNNN ou ENC-YYYY-NNNNNN"));

        var prefix = match.Groups[1].Value;
        var year = int.Parse(match.Groups[2].Value);
        var sequence = int.Parse(match.Groups[3].Value);

        return Create(prefix, year, sequence);
    }

    public CashOperationNumber Next()
    {
        var nextSequence = Sequence + 1;
        var next = Create(PrefixValue, Year, nextSequence);
        return next.Value;
    }

    public override string ToString() => Value;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    [GeneratedRegex(@"^(DEP|ENC)-(\d{4})-(\d{6})$")]
    private static partial Regex CashOperationNumberRegex();
}
