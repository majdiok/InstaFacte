using System.Text.RegularExpressions;
using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.ValueObjects;

/// <summary>
/// Sales return note number. Format: BRT-YYYY-NNNNNN (e.g. BRT-2026-000001).
/// Prefix is BRT — BR is already used by purchase receipts.
/// </summary>
public sealed partial class SalesReturnNoteNumber : ValueObject
{
    public string Value { get; }
    public int Year { get; }
    public int Sequence { get; }

    private SalesReturnNoteNumber(string value, int year, int sequence)
    {
        Value = value;
        Year = year;
        Sequence = sequence;
    }

    public static Result<SalesReturnNoteNumber> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result.Failure<SalesReturnNoteNumber>(
                Error.Validation("SalesReturnNoteNumber", "Le numéro de bon de retour est obligatoire"));

        var match = SalesReturnNoteNumberRegex().Match(value.Trim().ToUpperInvariant());
        if (!match.Success)
            return Result.Failure<SalesReturnNoteNumber>(
                Error.Validation("SalesReturnNoteNumber", "Format invalide. Attendu: BRT-YYYY-NNNNNN"));

        var year = int.Parse(match.Groups[1].Value);
        var sequence = int.Parse(match.Groups[2].Value);
        return Result.Success(new SalesReturnNoteNumber(value.Trim().ToUpperInvariant(), year, sequence));
    }

    public static Result<SalesReturnNoteNumber> Generate(int year, int sequence)
    {
        if (year < 2020 || year > 2100)
            return Result.Failure<SalesReturnNoteNumber>(
                Error.Validation("Year", "L'année doit être comprise entre 2020 et 2100"));

        if (sequence <= 0 || sequence > 999999)
            return Result.Failure<SalesReturnNoteNumber>(
                Error.Validation("Sequence", "Le numéro de séquence doit être entre 1 et 999999"));

        return Result.Success(new SalesReturnNoteNumber($"BRT-{year}-{sequence:D6}", year, sequence));
    }

    public static SalesReturnNoteNumber FromRendered(string value, int year, int sequence) =>
        new(value.Trim().ToUpperInvariant(), year, sequence);

    public override string ToString() => Value;

    public static implicit operator string(SalesReturnNoteNumber number) => number.Value;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    [GeneratedRegex(@"^BRT-(\d{4})-(\d{6})$")]
    private static partial Regex SalesReturnNoteNumberRegex();
}
