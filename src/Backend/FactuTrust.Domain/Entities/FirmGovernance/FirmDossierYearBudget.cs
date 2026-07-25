using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.FirmGovernance;

/// <summary>
/// Budget annuel par dossier et année (équivalent Décisiel Budget.BudgetAnnuel).
/// Fallback lecture : PermanentFile.AnnualFeeAmount si aucune ligne.
/// </summary>
public sealed class FirmDossierYearBudget : Entity
{
    public Guid FirmTenantId { get; private set; }
    public Guid FirmClientAssignmentId { get; private set; }
    public int Year { get; private set; }
    public decimal BudgetAnnuel { get; private set; }

    private FirmDossierYearBudget() { }

    public static Result<FirmDossierYearBudget> Create(
        Guid firmTenantId,
        Guid assignmentId,
        int year,
        decimal budgetAnnuel)
    {
        if (firmTenantId == Guid.Empty || assignmentId == Guid.Empty)
            return Result.Failure<FirmDossierYearBudget>(Error.Validation("Tenant", "Cabinet et dossier requis"));
        if (year < 2000 || year > 2100)
            return Result.Failure<FirmDossierYearBudget>(Error.Validation("Year", "Année invalide"));
        if (budgetAnnuel < 0)
            return Result.Failure<FirmDossierYearBudget>(Error.Validation("Budget", "Budget négatif interdit"));

        return Result.Success(new FirmDossierYearBudget
        {
            FirmTenantId = firmTenantId,
            FirmClientAssignmentId = assignmentId,
            Year = year,
            BudgetAnnuel = MillimeRounding.Round(budgetAnnuel)
        });
    }

    public Result SetBudget(decimal budgetAnnuel)
    {
        if (budgetAnnuel < 0)
            return Result.Failure(Error.Validation("Budget", "Budget négatif interdit"));
        BudgetAnnuel = MillimeRounding.Round(budgetAnnuel);
        return Result.Success();
    }
}
