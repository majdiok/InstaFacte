using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.ValueObjects;

/// <summary>
/// Code-barres article au format EAN-8 ou EAN-13 (GTIN-8 / GTIN-13).
///
/// La clé de contrôle est vérifiée à la création : un code mal recopié est refusé à la
/// saisie plutôt que de dormir au catalogue jusqu'à ce qu'un scan tombe à côté.
///
/// Le produit n'en portait aucun jusqu'ici : le scan du point de vente cherchait dans le
/// CODE PRODUIT interne, avec repli sur une correspondance approchée — un scan pouvait donc
/// encaisser un autre article.
/// </summary>
public sealed class Barcode : ValueObject
{
    public string Value { get; }

    /// <summary>8 ou 13.</summary>
    public int Length => Value.Length;

    private Barcode(string value) => Value = value;

    // Requis par EF Core
    private Barcode() => Value = string.Empty;

    public static Result<Barcode> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result.Failure<Barcode>(Error.Validation("Barcode", "Le code-barres est obligatoire"));

        var normalized = value.Trim();

        if (!normalized.All(char.IsAsciiDigit))
            return Result.Failure<Barcode>(Error.Validation("Barcode", "Le code-barres ne doit contenir que des chiffres"));

        if (normalized.Length is not (8 or 13))
            return Result.Failure<Barcode>(Error.Validation("Barcode",
                $"Le code-barres doit comporter 8 ou 13 chiffres (reçu : {normalized.Length})"));

        if (!HasValidCheckDigit(normalized))
            return Result.Failure<Barcode>(Error.Validation("Barcode",
                "Clé de contrôle invalide — vérifiez la saisie du code-barres"));

        return Result.Success(new Barcode(normalized));
    }

    /// <summary>Vrai si la chaîne est un EAN-8/EAN-13 valide, clé de contrôle comprise.</summary>
    public static bool IsValid(string? value) => Create(value).IsSuccess;

    /// <summary>
    /// Vérifie la clé de contrôle GTIN : somme pondérée 3/1 en partant de la droite, le
    /// dernier chiffre devant compléter à la dizaine supérieure.
    /// </summary>
    public static bool HasValidCheckDigit(string digits)
    {
        if (string.IsNullOrEmpty(digits) || !digits.All(char.IsAsciiDigit))
            return false;

        var sum = 0;
        // On parcourt de l'avant-dernier chiffre vers le premier ; le poids alterne 3, 1, 3, 1…
        for (int i = digits.Length - 2, weight = 3; i >= 0; i--, weight = weight == 3 ? 1 : 3)
            sum += (digits[i] - '0') * weight;

        var expected = (10 - (sum % 10)) % 10;
        return expected == digits[^1] - '0';
    }

    /// <summary>
    /// Calcule la clé de contrôle d'un code partiel (7 ou 12 chiffres). Sert à générer un
    /// code-barres interne valide pour un article sans EAN fournisseur.
    /// </summary>
    public static Result<Barcode> FromBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return Result.Failure<Barcode>(Error.Validation("Barcode", "Le corps du code-barres est obligatoire"));

        var normalized = body.Trim();

        if (!normalized.All(char.IsAsciiDigit))
            return Result.Failure<Barcode>(Error.Validation("Barcode", "Le corps ne doit contenir que des chiffres"));

        if (normalized.Length is not (7 or 12))
            return Result.Failure<Barcode>(Error.Validation("Barcode",
                "Le corps doit comporter 7 chiffres (EAN-8) ou 12 chiffres (EAN-13)"));

        var sum = 0;
        for (int i = normalized.Length - 1, weight = 3; i >= 0; i--, weight = weight == 3 ? 1 : 3)
            sum += (normalized[i] - '0') * weight;

        var check = (10 - (sum % 10)) % 10;
        return Create(normalized + check);
    }

    public override string ToString() => Value;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public static implicit operator string(Barcode barcode) => barcode.Value;
}
