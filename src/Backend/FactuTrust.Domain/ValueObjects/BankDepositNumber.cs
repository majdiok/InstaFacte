using System.Text.RegularExpressions;
using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.ValueObjects;

/// <summary>
/// Sequential bank deposit document number. Format: REM-YYYY-NNNNNN.
/// </summary>
public sealed partial class BankDepositNumber : ValueObject
{
    public const string Prefix = "REM";

    public string Value { get; private set; }
    public int Year { get; private set; }
    public int Sequence { get; private set; }

    private BankDepositNumber()
    {
        Value = string.Empty;
        Year = 0;
        Sequence = 0;
    }

    private BankDepositNumber(string value, int year, int sequence)
    {
        Value = value;
        Year = year;
        Sequence = sequence;
    }

    public static Result<BankDepositNumber> Create(int year, int sequence)
    {
        if (year < 2000 || year > 2100)
            return Result.Failure<BankDepositNumber>(Error.Validation("Year", "L'année doit être comprise entre 2000 et 2100"));

        if (sequence < 1 || sequence > 999999)
            return Result.Failure<BankDepositNumber>(Error.Validation("Sequence", "Le numéro de séquence doit être entre 1 et 999999"));

        var formattedSequence = sequence.ToString("D6");
        var value = $"{Prefix}-{year}-{formattedSequence}";
        return Result.Success(new BankDepositNumber(value, year, sequence));
    }

    public static Result<BankDepositNumber> Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result.Failure<BankDepositNumber>(Error.Validation("BankDepositNumber", "Le numéro de remise est obligatoire"));

        var match = BankDepositNumberRegex().Match(value.Trim().ToUpperInvariant());
        if (!match.Success)
            return Result.Failure<BankDepositNumber>(Error.Validation(
                "BankDepositNumber",
                "Format invalide. Attendu : REM-YYYY-NNNNNN"));

        var year = int.Parse(match.Groups[1].Value);
        var sequence = int.Parse(match.Groups[2].Value);

        return Create(year, sequence);
    }

    public override string ToString() => Value;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    [GeneratedRegex(@"^REM-(\d{4})-(\d{6})$")]
    private static partial Regex BankDepositNumberRegex();
}
