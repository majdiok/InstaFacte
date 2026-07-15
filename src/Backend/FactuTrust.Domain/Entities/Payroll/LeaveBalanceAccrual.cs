using FactuTrust.Domain.Common;
using FactuTrust.Domain.Services.Payroll;

namespace FactuTrust.Domain.Entities.Payroll;

/// <summary>
/// Acquisition mensuelle de congés payés (1 jour / 26 jours travaillés), créditée à la validation du cycle de paie.
/// </summary>
public sealed class LeaveBalanceAccrual : AggregateRoot
{
    public Guid EmployeeId { get; private set; }
    public int Year { get; private set; }
    public int Month { get; private set; }
    public decimal WorkedDays { get; private set; }
    public decimal AccruedDays { get; private set; }
    public Guid PayrollRunId { get; private set; }

    private LeaveBalanceAccrual() { }

    public static Result<LeaveBalanceAccrual> Create(
        Guid employeeId,
        int year,
        int month,
        decimal workedDays,
        Guid payrollRunId)
    {
        if (employeeId == Guid.Empty)
            return Result.Failure<LeaveBalanceAccrual>(Error.Validation("EmployeeId", "Le salarié est obligatoire."));
        if (year < 2000 || year > 2100)
            return Result.Failure<LeaveBalanceAccrual>(Error.Validation("Year", "Année invalide."));
        if (month is < 1 or > 12)
            return Result.Failure<LeaveBalanceAccrual>(Error.Validation("Month", "Mois invalide."));
        if (payrollRunId == Guid.Empty)
            return Result.Failure<LeaveBalanceAccrual>(Error.Validation("PayrollRunId", "Le cycle de paie est obligatoire."));
        if (workedDays < 0)
            return Result.Failure<LeaveBalanceAccrual>(Error.Validation("WorkedDays", "Les jours travaillés ne peuvent pas être négatifs."));

        var accrued = LeaveBalanceService.ComputeAccruedDays(workedDays);

        return Result.Success(new LeaveBalanceAccrual
        {
            EmployeeId = employeeId,
            Year = year,
            Month = month,
            WorkedDays = Math.Round(workedDays, 2),
            AccruedDays = accrued,
            PayrollRunId = payrollRunId
        });
    }
}
