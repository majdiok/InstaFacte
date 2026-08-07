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

    // ── Chantier 4/5 : congés statutaires ──
    public string? MedicalCertificateNumber { get; private set; }
    public DateTime? MedicalCertificateDate { get; private set; }
    public bool SubrogationEnabled { get; private set; }
    public decimal? EmployerTopUpPercent { get; private set; }
    public int? EmployerTopUpDays { get; private set; }
    public DateTime? ExpectedBirthDate { get; private set; }
    public DateTime? ActualBirthDate { get; private set; }
    public string? ChildBirthCertificateNumber { get; private set; }

    private LeaveRequest() { }

    public static Result<LeaveRequest> Create(
        Guid employeeId,
        LeaveType type,
        DateTime startDate,
        DateTime endDate,
        decimal days,
        string? reason = null,
        string? medicalCertificateNumber = null,
        DateTime? medicalCertificateDate = null,
        bool subrogationEnabled = false,
        decimal? employerTopUpPercent = null,
        int? employerTopUpDays = null,
        DateTime? expectedBirthDate = null,
        DateTime? actualBirthDate = null,
        string? childBirthCertificateNumber = null)
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
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            MedicalCertificateNumber = NormalizeOptional(medicalCertificateNumber),
            MedicalCertificateDate = medicalCertificateDate?.Date,
            SubrogationEnabled = subrogationEnabled,
            EmployerTopUpPercent = employerTopUpPercent.HasValue ? Math.Round(employerTopUpPercent.Value, 3) : null,
            EmployerTopUpDays = employerTopUpDays,
            ExpectedBirthDate = expectedBirthDate?.Date,
            ActualBirthDate = actualBirthDate?.Date,
            ChildBirthCertificateNumber = NormalizeOptional(childBirthCertificateNumber)
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

    public Result UpdateMedicalCertificate(string? number, DateTime? date)
    {
        MedicalCertificateNumber = NormalizeOptional(number);
        MedicalCertificateDate = date?.Date;
        IncrementVersion();
        return Result.Success();
    }

    public Result UpdateSubrogation(bool enabled)
    {
        SubrogationEnabled = enabled;
        IncrementVersion();
        return Result.Success();
    }

    public Result UpdateEmployerTopUp(decimal? percent, int? days)
    {
        if (percent.HasValue && (percent.Value < 0 || percent.Value > 100))
        {
            return Result.Failure(Error.Validation(
                "EmployerTopUpPercent",
                "Le taux de maintien doit être compris entre 0 et 100 %."));
        }

        if (days.HasValue && days.Value < 0)
        {
            return Result.Failure(Error.Validation(
                "EmployerTopUpDays",
                "Le nombre de jours de maintien ne peut pas être négatif."));
        }

        EmployerTopUpPercent = percent.HasValue ? Math.Round(percent.Value, 3) : null;
        EmployerTopUpDays = days;
        IncrementVersion();
        return Result.Success();
    }

    public Result UpdateBirthDates(DateTime? expectedBirthDate, DateTime? actualBirthDate)
    {
        ExpectedBirthDate = expectedBirthDate?.Date;
        ActualBirthDate = actualBirthDate?.Date;
        IncrementVersion();
        return Result.Success();
    }

    public Result UpdateChildBirthCertificate(string? certificateNumber)
    {
        ChildBirthCertificateNumber = NormalizeOptional(certificateNumber);
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

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
