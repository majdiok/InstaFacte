using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Payroll;

/// <summary>Avantage en nature (véhicule, logement…) valorisé mensuellement.</summary>
public sealed class EmployeeInKindBenefit : AggregateRoot
{
    public Guid EmployeeId { get; private set; }
    public BenefitInKindType Type { get; private set; }
    public string Label { get; private set; } = null!;
    public decimal MonthlyValue { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public string? Description { get; private set; }

    private EmployeeInKindBenefit() { }

    public static Result<EmployeeInKindBenefit> Create(
        Guid employeeId,
        BenefitInKindType type,
        string label,
        decimal monthlyValue,
        DateTime startDate,
        DateTime? endDate = null,
        string? description = null)
    {
        if (employeeId == Guid.Empty)
            return Result.Failure<EmployeeInKindBenefit>(Error.Validation("EmployeeId", "Le salarié est obligatoire."));
        if (string.IsNullOrWhiteSpace(label))
            return Result.Failure<EmployeeInKindBenefit>(Error.Validation("Label", "Le libellé est obligatoire."));
        if (monthlyValue <= 0)
            return Result.Failure<EmployeeInKindBenefit>(Error.Validation("MonthlyValue", "La valeur mensuelle doit être positive."));
        if (endDate.HasValue && endDate.Value.Date < startDate.Date)
            return Result.Failure<EmployeeInKindBenefit>(Error.Validation("EndDate", "La date de fin doit être postérieure à la date de début."));

        return Result.Success(new EmployeeInKindBenefit
        {
            EmployeeId = employeeId,
            Type = type,
            Label = label.Trim(),
            MonthlyValue = R(monthlyValue),
            StartDate = startDate.Date,
            EndDate = endDate?.Date,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim()
        });
    }

    public bool IsActiveOn(DateTime date)
    {
        if (date.Date < StartDate.Date) return false;
        if (EndDate.HasValue && date.Date > EndDate.Value.Date) return false;
        return true;
    }

    public Result Update(BenefitInKindType type, string label, decimal monthlyValue, DateTime? endDate, string? description)
    {
        if (string.IsNullOrWhiteSpace(label))
            return Result.Failure(Error.Validation("Label", "Le libellé est obligatoire."));
        if (monthlyValue <= 0)
            return Result.Failure(Error.Validation("MonthlyValue", "La valeur mensuelle doit être positive."));
        if (endDate.HasValue && endDate.Value.Date < StartDate.Date)
            return Result.Failure(Error.Validation("EndDate", "La date de fin doit être postérieure à la date de début."));

        Type = type;
        Label = label.Trim();
        MonthlyValue = R(monthlyValue);
        EndDate = endDate?.Date;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        IncrementVersion();
        return Result.Success();
    }

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
