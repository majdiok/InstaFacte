using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Montant budgété d'un poste pour un mois d'un exercice, dans une version (Initial ou Révisé).
/// Les montants nuls ne sont pas stockés (absence de ligne = 0).
/// </summary>
public sealed class BudgetLine : Entity
{
    public Guid BudgetPostId { get; private set; }
    public BudgetPost BudgetPost { get; private set; } = null!;
    public int FiscalYear { get; private set; }
    public BudgetVersion Version { get; private set; }
    public int Month { get; private set; }
    public decimal Amount { get; private set; }

    private BudgetLine() { }

    public static Result<BudgetLine> Create(
        Guid budgetPostId, int fiscalYear, BudgetVersion version, int month, decimal amount)
    {
        if (budgetPostId == Guid.Empty)
            return Result.Failure<BudgetLine>(Error.Validation("BudgetPostId", "Poste budgétaire obligatoire."));
        if (fiscalYear is < 2000 or > 2100)
            return Result.Failure<BudgetLine>(Error.Validation("FiscalYear", "Exercice invalide."));
        if (month is < 1 or > 12)
            return Result.Failure<BudgetLine>(Error.Validation("Month", "Le mois doit être compris entre 1 et 12."));
        if (amount < 0)
            return Result.Failure<BudgetLine>(Error.Validation("Amount", "Le montant budgété ne peut pas être négatif."));

        return Result.Success(new BudgetLine
        {
            BudgetPostId = budgetPostId,
            FiscalYear = fiscalYear,
            Version = version,
            Month = month,
            Amount = amount
        });
    }
}
