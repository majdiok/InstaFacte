using System.Text.RegularExpressions;
using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.ValueObjects;

/// <summary>
/// Represents a delivery note number (numéro de bon de livraison).
/// Format: BL-YYYY-NNNNNN (e.g., BL-2026-000001)
/// </summary>
public sealed partial class DeliveryNoteNumber : ValueObject
{
    public string Value { get; }
    public int Year { get; }
    public int Sequence { get; }

    private DeliveryNoteNumber(string value, int year, int sequence)
    {
        Value = value;
        Year = year;
        Sequence = sequence;
    }

    public static Result<DeliveryNoteNumber> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result.Failure<DeliveryNoteNumber>(
                Error.Validation("DeliveryNoteNumber", "Le numéro de bon de livraison est obligatoire"));

        var match = DeliveryNoteNumberRegex().Match(value.Trim().ToUpperInvariant());
        if (!match.Success)
            return Result.Failure<DeliveryNoteNumber>(
                Error.Validation("DeliveryNoteNumber", "Format invalide. Attendu: BL-YYYY-NNNNNN"));

        var year = int.Parse(match.Groups[1].Value);
        var sequence = int.Parse(match.Groups[2].Value);

        return Result.Success(new DeliveryNoteNumber(value.Trim().ToUpperInvariant(), year, sequence));
    }

    public static Result<DeliveryNoteNumber> Generate(int year, int sequence)
    {
        if (year < 2020 || year > 2100)
            return Result.Failure<DeliveryNoteNumber>(
                Error.Validation("Year", "L'année doit être comprise entre 2020 et 2100"));

        if (sequence <= 0 || sequence > 999999)
            return Result.Failure<DeliveryNoteNumber>(
                Error.Validation("Sequence", "Le numéro de séquence doit être entre 1 et 999999"));

        var value = $"BL-{year}-{sequence:D6}";
        return Result.Success(new DeliveryNoteNumber(value, year, sequence));
    }

    /// <summary>
    /// Creates a delivery note number from a rendered scheme value.
    /// </summary>
    public static DeliveryNoteNumber FromRendered(string value, int year, int sequence) =>
        new(value.Trim().ToUpperInvariant(), year, sequence);

    public DeliveryNoteNumber Next()
    {
        var nextSequence = Sequence + 1;
        return new DeliveryNoteNumber($"BL-{Year}-{nextSequence:D6}", Year, nextSequence);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(DeliveryNoteNumber number) => number.Value;

    [GeneratedRegex(@"^BL-(\d{4})-(\d{6})$")]
    private static partial Regex DeliveryNoteNumberRegex();
}
