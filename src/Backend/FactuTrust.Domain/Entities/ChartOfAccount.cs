using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// SCE (Tunisia) chart of accounts row.
/// </summary>
public sealed class ChartOfAccount : Entity
{
    public string AccountNumber { get; private set; } = null!;
    public string Label { get; private set; } = null!;
    public int AccountClass { get; private set; }
    public string? ParentAccountNumber { get; private set; }
    public AccountNatureType NatureType { get; private set; }
    public bool IsSystem { get; private set; }
    public bool IsActive { get; private set; }
    public int Level { get; private set; }

    // ── Attributs « façon Axeane » (additifs, défauts rétro-compatibles). ──
    /// <summary>Nature métier du compte (général/client/fournisseur/autre).</summary>
    public AccountType AccountType { get; private set; } = AccountType.General;
    /// <summary>Vrai si le compte est un compte auxiliaire (rattaché à un compte collectif).</summary>
    public bool IsAuxiliary { get; private set; }
    /// <summary>Compte d'affectation (compte collectif de rattachement) pour un compte auxiliaire.</summary>
    public string? AffectationAccountNumber { get; private set; }

    private ChartOfAccount() { }

    public static Result<ChartOfAccount> Create(
        string accountNumber,
        string label,
        int accountClass,
        string? parentAccountNumber,
        AccountNatureType natureType,
        bool isSystem = false,
        AccountType accountType = AccountType.General,
        bool isAuxiliary = false,
        string? affectationAccountNumber = null)
    {
        accountNumber = accountNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(accountNumber))
            return Result.Failure<ChartOfAccount>(Error.Validation("AccountNumber", "Le numéro de compte est obligatoire"));

        if (accountClass is < 1 or > 7)
            return Result.Failure<ChartOfAccount>(Error.Validation("AccountClass", "La classe doit être entre 1 et 7"));

        label = label?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(label))
            return Result.Failure<ChartOfAccount>(Error.Validation("Label", "Le libellé est obligatoire"));

        parentAccountNumber = string.IsNullOrWhiteSpace(parentAccountNumber) ? null : parentAccountNumber.Trim();
        if (parentAccountNumber is not null && !accountNumber.StartsWith(parentAccountNumber, StringComparison.Ordinal))
            return Result.Failure<ChartOfAccount>(Error.Validation("ParentAccountNumber", "Le numéro doit commencer par le compte parent"));

        var level = accountNumber.Length;
        affectationAccountNumber = string.IsNullOrWhiteSpace(affectationAccountNumber) ? null : affectationAccountNumber.Trim();

        return Result.Success(new ChartOfAccount
        {
            AccountNumber = accountNumber,
            Label = label,
            AccountClass = accountClass,
            ParentAccountNumber = parentAccountNumber,
            NatureType = natureType,
            IsSystem = isSystem,
            IsActive = true,
            Level = level,
            AccountType = accountType,
            IsAuxiliary = isAuxiliary,
            AffectationAccountNumber = affectationAccountNumber
        });
    }

    public Result UpdateLabel(string label)
    {
        if (IsSystem)
            return Result.Failure(Error.Validation("IsSystem", "Un compte système ne peut pas être renommé"));

        label = label?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(label))
            return Result.Failure(Error.Validation("Label", "Le libellé est obligatoire"));

        Label = label;
        return Result.Success();
    }

    /// <summary>Bascule l'état actif/inactif. Sans effet sur un compte système.</summary>
    public void ToggleActive()
    {
        if (!IsSystem)
            IsActive = !IsActive;
    }
}
