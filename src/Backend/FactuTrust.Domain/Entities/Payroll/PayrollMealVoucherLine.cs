using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.Payroll;

/// <summary>Tickets restaurant mensuels pour un salarié.</summary>
public sealed class PayrollMealVoucherLine : AggregateRoot
{
    public Guid EmployeeId { get; private set; }
    public int Year { get; private set; }
    public int Month { get; private set; }
    public int Days { get; private set; }
    public decimal FaceValue { get; private set; }
    public decimal EmployerContributionRate { get; private set; }

    private PayrollMealVoucherLine() { }

    public decimal TotalValue => R(Days * FaceValue);
    public decimal EmployerContribution => R(TotalValue * EmployerContributionRate / 100m);
    public decimal EmployeeContribution => R(TotalValue - EmployerContribution);

    public static Result<PayrollMealVoucherLine> Create(
        Guid employeeId,
        int year,
        int month,
        int days,
        decimal faceValue,
        decimal employerContributionRate)
    {
        if (employeeId == Guid.Empty)
            return Result.Failure<PayrollMealVoucherLine>(Error.Validation("EmployeeId", "Le salarié est obligatoire."));
        if (year < 2000 || year > 2100)
            return Result.Failure<PayrollMealVoucherLine>(Error.Validation("Year", "Année invalide."));
        if (month is < 1 or > 12)
            return Result.Failure<PayrollMealVoucherLine>(Error.Validation("Month", "Mois invalide."));
        if (days <= 0)
            return Result.Failure<PayrollMealVoucherLine>(Error.Validation("Days", "Le nombre de jours doit être positif."));
        if (faceValue <= 0)
            return Result.Failure<PayrollMealVoucherLine>(Error.Validation("FaceValue", "La valeur faciale doit être positive."));
        if (employerContributionRate is < 0 or > 100)
            return Result.Failure<PayrollMealVoucherLine>(Error.Validation("EmployerContributionRate", "Le taux employeur doit être entre 0 et 100 %."));

        return Result.Success(new PayrollMealVoucherLine
        {
            EmployeeId = employeeId,
            Year = year,
            Month = month,
            Days = days,
            FaceValue = R(faceValue),
            EmployerContributionRate = employerContributionRate
        });
    }

    public Result Update(int days, decimal faceValue, decimal employerContributionRate)
    {
        if (days <= 0)
            return Result.Failure(Error.Validation("Days", "Le nombre de jours doit être positif."));
        if (faceValue <= 0)
            return Result.Failure(Error.Validation("FaceValue", "La valeur faciale doit être positive."));
        if (employerContributionRate is < 0 or > 100)
            return Result.Failure(Error.Validation("EmployerContributionRate", "Le taux employeur doit être entre 0 et 100 %."));

        Days = days;
        FaceValue = R(faceValue);
        EmployerContributionRate = employerContributionRate;
        IncrementVersion();
        return Result.Success();
    }

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
