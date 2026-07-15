using System.Text.RegularExpressions;
using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.ValueObjects;

/// <summary>
/// Represents a Tunisian Numéro d'Identification Fiscale (NIF).
/// Also known as Matricule Fiscal.
/// Format: NNNNNNN/L/A/M/NNN (ex: 1234567/A/B/C/000).
/// </summary>
public sealed partial class NIF : ValueObject
{
    private const string FullFormatPattern = @"^\d{7}/[A-Z]/[A-Z]/[A-Z]/\d{3}$";

    public string Value { get; private set; }

    public string IdentificationNumber => Value.Length >= 7 ? Value[..7] : string.Empty;
    public char TaxpayerCategory => Value.Length >= 9 ? Value[8] : ' ';
    public char MainActivity => Value.Length >= 11 ? Value[10] : ' ';
    public char SecondaryEstablishment => Value.Length >= 13 ? Value[12] : ' ';
    public string OfficeCode => Value.Length >= 17 ? Value[14..17] : string.Empty;

    // Required for EF Core
    private NIF()
    {
        Value = string.Empty;
    }

    private NIF(string value)
    {
        Value = value.ToUpperInvariant();
    }

    public static Result<NIF> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result.Failure<NIF>(Error.Validation("NIF", "Le NIF est obligatoire"));

        // Normalisation : trim, suppression des espaces, conversion en majuscules
        var normalizedValue = value.Trim().Replace(" ", "").ToUpperInvariant();

        if (!FullFormatRegex().IsMatch(normalizedValue))
            return Result.Failure<NIF>(Error.Validation("NIF",
                "Format du NIF invalide. Format attendu: NNNNNNN/L/A/M/NNN (ex: 1234567/A/B/C/000)."));

        return Result.Success(new NIF(normalizedValue));
    }

    public static bool IsValid(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim().Replace(" ", "").ToUpperInvariant();
        return FullFormatRegex().IsMatch(normalized);
    }

    public override string ToString() => Value;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    [GeneratedRegex(FullFormatPattern)]
    private static partial Regex FullFormatRegex();
}

/// <summary>
/// Taxpayer categories according to Tunisian tax law.
/// </summary>
public static class TaxpayerCategories
{
    public const char NaturalPerson = 'A';           // Personnes physiques
    public const char Partnership = 'B';              // Sociétés de personnes
    public const char Corporation = 'C';              // Sociétés de capitaux
    public const char Association = 'D';              // Associations
    public const char PublicEstablishment = 'E';      // Établissements publics
    public const char ForeignLegalEntity = 'F';       // Personnes morales étrangères
    public const char Other = 'G';                    // Autres

    public static string GetDescription(char category) => category switch
    {
        'A' => "Personne physique",
        'B' => "Société de personnes",
        'C' => "Société de capitaux",
        'D' => "Association",
        'E' => "Établissement public",
        'F' => "Personne morale étrangère",
        'G' => "Autre",
        _ => "Inconnu"
    };
}
