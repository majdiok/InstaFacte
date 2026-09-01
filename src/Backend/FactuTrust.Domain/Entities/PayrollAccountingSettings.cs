using System.Text.RegularExpressions;

using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Paramètres d'imputation comptable de la paie pour un dossier (tenant) — singleton par tenant.
/// </summary>
/// <remarks>
/// <para>
/// Le profil d'imputation (<see cref="PayrollAccountProfile"/>) était jusqu'ici une configuration
/// globale (<c>AccountingSettings</c> / <c>appsettings</c>), donc commune à tous les dossiers d'un
/// cabinet. Cette entité le rend décidable dossier par dossier ; l'absence de ligne vaut « repli sur
/// la configuration globale », de sorte qu'un dossier non paramétré conserve exactement son
/// comportement actuel.
/// </para>
/// <para>
/// <b>Invariant central</b> : passer à <see cref="PayrollAccountProfile.Sce2026"/> exige une date
/// d'effet. Sans elle, le profil s'appliquerait rétroactivement à tous les cycles — rouvrir puis
/// revalider un cycle ancien réécrirait son imputation, alors que la bascule est explicitement « au
/// fil de l'eau ». La date doit être le premier jour d'un mois : le profil se résout sur la période
/// du cycle, pas sur une date de traitement.
/// </para>
/// </remarks>
public sealed class PayrollAccountingSettings : Entity
{
    /// <summary>Longueur maximale d'un numéro de compte, alignée sur <c>ChartOfAccounts</c>.</summary>
    public const int MaxAccountNumberLength = 20;

    /// <summary>
    /// Forme d'un numéro de compte SCE, identique à <c>CreateSubAccountCommandValidator</c> :
    /// chiffres, classes 1 à 7, points autorisés pour les sous-comptes de l'overlay métier
    /// (421.1, 428.1…). Dupliquée ici parce que le domaine ne référence pas la couche Application ;
    /// elle protège la persistance d'un numéro qui finirait sinon tel quel dans une ligne d'écriture
    /// (<c>ChartOfAccount.Create</c> ne valide pas la forme du numéro).
    /// </summary>
    public const string AccountNumberPattern = @"^[1-7]\d*(?:\.\d+)*$";

    private static readonly Regex AccountNumberRegex =
        new(AccountNumberPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public PayrollAccountProfile AccountProfile { get; private set; } = PayrollAccountProfile.Legacy;

    /// <summary>Premier jour du mois à partir duquel <see cref="AccountProfile"/> s'applique.</summary>
    public DateTime? AccountProfileEffectiveDate { get; private set; }

    /// <summary>Compte de compensation des avantages en nature (retenue salarié non décaissée).</summary>
    public string? InKindOffsetAccount { get; private set; }

    /// <summary>Génère l'OD de décaissement à l'octroi d'une avance / d'un prêt salarié.</summary>
    public bool DisbursementEntriesEnabled { get; private set; }

    /// <summary>Ventile le débit 640 en sous-comptes 6400/6401/6402/6403/6404 selon la nature du gain.</summary>
    public bool DetailedSalarySplitEnabled { get; private set; }

    /// <summary>
    /// Ventile le crédit 425 par salarié (comptes auxiliaires) au lieu d'une seule ligne agrégée.
    /// </summary>
    /// <remarks>
    /// Vrai par défaut, comme la configuration globale qu'il remplace : le suivi et le lettrage par
    /// salarié restent le comportement normal. Le mettre à faux ramène l'OD à une ligne 425 unique.
    /// </remarks>
    public bool EmployeeAuxiliaryEnabled { get; private set; } = true;

    private PayrollAccountingSettings() { }

    public static Result<PayrollAccountingSettings> Create(
        PayrollAccountProfile accountProfile,
        DateTime? accountProfileEffectiveDate,
        string? inKindOffsetAccount,
        bool disbursementEntriesEnabled,
        bool detailedSalarySplitEnabled,
        bool employeeAuxiliaryEnabled = true)
    {
        var validation = Validate(accountProfile, accountProfileEffectiveDate, inKindOffsetAccount);
        if (validation.IsFailure)
            return Result.Failure<PayrollAccountingSettings>(validation.Error);

        return Result.Success(new PayrollAccountingSettings
        {
            AccountProfile = accountProfile,
            AccountProfileEffectiveDate = accountProfileEffectiveDate?.Date,
            InKindOffsetAccount = Normalize(inKindOffsetAccount),
            DisbursementEntriesEnabled = disbursementEntriesEnabled,
            DetailedSalarySplitEnabled = detailedSalarySplitEnabled,
            EmployeeAuxiliaryEnabled = employeeAuxiliaryEnabled
        });
    }

    public Result Update(
        PayrollAccountProfile accountProfile,
        DateTime? accountProfileEffectiveDate,
        string? inKindOffsetAccount,
        bool disbursementEntriesEnabled,
        bool detailedSalarySplitEnabled,
        bool employeeAuxiliaryEnabled = true)
    {
        var validation = Validate(accountProfile, accountProfileEffectiveDate, inKindOffsetAccount);
        if (validation.IsFailure)
            return validation;

        AccountProfile = accountProfile;
        AccountProfileEffectiveDate = accountProfileEffectiveDate?.Date;
        InKindOffsetAccount = Normalize(inKindOffsetAccount);
        DisbursementEntriesEnabled = disbursementEntriesEnabled;
        DetailedSalarySplitEnabled = detailedSalarySplitEnabled;
        EmployeeAuxiliaryEnabled = employeeAuxiliaryEnabled;
        return Result.Success();
    }

    private static Result Validate(
        PayrollAccountProfile accountProfile,
        DateTime? accountProfileEffectiveDate,
        string? inKindOffsetAccount)
    {
        if (!Enum.IsDefined(accountProfile))
        {
            return Result.Failure(Error.Validation(
                "AccountProfile", "Profil d'imputation comptable inconnu."));
        }

        if (accountProfileEffectiveDate is { } effective && effective.Day != 1)
        {
            return Result.Failure(Error.Validation(
                "AccountProfileEffectiveDate",
                "La date de bascule doit être le premier jour d'un mois : le profil se résout sur la "
                + "période du cycle de paie, pas sur une date de traitement."));
        }

        if (accountProfile == PayrollAccountProfile.Sce2026 && accountProfileEffectiveDate is null)
        {
            return Result.Failure(Error.Validation(
                "AccountProfileEffectiveDate",
                "Le profil SCE 2026 exige une date de bascule : sans elle il s'appliquerait aussi aux "
                + "cycles antérieurs, dont la réouverture réécrirait l'imputation d'origine."));
        }

        var account = Normalize(inKindOffsetAccount);
        if (account is null)
            return Result.Success();

        if (account.Length > MaxAccountNumberLength)
        {
            return Result.Failure(Error.Validation(
                "InKindOffsetAccount",
                $"Le compte de compensation ne peut pas dépasser {MaxAccountNumberLength} caractères."));
        }

        if (!AccountNumberRegex.IsMatch(account))
        {
            return Result.Failure(Error.Validation(
                "InKindOffsetAccount",
                "Le compte de compensation doit être un numéro SCE (chiffres, classes 1 à 7, points autorisés)."));
        }

        return Result.Success();
    }

    private static string? Normalize(string? accountNumber) =>
        string.IsNullOrWhiteSpace(accountNumber) ? null : accountNumber.Trim();
}
