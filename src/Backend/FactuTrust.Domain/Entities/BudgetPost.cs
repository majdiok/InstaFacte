using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Poste budgétaire : regroupe des comptes du plan par préfixes (ex. « Services extérieurs » = 61;62).
/// Le réalisé agrège les comptes commençant par l'un des préfixes ; en cas de chevauchement entre
/// postes, le préfixe le plus long gagne (un compte n'est compté qu'une seule fois).
/// </summary>
public sealed class BudgetPost : Entity
{
    public string Code { get; private set; } = null!;
    public string Label { get; private set; } = null!;
    public BudgetPostKind Kind { get; private set; }

    /// <summary>Préfixes de comptes séparés par « ; » (ex. « 61;62 »), normalisés par la factory.</summary>
    public string AccountPrefixes { get; private set; } = null!;

    public bool IsActive { get; private set; }

    /// <summary>Ordre d'affichage dans la grille de saisie et les états.</summary>
    public int DisplayOrder { get; private set; }

    private BudgetPost() { }

    public static Result<BudgetPost> Create(
        string code, string label, BudgetPostKind kind, string accountPrefixes, int displayOrder = 0)
    {
        code = code?.Trim().ToUpperInvariant() ?? string.Empty;
        if (string.IsNullOrEmpty(code))
            return Result.Failure<BudgetPost>(Error.Validation("Code", "Le code du poste budgétaire est obligatoire."));
        if (code.Length > 20)
            return Result.Failure<BudgetPost>(Error.Validation("Code", "Le code ne peut pas dépasser 20 caractères."));

        label = label?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(label))
            return Result.Failure<BudgetPost>(Error.Validation("Label", "Le libellé est obligatoire."));
        if (label.Length > 200)
            return Result.Failure<BudgetPost>(Error.Validation("Label", "Le libellé ne peut pas dépasser 200 caractères."));

        var prefixes = ParsePrefixes(accountPrefixes);
        if (prefixes.IsFailure)
            return Result.Failure<BudgetPost>(prefixes.Error);

        return Result.Success(new BudgetPost
        {
            Code = code,
            Label = label,
            Kind = kind,
            AccountPrefixes = string.Join(';', prefixes.Value),
            IsActive = true,
            DisplayOrder = displayOrder
        });
    }

    public Result Update(string label, BudgetPostKind kind, string accountPrefixes, int displayOrder)
    {
        label = label?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(label))
            return Result.Failure(Error.Validation("Label", "Le libellé est obligatoire."));
        if (label.Length > 200)
            return Result.Failure(Error.Validation("Label", "Le libellé ne peut pas dépasser 200 caractères."));

        var prefixes = ParsePrefixes(accountPrefixes);
        if (prefixes.IsFailure)
            return Result.Failure(prefixes.Error);

        Label = label;
        Kind = kind;
        AccountPrefixes = string.Join(';', prefixes.Value);
        DisplayOrder = displayOrder;
        return Result.Success();
    }

    public void ToggleActive() => IsActive = !IsActive;

    /// <summary>Préfixes normalisés du poste (déjà validés par la factory).</summary>
    public IReadOnlyList<string> GetPrefixes() =>
        AccountPrefixes.Split(';', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Valide et normalise une liste de préfixes « ; » : chaque segment = 1 à 8 chiffres,
    /// doublons refusés, espaces tolérés.
    /// </summary>
    public static Result<IReadOnlyList<string>> ParsePrefixes(string? accountPrefixes)
    {
        var raw = (accountPrefixes ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (raw.Length == 0)
            return Result.Failure<IReadOnlyList<string>>(Error.Validation(
                "AccountPrefixes", "Au moins un préfixe de compte est requis (ex. « 61;62 »)."));

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var prefix in raw)
        {
            if (prefix.Length > 8 || !prefix.All(char.IsAsciiDigit))
                return Result.Failure<IReadOnlyList<string>>(Error.Validation(
                    "AccountPrefixes", $"Préfixe invalide « {prefix} » : 1 à 8 chiffres attendus."));
            if (!seen.Add(prefix))
                return Result.Failure<IReadOnlyList<string>>(Error.Validation(
                    "AccountPrefixes", $"Préfixe en doublon : « {prefix} »."));
        }

        var normalized = string.Join(';', raw);
        if (normalized.Length > 200)
            return Result.Failure<IReadOnlyList<string>>(Error.Validation(
                "AccountPrefixes", "La liste des préfixes ne peut pas dépasser 200 caractères."));

        return Result.Success<IReadOnlyList<string>>(raw);
    }
}
