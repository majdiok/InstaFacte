namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Résout le numéro de compte auxiliaire SCE 421xxxx pour un salarié.
/// </summary>
public static class PayrollEmployeeAuxiliaryAccountResolver
{
    public const string PersonnelPayableParentAccount = PayrollJournalEntryBuilder.PersonnelPayableAccount;
    public const int MaxAccountNumberLength = 10;

    /// <summary>
    /// Génère un compte auxiliaire 421 + matricule numérique zero-paddé (ex. 4210001).
    /// </summary>
    public static string Resolve(string employeeNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeNumber);

        var digits = new string(employeeNumber.Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
            digits = new string(employeeNumber.Where(char.IsLetterOrDigit).ToArray());

        if (digits.Length == 0)
            throw new ArgumentException("Le matricule salarié doit contenir au moins un caractère alphanumérique.", nameof(employeeNumber));

        var suffixLength = MaxAccountNumberLength - PersonnelPayableParentAccount.Length;
        if (suffixLength <= 0)
            throw new InvalidOperationException("Le préfixe du compte personnel est trop long.");

        var suffix = digits.Length <= suffixLength
            ? digits.PadLeft(suffixLength, '0')
            : digits[^suffixLength..];

        return PersonnelPayableParentAccount + suffix;
    }
}
