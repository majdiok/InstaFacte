using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.Projects;

/// <summary>Monthly billable time target per employee (Odoo Billing Time Target).</summary>
public sealed class EmployeeBillingTimeTarget : Entity
{
    public Guid UserId { get; private set; }
    public int Year { get; private set; }
    public int Month { get; private set; }
    public decimal TargetHours { get; private set; }

    private EmployeeBillingTimeTarget() { }

    public static Result<EmployeeBillingTimeTarget> Create(Guid userId, int year, int month, decimal targetHours)
    {
        if (userId == Guid.Empty)
            return Result.Failure<EmployeeBillingTimeTarget>(Error.Validation("UserId", "L'utilisateur est obligatoire"));
        if (year < 2000 || year > 2100)
            return Result.Failure<EmployeeBillingTimeTarget>(Error.Validation("Year", "Année invalide"));
        if (month is < 1 or > 12)
            return Result.Failure<EmployeeBillingTimeTarget>(Error.Validation("Month", "Mois invalide"));
        if (targetHours < 0)
            return Result.Failure<EmployeeBillingTimeTarget>(Error.Validation("TargetHours", "L'objectif ne peut pas être négatif"));

        return Result.Success(new EmployeeBillingTimeTarget
        {
            UserId = userId,
            Year = year,
            Month = month,
            TargetHours = decimal.Round(targetHours, 2)
        });
    }

    public Result Update(decimal targetHours)
    {
        if (targetHours < 0)
            return Result.Failure(Error.Validation("TargetHours", "L'objectif ne peut pas être négatif"));
        TargetHours = decimal.Round(targetHours, 2);
        return Result.Success();
    }
}
