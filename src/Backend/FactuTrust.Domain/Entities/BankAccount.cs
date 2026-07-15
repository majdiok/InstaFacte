using System.Text.RegularExpressions;
using FactuTrust.Domain.Banking;
using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Bank account for treasury (Tunisian RIB / IBAN, optional SWIFT).
/// Scoped to a <see cref="Company"/> (seller).
/// </summary>
public sealed partial class BankAccount : AggregateRoot
{
    public Guid CompanyId { get; private set; }

    /// <summary>Catalog code (e.g. BIAT) or AUTRE.</summary>
    public string BankCode { get; private set; } = null!;

    public string BankName { get; private set; } = null!;

    public string? Designation { get; private set; }

    public string? AgencyName { get; private set; }

    /// <summary>20-digit RIB, digits only.</summary>
    public string Rib { get; private set; } = null!;

    /// <summary>24-char Tunisian IBAN without spaces (TN + 22 digits).</summary>
    public string Iban { get; private set; } = null!;

    public string? SwiftBic { get; private set; }

    public bool IsDefault { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>Compte du plan comptable (532x) associé à ce RIB pour le rapprochement.</summary>
    public string? ChartOfAccountNumber { get; private set; }

    /// <summary>Devise du compte (TND par défaut).</summary>
    public string Currency { get; private set; } = "TND";

    private BankAccount() { }

    public static Result<BankAccount> Create(
        Guid companyId,
        string bankCode,
        string bankName,
        string rib,
        string iban,
        string? designation = null,
        string? agencyName = null,
        string? swiftBic = null,
        bool isDefault = false)
    {
        if (companyId == Guid.Empty)
            return Result.Failure<BankAccount>(Error.Validation("CompanyId", "L'entreprise est obligatoire"));

        var normalizedRib = NormalizeDigits(rib);
        var normalizedIban = NormalizeIban(iban);

        var validate = ValidateBankingFields(bankCode, bankName, normalizedRib, normalizedIban, swiftBic, designation, agencyName);
        if (validate.IsFailure)
            return Result.Failure<BankAccount>(validate.Error);

        var account = new BankAccount
        {
            CompanyId = companyId,
            BankCode = bankCode.Trim().ToUpperInvariant(),
            BankName = bankName.Trim(),
            Designation = string.IsNullOrWhiteSpace(designation) ? null : designation.Trim(),
            AgencyName = string.IsNullOrWhiteSpace(agencyName) ? null : agencyName.Trim(),
            Rib = normalizedRib,
            Iban = normalizedIban,
            SwiftBic = NormalizeSwift(swiftBic),
            IsDefault = isDefault,
            IsActive = true
        };

        return Result.Success(account);
    }

    public Result Update(
        string bankCode,
        string bankName,
        string rib,
        string iban,
        string? designation = null,
        string? agencyName = null,
        string? swiftBic = null)
    {
        var normalizedRib = NormalizeDigits(rib);
        var normalizedIban = NormalizeIban(iban);

        var validate = ValidateBankingFields(bankCode, bankName, normalizedRib, normalizedIban, swiftBic, designation, agencyName);
        if (validate.IsFailure)
            return validate;

        BankCode = bankCode.Trim().ToUpperInvariant();
        BankName = bankName.Trim();
        Designation = string.IsNullOrWhiteSpace(designation) ? null : designation.Trim();
        AgencyName = string.IsNullOrWhiteSpace(agencyName) ? null : agencyName.Trim();
        Rib = normalizedRib;
        Iban = normalizedIban;
        SwiftBic = NormalizeSwift(swiftBic);
        IncrementVersion();
        return Result.Success();
    }

    public void SetAsDefault()
    {
        IsDefault = true;
        IncrementVersion();
    }

    public void RemoveDefault()
    {
        IsDefault = false;
        IncrementVersion();
    }

    public void Deactivate()
    {
        IsActive = false;
        IncrementVersion();
    }

    public Result SetChartOfAccountNumber(string? accountNumber)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
        {
            ChartOfAccountNumber = null;
            IncrementVersion();
            return Result.Success();
        }

        var normalized = accountNumber.Trim();
        if (!normalized.StartsWith("532", StringComparison.Ordinal))
            return Result.Failure(Error.Validation("ChartOfAccountNumber",
                "Le compte comptable lié doit appartenir à la classe banques (532…)."));

        if (normalized.Length > 20)
            return Result.Failure(Error.Validation("ChartOfAccountNumber", "Numéro de compte comptable trop long."));

        ChartOfAccountNumber = normalized;
        IncrementVersion();
        return Result.Success();
    }

    public void SetCurrency(string currency)
    {
        Currency = string.IsNullOrWhiteSpace(currency) ? "TND" : currency.Trim().ToUpperInvariant();
        IncrementVersion();
    }

    private static Result ValidateBankingFields(
        string bankCode,
        string bankName,
        string ribDigits,
        string ibanClean,
        string? swiftBic,
        string? designation,
        string? agencyName)
    {
        if (string.IsNullOrWhiteSpace(bankCode))
            return Result.Failure(Error.Validation("BankCode", "La banque est obligatoire"));

        if (bankCode.Trim().Length > 32)
            return Result.Failure(Error.Validation("BankCode", "Le code banque est trop long"));

        if (string.IsNullOrWhiteSpace(bankName))
            return Result.Failure(Error.Validation("BankName", "Le nom de la banque est obligatoire"));

        if (bankName.Trim().Length > 100)
            return Result.Failure(Error.Validation("BankName", "Le nom de la banque ne peut pas dépasser 100 caractères"));

        if (designation != null && designation.Trim().Length > 200)
            return Result.Failure(Error.Validation("Designation", "La désignation ne peut pas dépasser 200 caractères"));

        if (agencyName != null && agencyName.Trim().Length > 200)
            return Result.Failure(Error.Validation("AgencyName", "Le nom d'agence ne peut pas dépasser 200 caractères"));

        if (ribDigits.Length != 20 || !RibDigitsRegex().IsMatch(ribDigits))
            return Result.Failure(Error.Validation("Rib", "Le RIB doit contenir exactement 20 chiffres"));

        if (ibanClean.Length != 24 || !TunisianIbanRegex().IsMatch(ibanClean))
            return Result.Failure(Error.Validation("Iban", "L'IBAN tunisien doit être au format TN suivi de 22 chiffres (24 caractères au total)"));

        // Last 20 digits of IBAN (after country code + check digits) must match RIB
        var ibanAccountPart = ibanClean.Substring(4);
        if (!string.Equals(ibanAccountPart, ribDigits, StringComparison.Ordinal))
            return Result.Failure(Error.Validation("Rib", "Le RIB ne correspond pas à l'IBAN"));

        var swift = NormalizeSwift(swiftBic);
        if (swift != null && !SwiftBicRegex().IsMatch(swift))
            return Result.Failure(Error.Validation("SwiftBic", "Le code SWIFT/BIC est invalide"));

        return Result.Success();
    }

    private static string NormalizeDigits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        return new string(value.Where(char.IsDigit).ToArray());
    }

    private static string NormalizeIban(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        var cleaned = new string(value.Where(c => char.IsLetterOrDigit(c)).ToArray()).ToUpperInvariant();
        return cleaned;
    }

    private static string? NormalizeSwift(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var s = value.Trim().ToUpperInvariant().Replace(" ", "");
        return s.Length == 0 ? null : s;
    }

    [GeneratedRegex(@"^\d{20}$", RegexOptions.Compiled)]
    private static partial Regex RibDigitsRegex();

    [GeneratedRegex(@"^TN\d{22}$", RegexOptions.Compiled)]
    private static partial Regex TunisianIbanRegex();

    /// <summary>ISO 9362 BIC: 8 or 11 characters.</summary>
    [GeneratedRegex(@"^[A-Z]{6}[A-Z0-9]{2}([A-Z0-9]{3})?$", RegexOptions.Compiled)]
    private static partial Regex SwiftBicRegex();
}
