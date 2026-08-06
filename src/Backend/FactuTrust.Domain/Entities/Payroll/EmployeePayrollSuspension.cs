using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Payroll;

/// <summary>
/// Période de suspension de contrat impactant éventuellement le salaire de base.
/// </summary>
public sealed class EmployeePayrollSuspension : AggregateRoot
{
    public Guid EmployeeId { get; private set; }
    public PayrollSuspensionType Type { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    /// <summary>Si vrai, le salaire est maintenu pendant la suspension.</summary>
    public bool IsPaid { get; private set; }
    public string? Reason { get; private set; }
    public bool IsApproved { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public string? ApprovedBy { get; private set; }

    private EmployeePayrollSuspension() { }

    public static Result<EmployeePayrollSuspension> Create(
        Guid employeeId,
        PayrollSuspensionType type,
        DateTime startDate,
        DateTime? endDate,
        bool isPaid,
        string? reason = null)
    {
        if (employeeId == Guid.Empty)
            return Result.Failure<EmployeePayrollSuspension>(Error.Validation("EmployeeId", "Le salarié est obligatoire."));
        if (endDate.HasValue && endDate.Value.Date < startDate.Date)
            return Result.Failure<EmployeePayrollSuspension>(Error.Validation("EndDate", "La date de fin ne peut pas être antérieure à la date de début."));

        return Result.Success(new EmployeePayrollSuspension
        {
            EmployeeId = employeeId,
            Type = type,
            StartDate = startDate.Date,
            EndDate = endDate?.Date,
            IsPaid = isPaid,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()
        });
    }

    public Result Update(
        PayrollSuspensionType type,
        DateTime startDate,
        DateTime? endDate,
        bool isPaid,
        string? reason)
    {
        if (endDate.HasValue && endDate.Value.Date < startDate.Date)
            return Result.Failure(Error.Validation("EndDate", "La date de fin ne peut pas être antérieure à la date de début."));

        Type = type;
        StartDate = startDate.Date;
        EndDate = endDate?.Date;
        IsPaid = isPaid;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        IncrementVersion();
        return Result.Success();
    }

    public void Approve(string approvedBy)
    {
        IsApproved = true;
        ApprovedAt = DateTime.UtcNow;
        ApprovedBy = string.IsNullOrWhiteSpace(approvedBy) ? null : approvedBy.Trim();
        IncrementVersion();
    }

    public Result Close(DateTime endDate)
    {
        if (endDate.Date < StartDate)
            return Result.Failure(Error.Validation("EndDate", "La date de fin ne peut pas être antérieure à la date de début."));

        EndDate = endDate.Date;
        IncrementVersion();
        return Result.Success();
    }
}
