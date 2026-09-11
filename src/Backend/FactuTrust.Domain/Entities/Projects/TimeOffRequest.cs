using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Projects;

/// <summary>Minimal time-off request that generates timesheet entries when approved (Odoo Time off entries).</summary>
public sealed class TimeOffRequest : Entity
{
    public Guid UserId { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public decimal HoursPerDay { get; private set; }
    public string TypeName { get; private set; } = null!;
    public bool RequiresApproval { get; private set; }
    public TimeOffRequestStatus Status { get; private set; }
    public Guid? TimesheetEntryId { get; private set; }

    private TimeOffRequest() { }

    public static Result<TimeOffRequest> Create(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        decimal hoursPerDay,
        string typeName,
        bool requiresApproval)
    {
        if (userId == Guid.Empty)
            return Result.Failure<TimeOffRequest>(Error.Validation("UserId", "L'utilisateur est obligatoire"));
        if (endDate.Date < startDate.Date)
            return Result.Failure<TimeOffRequest>(Error.Validation("EndDate", "La date de fin doit être postérieure au début"));
        if (hoursPerDay <= 0 || hoursPerDay > 24)
            return Result.Failure<TimeOffRequest>(Error.Validation("HoursPerDay", "Les heures par jour doivent être entre 0 et 24"));

        typeName = typeName?.Trim() ?? "Congé";
        if (typeName.Length > 100) typeName = typeName[..100];

        var status = requiresApproval ? TimeOffRequestStatus.Submitted : TimeOffRequestStatus.Approved;

        return Result.Success(new TimeOffRequest
        {
            UserId = userId,
            StartDate = startDate.Date,
            EndDate = endDate.Date,
            HoursPerDay = decimal.Round(hoursPerDay, 2),
            TypeName = typeName,
            RequiresApproval = requiresApproval,
            Status = status
        });
    }

    public Result Approve()
    {
        if (Status == TimeOffRequestStatus.Approved)
            return Result.Failure(Error.Validation("Status", "Demande déjà approuvée"));
        if (Status == TimeOffRequestStatus.Refused)
            return Result.Failure(Error.Validation("Status", "Demande refusée"));
        Status = TimeOffRequestStatus.Approved;
        return Result.Success();
    }

    public Result Refuse()
    {
        if (Status == TimeOffRequestStatus.Approved)
            return Result.Failure(Error.Validation("Status", "Demande déjà approuvée"));
        Status = TimeOffRequestStatus.Refused;
        return Result.Success();
    }

    public void LinkTimesheetEntry(Guid entryId) => TimesheetEntryId = entryId;
}
