namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Décrit la <b>forme historique</b> d'un compte auxiliaire salarié : <c>425</c> + les 7 derniers
/// chiffres du matricule.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ne plus utiliser pour créer un compte.</b> Cette dérivation a été retirée de tous les chemins
/// d'écriture (validation de cycle, génération de l'OD, règlement, création de salarié) pour deux
/// raisons : elle produisait un numéro de <b>10 chiffres</b>, au-delà du plafond de
/// <see cref="Accounting.AccountNumberRules.MaxDigits"/>, et la troncature aux 7 derniers chiffres
/// faisait collisionner deux matricules — « 1 » et « 0000001 », ou deux CIN de même queue — dont les
/// dettes de salaire se confondaient alors sur un seul compte.
/// </para>
/// <para>
/// Le compte est désormais <b>alloué</b> séquentiellement
/// (<c>PayrollEmployeeChartProvisioningService</c>, <c>425</c> + 4 chiffres) et porté par la fiche
/// salarié. Cette classe ne sert plus qu'à <b>reconnaître</b> un compte hérité, pour le rapprocher
/// de son matricule d'origine dans un diagnostic ou une reprise de données.
/// </para>
/// </remarks>
public static class PayrollEmployeeAuxiliaryAccountResolver
{
    public const string PersonnelPayableParentAccount = PayrollJournalEntryBuilder.PersonnelPayableAccount;

    /// <summary>Longueur de la forme héritée : <c>425</c> + 7 chiffres.</summary>
    public const int LegacyAccountNumberLength = 10;

    /// <summary>
    /// Recalcule le compte hérité qu'un matricule aurait produit. Réservé à la reconnaissance de
    /// l'existant — voir les remarques de la classe.
    /// </summary>
    public static string ResolveLegacy(string employeeNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeNumber);

        // Un compte SCE est strictement numérique : l'ancien repli alphanumérique émettait des
        // lettres (ex. 42500AB12), refusées. On ne retient que les chiffres du matricule.
        var digits = new string(employeeNumber.Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
            throw new ArgumentException(
                "Le matricule salarié doit contenir au moins un chiffre pour retrouver la forme héritée "
                + "du compte auxiliaire 425 ; les caractères alphabétiques ne sont pas acceptés en SCE.",
                nameof(employeeNumber));

        var suffixLength = LegacyAccountNumberLength - PersonnelPayableParentAccount.Length;
        var suffix = digits.Length <= suffixLength
            ? digits.PadLeft(suffixLength, '0')
            : digits[^suffixLength..];

        return PersonnelPayableParentAccount + suffix;
    }

    /// <summary>
    /// Indique si le matricule contient au moins un chiffre, précondition de
    /// <see cref="ResolveLegacy"/>, sans lever.
    /// </summary>
    public static bool CanResolveLegacy(string employeeNumber)
        => !string.IsNullOrWhiteSpace(employeeNumber) && employeeNumber.Any(char.IsDigit);
}
