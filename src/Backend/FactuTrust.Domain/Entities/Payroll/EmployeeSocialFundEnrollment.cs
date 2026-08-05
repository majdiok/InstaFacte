using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.Payroll;

/// <summary>Adhésion d'un salarié à une caisse / mutuelle complémentaire.</summary>
public sealed class EmployeeSocialFundEnrollment : AggregateRoot
{
    public Guid EmployeeId { get; private set; }
    public Guid SocialFundSchemeId { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public decimal? OverrideEmployeeAmount { get; private set; }
    public decimal? OverrideEmployerAmount { get; private set; }

    private EmployeeSocialFundEnrollment() { }

    public static Result<EmployeeSocialFundEnrollment> Create(
        Guid employeeId,
        Guid socialFundSchemeId,
        DateTime startDate,
        DateTime? endDate = null,
        decimal? overrideEmployeeAmount = null,
        decimal? overrideEmployerAmount = null)
    {
        if (employeeId == Guid.Empty)
            return Result.Failure<EmployeeSocialFundEnrollment>(Error.Validation("EmployeeId", "Le salarié est obligatoire."));
        if (socialFundSchemeId == Guid.Empty)
            return Result.Failure<EmployeeSocialFundEnrollment>(Error.Validation("SocialFundSchemeId", "La caisse est obligatoire."));
        if (endDate.HasValue && endDate.Value.Date < startDate.Date)
            return Result.Failure<EmployeeSocialFundEnrollment>(Error.Validation("EndDate", "La date de fin doit être postérieure à la date de début."));

        return Result.Success(new EmployeeSocialFundEnrollment
        {
            EmployeeId = employeeId,
            SocialFundSchemeId = socialFundSchemeId,
            StartDate = startDate.Date,
            EndDate = endDate?.Date,
            OverrideEmployeeAmount = overrideEmployeeAmount.HasValue ? R(overrideEmployeeAmount.Value) : null,
            OverrideEmployerAmount = overrideEmployerAmount.HasValue ? R(overrideEmployerAmount.Value) : null
        });
    }

    public bool IsActiveOn(DateTime date)
    {
        if (date.Date < StartDate.Date) return false;
        if (EndDate.HasValue && date.Date > EndDate.Value.Date) return false;
        return true;
    }

    public Result Update(DateTime? endDate, decimal? overrideEmployeeAmount, decimal? overrideEmployerAmount)
    {
        if (endDate.HasValue && endDate.Value.Date < StartDate.Date)
            return Result.Failure(Error.Validation("EndDate", "La date de fin doit être postérieure à la date de début."));

        EndDate = endDate?.Date;
        OverrideEmployeeAmount = overrideEmployeeAmount.HasValue ? R(overrideEmployeeAmount.Value) : null;
        OverrideEmployerAmount = overrideEmployerAmount.HasValue ? R(overrideEmployerAmount.Value) : null;
        IncrementVersion();
        return Result.Success();
    }

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
