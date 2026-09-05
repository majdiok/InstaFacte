using System.Text.RegularExpressions;

using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Services.Accounting;

/// <summary>
/// Règle unique de forme d'un numéro de compte du plan comptable (NCT 01).
/// </summary>
/// <remarks>
/// <para>
/// <b>Invariant métier : au plus <see cref="MaxDigits"/> chiffres.</b> On compte les caractères
/// numériques, pas la longueur de la chaîne : les séparateurs de l'overlay métier (421.1, 428.1)
/// ne consomment pas le budget. <c>421.1</c> vaut donc 4 chiffres et reste valide, tandis que
/// <c>4259655554</c> en vaut 10 et est refusé.
/// </para>
/// <para>
/// Cette classe est l'unique source de vérité. Avant elle, le pattern était recopié dans
/// <c>CreateSubAccountCommandValidator</c>, <c>PayrollAccountingSettings</c> et le composant
/// Angular du plan comptable, avec trois plafonds différents (20, 20, aucun) — et aucun des trois
/// ne protégeait <see cref="Entities.ChartOfAccount.Create"/>, que les chemins d'auto-création
/// appellent directement.
/// </para>
/// </remarks>
public static class AccountNumberRules
{
    /// <summary>Nombre maximal de chiffres d'un numéro de compte.</summary>
    public const int MaxDigits = 8;

    /// <summary>
    /// Forme d'un numéro SCE : classe 1 à 7, chiffres, segments optionnels après un point.
    /// </summary>
    public const string Pattern = @"^[1-7]\d*(?:\.\d+)*$";

    private static readonly Regex FormatRegex =
        new(Pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Nombre de chiffres du numéro, séparateurs exclus.</summary>
    public static int DigitCount(string? accountNumber)
    {
        if (string.IsNullOrEmpty(accountNumber))
            return 0;

        var count = 0;
        foreach (var c in accountNumber)
        {
            if (char.IsAsciiDigit(c))
                count++;
        }

        return count;
    }

    /// <summary>Vrai si le numéro respecte la forme SCE et le plafond de chiffres.</summary>
    public static bool IsWellFormed(string? accountNumber)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
            return false;

        var trimmed = accountNumber.Trim();
        return FormatRegex.IsMatch(trimmed) && DigitCount(trimmed) <= MaxDigits;
    }

    /// <summary>
    /// Valide le numéro et renvoie une erreur nominative — le compte fautif et son nombre de
    /// chiffres — pour que le message remonte tel quel à l'utilisateur.
    /// </summary>
    /// <param name="field">Nom du champ porté par l'erreur (« AccountNumber » par défaut).</param>
    public static Result Validate(string? accountNumber, string field = "AccountNumber")
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
            return Result.Failure(Error.Validation(field, "Le numéro de compte est obligatoire."));

        var trimmed = accountNumber.Trim();

        if (!FormatRegex.IsMatch(trimmed))
        {
            return Result.Failure(Error.Validation(
                field,
                $"« {trimmed} » n'est pas un numéro de compte SCE valide : chiffres uniquement, "
                + "classe 1 à 7, points autorisés pour les sous-comptes (ex. 421.1)."));
        }

        var digits = DigitCount(trimmed);
        if (digits > MaxDigits)
        {
            return Result.Failure(Error.Validation(
                field,
                $"Le compte « {trimmed} » comporte {digits} chiffres : un numéro de compte ne peut "
                + $"pas en dépasser {MaxDigits}."));
        }

        return Result.Success();
    }
}
