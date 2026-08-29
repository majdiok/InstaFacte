using System.Text.RegularExpressions;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Features.FixedAssets;

/// <summary>
/// Validation serveur (B5) des comptes comptables d'une immobilisation : format, préfixes NCT et
/// cohérence corporel/incorporel entre le compte d'actif, le compte d'amortissement cumulé et le
/// compte de dotation. Utilisée par <c>CreateFixedAssetCommandHandler</c> /
/// <c>UpdateFixedAssetCommandHandler</c> (brouillons uniquement) ainsi que par
/// <c>CreateFixedAssetsFromSupplierInvoiceHandler</c> (repli de ligne + garde défensive avant
/// création). Codes d'erreur et messages stables : le frontend (T10, bug C1) reproduit ces mêmes
/// règles avec les mêmes messages pour la parité client/serveur.
/// </summary>
public static class FixedAssetAccountRules
{
    private static readonly Regex AccountFormatRegex = new("^\\d{2,20}$", RegexOptions.Compiled);

    /// <summary>Chiffres uniquement, 2 à 20 caractères — invariant minimal partagé avec le domaine.</summary>
    public static bool IsValidFormat(string? account) =>
        !string.IsNullOrWhiteSpace(account) && AccountFormatRegex.IsMatch(account);

    /// <summary>
    /// Compte d'actif valide : classe 2 en <c>21x</c>/<c>22x</c> (<c>271</c> — frais préliminaires —
    /// toléré), jamais <c>28x</c> (amortissements) ni <c>29x</c> (provisions).
    /// </summary>
    public static bool IsValidAssetAccount(string? account)
    {
        if (!IsValidFormat(account))
            return false;

        if (account!.StartsWith("28", StringComparison.Ordinal) || account.StartsWith("29", StringComparison.Ordinal))
            return false;

        return account.StartsWith("21", StringComparison.Ordinal)
            || account.StartsWith("22", StringComparison.Ordinal)
            || account.StartsWith("271", StringComparison.Ordinal);
    }

    /// <summary>Compte d'amortissement cumulé valide : préfixe <c>281x</c> ou <c>282x</c>.</summary>
    public static bool IsValidDepreciationAccount(string? account) =>
        IsValidFormat(account)
        && (account!.StartsWith("281", StringComparison.Ordinal) || account.StartsWith("282", StringComparison.Ordinal));

    /// <summary>
    /// Compte de dotation valide : <c>68111</c>/<c>68112</c> exacts, ou préfixe <c>6811</c> toléré
    /// (sous-comptes de dotation plus fins).
    /// </summary>
    public static bool IsValidExpenseAccount(string? account) =>
        IsValidFormat(account) && account!.StartsWith("6811", StringComparison.Ordinal);

    /// <summary>
    /// Cohérence corporel/incorporel : un actif <c>21x</c> doit être amorti en <c>281x</c> avec une
    /// dotation <c>68111</c> ; un actif <c>22x</c> doit être amorti en <c>282x</c> avec une dotation
    /// <c>68112</c>. Le compte <c>271</c> (frais préliminaires, classe 27) n'est ni corporel ni
    /// incorporel : aucune règle de cohérence ne s'applique à ce préfixe.
    /// </summary>
    public static bool IsCoherentTriplet(string assetAccount, string depreciationAccount, string expenseAccount)
    {
        if (assetAccount.StartsWith("21", StringComparison.Ordinal))
            return depreciationAccount.StartsWith("281", StringComparison.Ordinal)
                && expenseAccount.StartsWith("68111", StringComparison.Ordinal);

        if (assetAccount.StartsWith("22", StringComparison.Ordinal))
            return depreciationAccount.StartsWith("282", StringComparison.Ordinal)
                && expenseAccount.StartsWith("68112", StringComparison.Ordinal);

        return true;
    }

    /// <summary>
    /// Valide le triplet complet (format + préfixes + cohérence) et renvoie la première violation
    /// rencontrée, avec un code d'erreur stable par champ.
    /// </summary>
    public static Result Validate(string? assetAccount, string? depreciationAccount, string? expenseAccount)
    {
        if (!IsValidAssetAccount(assetAccount))
            return Result.Failure(Error.Validation(
                "AssetAccountNumber",
                "Compte d'actif invalide : chiffres uniquement, préfixe attendu 21x ou 22x (271 toléré), jamais 28x ni 29x."));

        if (!IsValidDepreciationAccount(depreciationAccount))
            return Result.Failure(Error.Validation(
                "DepreciationAccountNumber",
                "Compte d'amortissement invalide : chiffres uniquement, préfixe attendu 281x ou 282x."));

        if (!IsValidExpenseAccount(expenseAccount))
            return Result.Failure(Error.Validation(
                "ExpenseAccountNumber",
                "Compte de dotation invalide : chiffres uniquement, préfixe attendu 68111/68112 (6811 toléré)."));

        if (!IsCoherentTriplet(assetAccount!, depreciationAccount!, expenseAccount!))
            return Result.Failure(Error.Validation(
                "AssetAccountNumber",
                "Incohérence comptable : un actif 21x doit être amorti en 281x avec une dotation 68111 ; un actif 22x doit être amorti en 282x avec une dotation 68112."));

        return Result.Success();
    }

    /// <summary>Vrai si les trois comptes forment un triplet valide (format + préfixes + cohérence).</summary>
    public static bool IsValidTriplet(string? assetAccount, string? depreciationAccount, string? expenseAccount) =>
        Validate(assetAccount, depreciationAccount, expenseAccount).IsSuccess;
}
