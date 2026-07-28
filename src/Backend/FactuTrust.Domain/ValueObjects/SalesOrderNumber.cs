using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.ValueObjects;

/// <summary>
/// Numéro de commande client, séquentiel et unique.
/// Format : {PREFIXE}-{ANNÉE}-{SÉQUENCE} — exemple : CDE-2026-000001.
///
/// Calqué sur <see cref="PurchaseOrderNumber"/> : même format, même mécanique de réservation
/// atomique par <c>IDocumentNumberService</c>, pour que la commande client s'inscrive dans la
/// numérotation documentaire existante plutôt que d'en inventer une seconde.
/// </summary>
public sealed class SalesOrderNumber : ValueObject
{
    public string Value { get; private set; }
    public string Prefix { get; private set; }
    public int Year { get; private set; }
    public int Sequence { get; private set; }

    // Requis par EF Core
    private SalesOrderNumber()
    {
        Value = string.Empty;
        Prefix = string.Empty;
        Year = 0;
        Sequence = 0;
    }

    private SalesOrderNumber(string value, string prefix, int year, int sequence)
    {
        Value = value;
        Prefix = prefix;
        Year = year;
        Sequence = sequence;
    }

    public static SalesOrderNumber Create(string prefix, int year, int sequence)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentException("Le préfixe est obligatoire", nameof(prefix));

        if (year < 2000 || year > 2100)
            throw new ArgumentException("Année invalide", nameof(year));

        if (sequence < 1)
            throw new ArgumentException("Le numéro de séquence doit être positif", nameof(sequence));

        var normalizedPrefix = prefix.Trim().ToUpperInvariant();
        var value = $"{normalizedPrefix}-{year}-{sequence:D6}";

        return new SalesOrderNumber(value, normalizedPrefix, year, sequence);
    }

    public static Result<SalesOrderNumber> Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result.Failure<SalesOrderNumber>(Error.Validation("SalesOrderNumber", "Le numéro de commande est obligatoire"));

        var parts = value.Split('-');
        if (parts.Length != 3)
            return Result.Failure<SalesOrderNumber>(Error.Validation("SalesOrderNumber",
                "Format de numéro de commande invalide. Format attendu : PREFIXE-ANNÉE-NUMÉRO"));

        if (!int.TryParse(parts[1], out var year))
            return Result.Failure<SalesOrderNumber>(Error.Validation("SalesOrderNumber", "Année invalide"));

        if (!int.TryParse(parts[2], out var sequence))
            return Result.Failure<SalesOrderNumber>(Error.Validation("SalesOrderNumber", "Numéro de séquence invalide"));

        try
        {
            return Result.Success(Create(parts[0], year, sequence));
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<SalesOrderNumber>(Error.Validation("SalesOrderNumber", ex.Message));
        }
    }

    /// <summary>Crée un numéro à partir d'un format rendu par un schéma de numérotation.</summary>
    public static SalesOrderNumber FromRendered(string value, string prefix, int year, int sequence) =>
        new(value.Trim().ToUpperInvariant(), prefix.Trim().ToUpperInvariant(), year, sequence);

    public SalesOrderNumber Next() => Create(Prefix, Year, Sequence + 1);

    public override string ToString() => Value;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public static implicit operator string(SalesOrderNumber number) => number.Value;
}
