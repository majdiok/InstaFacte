using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;

namespace FactuTrust.Domain.Entities.Payroll;

/// <summary>Prêt salarié avec échéancier mensuel sans intérêt.</summary>
public sealed class EmployeeLoan : AggregateRoot
{
    public Guid EmployeeId { get; private set; }
    public string Reference { get; private set; } = null!;
    public decimal Principal { get; private set; }
    public int InstallmentCount { get; private set; }
    public decimal MonthlyInstallmentAmount { get; private set; }
    public int StartYear { get; private set; }
    public int StartMonth { get; private set; }
    public string? Notes { get; private set; }
    public EmployeeLoanStatus Status { get; private set; }

    private readonly List<EmployeeLoanInstallment> _installments = new();
    public IReadOnlyCollection<EmployeeLoanInstallment> Installments => _installments.AsReadOnly();

    public decimal RemainingBalance => R(_installments.Where(i => !i.IsSettled).Sum(i => i.Amount));

    private EmployeeLoan() { }

    public static Result<EmployeeLoan> Create(
        Guid employeeId,
        string reference,
        decimal principal,
        int installmentCount,
        int startYear,
        int startMonth,
        string? notes = null)
    {
        if (employeeId == Guid.Empty)
            return Result.Failure<EmployeeLoan>(Error.Validation("EmployeeId", "Le salarié est obligatoire."));
        if (string.IsNullOrWhiteSpace(reference))
            return Result.Failure<EmployeeLoan>(Error.Validation("Reference", "La référence est obligatoire."));
        if (principal <= 0)
            return Result.Failure<EmployeeLoan>(Error.Validation("Principal", "Le montant du prêt doit être positif."));
        if (installmentCount <= 0)
            return Result.Failure<EmployeeLoan>(Error.Validation("InstallmentCount", "Le nombre d'échéances doit être positif."));
        if (startMonth is < 1 or > 12)
            return Result.Failure<EmployeeLoan>(Error.Validation("StartMonth", "Mois invalide."));

        var schedule = LoanScheduleGenerator.Generate(principal, installmentCount, startYear, startMonth);
        var loan = new EmployeeLoan
        {
            EmployeeId = employeeId,
            Reference = reference.Trim().ToUpperInvariant(),
            Principal = R(principal),
            InstallmentCount = installmentCount,
            MonthlyInstallmentAmount = schedule.First().Amount,
            StartYear = startYear,
            StartMonth = startMonth,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            Status = EmployeeLoanStatus.Active
        };

        foreach (var item in schedule)
        {
            loan._installments.Add(EmployeeLoanInstallment.Create(
                loan.Id, item.SequenceNumber, item.Year, item.Month, item.Amount));
        }

        return Result.Success(loan);
    }

    public void MarkInstallmentSettled(Guid installmentId, Guid payrollRunId)
    {
        var installment = _installments.FirstOrDefault(i => i.Id == installmentId)
            ?? throw new InvalidOperationException("Échéance introuvable.");
        installment.Settle(payrollRunId);

        if (_installments.All(i => i.IsSettled))
            Status = EmployeeLoanStatus.FullyRepaid;

        IncrementVersion();
    }

    public void UnsettleInstallmentsForRun(Guid payrollRunId)
    {
        foreach (var installment in _installments.Where(i => i.SettledInPayrollRunId == payrollRunId))
            installment.Unsettle();

        if (Status == EmployeeLoanStatus.FullyRepaid)
            Status = EmployeeLoanStatus.Active;

        IncrementVersion();
    }

    public void Cancel()
    {
        if (_installments.Any(i => i.IsSettled))
            throw new InvalidOperationException("Impossible d'annuler un prêt avec des échéances déjà retenues.");

        Status = EmployeeLoanStatus.Cancelled;
        IncrementVersion();
    }

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}

public sealed class EmployeeLoanInstallment : Entity
{
    public Guid EmployeeLoanId { get; private set; }
    public int SequenceNumber { get; private set; }
    public int Year { get; private set; }
    public int Month { get; private set; }
    public decimal Amount { get; private set; }
    public bool IsSettled { get; private set; }
    public Guid? SettledInPayrollRunId { get; private set; }

    private EmployeeLoanInstallment() { }

    internal static EmployeeLoanInstallment Create(Guid loanId, int sequence, int year, int month, decimal amount) =>
        new()
        {
            EmployeeLoanId = loanId,
            SequenceNumber = sequence,
            Year = year,
            Month = month,
            Amount = Math.Round(amount, 3, MidpointRounding.AwayFromZero)
        };

    internal void Settle(Guid payrollRunId)
    {
        IsSettled = true;
        SettledInPayrollRunId = payrollRunId;
    }

    internal void Unsettle()
    {
        IsSettled = false;
        SettledInPayrollRunId = null;
    }
}
