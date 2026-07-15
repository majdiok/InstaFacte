using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.Payroll;

/// <summary>
/// Avance sur salaire consentie à un salarié, retenue ultérieurement sur un bulletin.
/// </summary>
public sealed class EmployeeAdvance : AggregateRoot
{
    public Guid EmployeeId { get; private set; }
    public DateTime Date { get; private set; }
    public decimal Amount { get; private set; }
    public string? Reason { get; private set; }
    /// <summary>Vrai si l'avance a été retenue (soldée) sur un bulletin.</summary>
    public bool IsSettled { get; private set; }
    /// <summary>Cycle de paie sur lequel l'avance a été retenue.</summary>
    public Guid? SettledInPayrollRunId { get; private set; }

    private EmployeeAdvance() { }

    public static Result<EmployeeAdvance> Create(Guid employeeId, DateTime date, decimal amount, string? reason = null)
    {
        if (employeeId == Guid.Empty)
            return Result.Failure<EmployeeAdvance>(Error.Validation("EmployeeId", "Le salarié est obligatoire."));
        if (amount <= 0)
            return Result.Failure<EmployeeAdvance>(Error.Validation("Amount", "Le montant de l'avance doit être strictement positif."));

        return Result.Success(new EmployeeAdvance
        {
            EmployeeId = employeeId,
            Date = date.Date,
            Amount = Math.Round(amount, 3),
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()
        });
    }

    public void Settle(Guid payrollRunId)
    {
        IsSettled = true;
        SettledInPayrollRunId = payrollRunId;
        IncrementVersion();
    }

    public void Unsettle()
    {
        IsSettled = false;
        SettledInPayrollRunId = null;
        IncrementVersion();
    }
}
