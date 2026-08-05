using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.Payroll;

/// <summary>
/// Prime ou indemnité variable saisie pour un salarié sur un mois de paie (hors contrat récurrent).
/// </summary>
public sealed class PayrollVariableAllowanceLine : AggregateRoot
{
    public Guid EmployeeId { get; private set; }
    public int Year { get; private set; }
    public int Month { get; private set; }
    public string Label { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public bool Taxable { get; private set; }
    public bool SubjectToCnss { get; private set; }

    private PayrollVariableAllowanceLine() { }

    public static Result<PayrollVariableAllowanceLine> Create(
        Guid employeeId,
        int year,
        int month,
        string label,
        decimal amount,
        bool taxable,
        bool subjectToCnss)
    {
        if (employeeId == Guid.Empty)
            return Result.Failure<PayrollVariableAllowanceLine>(Error.Validation("EmployeeId", "Le salarié est obligatoire."));
        if (year < 2000 || year > 2100)
            return Result.Failure<PayrollVariableAllowanceLine>(Error.Validation("Year", "Année invalide."));
        if (month is < 1 or > 12)
            return Result.Failure<PayrollVariableAllowanceLine>(Error.Validation("Month", "Mois invalide."));
        if (string.IsNullOrWhiteSpace(label))
            return Result.Failure<PayrollVariableAllowanceLine>(Error.Validation("Label", "Le libellé de la prime est obligatoire."));
        if (amount <= 0)
            return Result.Failure<PayrollVariableAllowanceLine>(Error.Validation("Amount", "Le montant de la prime doit être strictement positif."));

        return Result.Success(new PayrollVariableAllowanceLine
        {
            EmployeeId = employeeId,
            Year = year,
            Month = month,
            Label = label.Trim(),
            Amount = Math.Round(amount, 3, MidpointRounding.AwayFromZero),
            Taxable = taxable,
            SubjectToCnss = subjectToCnss
        });
    }

    public Result Update(string label, decimal amount, bool taxable, bool subjectToCnss)
    {
        if (string.IsNullOrWhiteSpace(label))
            return Result.Failure(Error.Validation("Label", "Le libellé de la prime est obligatoire."));
        if (amount <= 0)
            return Result.Failure(Error.Validation("Amount", "Le montant de la prime doit être strictement positif."));

        Label = label.Trim();
        Amount = Math.Round(amount, 3, MidpointRounding.AwayFromZero);
        Taxable = taxable;
        SubjectToCnss = subjectToCnss;
        IncrementVersion();
        return Result.Success();
    }
}
