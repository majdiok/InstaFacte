namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Résout le numéro de compte auxiliaire SCE 425xxxx pour un salarié.
/// </summary>
public static class PayrollEmployeeAuxiliaryAccountResolver
{
    public const string PersonnelPayableParentAccount = PayrollJournalEntryBuilder.PersonnelPayableAccount;
    public const int MaxAccountNumberLength = 10;

    /// <summary>
    /// Génère un compte auxiliaire 425 + matricule numérique zero-paddé (ex. 4250001).
    /// </summary>
    public static string Resolve(string employeeNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeNumber);

        // R-15 : un compte auxiliaire SCE doit être strictement numérique. L'ancien repli
        // alphanumérique émettait des lettres (ex. 42500AB12) — refusé. On n'accepte que les
        // chiffres du matricule ; à défaut, on lève (les appelants de validation renvoient un
        // Result explicite plutôt que de produire un compte invalide).
        var digits = new string(employeeNumber.Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
            throw new ArgumentException(
                "Le matricule salarié doit contenir au moins un chiffre pour générer le compte auxiliaire 425 ; "
                + "les caractères alphabétiques ne sont pas acceptés en SCE.", nameof(employeeNumber));

        var suffixLength = MaxAccountNumberLength - PersonnelPayableParentAccount.Length;
        if (suffixLength <= 0)
            throw new InvalidOperationException("Le préfixe du compte personnel est trop long.");

        var suffix = digits.Length <= suffixLength
            ? digits.PadLeft(suffixLength, '0')
            : digits[^suffixLength..];

        return PersonnelPayableParentAccount + suffix;
    }

    /// <summary>
    /// R-15 : indique si le matricule contient au moins un chiffre (précondition de <see cref="Resolve"/>),
    /// sans lever. Utilisé par la validation pour produire un message d'erreur métier nominatif.
    /// </summary>
    public static bool CanResolve(string employeeNumber)
        => !string.IsNullOrWhiteSpace(employeeNumber) && employeeNumber.Any(char.IsDigit);
}
