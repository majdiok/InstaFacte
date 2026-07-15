using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Payroll;

/// <summary>
/// Congé ou absence d'un salarié, avec impact éventuel sur le brut du mois.
/// </summary>
public sealed class LeaveRequest : AggregateRoot
{
    public Guid EmployeeId { get; private set; }
    public LeaveType Type { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    /// <summary>Nombre de jours (ouvrables) concernés.</summary>
    public decimal Days { get; private set; }
    public string? Reason { get; private set; }
    public bool IsApproved { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public string? ApprovedBy { get; private set; }

    private LeaveRequest() { }

    public static Result<LeaveRequest> Create(
        Guid employeeId,
        LeaveType type,
        DateTime startDate,
        DateTime endDate,
        decimal days,
        string? reason = null)
    {
        if (employeeId == Guid.Empty)
            return Result.Failure<LeaveRequest>(Error.Validation("EmployeeId", "Le salarié est obligatoire."));
        if (endDate.Date < startDate.Date)
            return Result.Failure<LeaveRequest>(Error.Validation("EndDate", "La date de fin ne peut pas être antérieure à la date de début."));
        if (days <= 0)
            return Result.Failure<LeaveRequest>(Error.Validation("Days", "Le nombre de jours doit être strictement positif."));

        return Result.Success(new LeaveRequest
        {
            EmployeeId = employeeId,
            Type = type,
            StartDate = startDate.Date,
            EndDate = endDate.Date,
            Days = Math.Round(days, 2),
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()
        });
    }

    public Result Update(LeaveType type, DateTime startDate, DateTime endDate, decimal days, string? reason)
    {
        if (endDate.Date < startDate.Date)
            return Result.Failure(Error.Validation("EndDate", "La date de fin ne peut pas être antérieure à la date de début."));
        if (days <= 0)
            return Result.Failure(Error.Validation("Days", "Le nombre de jours doit être strictement positif."));

        Type = type;
        StartDate = startDate.Date;
        EndDate = endDate.Date;
        Days = Math.Round(days, 2);
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
}
