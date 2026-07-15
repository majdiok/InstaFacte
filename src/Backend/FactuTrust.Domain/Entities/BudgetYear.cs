using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Statut budgétaire d'un exercice. Créé implicitement au premier enregistrement de lignes ;
/// la validation fige le budget initial (copié vers la version révisée, seule modifiable ensuite).
/// </summary>
public sealed class BudgetYear : Entity
{
    public int FiscalYear { get; private set; }
    public BudgetYearStatus Status { get; private set; }
    public DateTime? ValidatedAt { get; private set; }
    public string? ValidatedBy { get; private set; }

    private BudgetYear() { }

    public static Result<BudgetYear> Create(int fiscalYear)
    {
        if (fiscalYear is < 2000 or > 2100)
            return Result.Failure<BudgetYear>(Error.Validation("FiscalYear", "Exercice invalide."));

        return Result.Success(new BudgetYear
        {
            FiscalYear = fiscalYear,
            Status = BudgetYearStatus.Draft
        });
    }

    /// <summary>Fige le budget initial. Refuse une seconde validation.</summary>
    public Result ValidateInitial(string user)
    {
        if (Status == BudgetYearStatus.Validated)
            return Result.Failure(Error.Validation(
                "Status", $"Le budget initial de l'exercice {FiscalYear} est déjà validé."));

        Status = BudgetYearStatus.Validated;
        ValidatedAt = DateTime.UtcNow;
        ValidatedBy = string.IsNullOrWhiteSpace(user) ? "system" : user.Trim();
        return Result.Success();
    }

    /// <summary>Version actuellement modifiable : Initial en brouillon, Révisé après validation.</summary>
    public BudgetVersion EditableVersion =>
        Status == BudgetYearStatus.Validated ? BudgetVersion.Revised : BudgetVersion.Initial;
}
