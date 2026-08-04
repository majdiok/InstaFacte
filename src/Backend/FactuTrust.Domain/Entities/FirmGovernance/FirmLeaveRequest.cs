using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.FirmGovernance;

/// <summary>Demande de congé / absence d'un collaborateur cabinet.</summary>
public sealed class FirmLeaveRequest : AggregateRoot
{
    public Guid FirmTenantId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid LeaveTypeId { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public FirmLeaveDayUnit StartUnit { get; private set; }
    public FirmLeaveDayUnit EndUnit { get; private set; }
    public decimal Days { get; private set; }
    public string? Reason { get; private set; }
    public FirmLeaveRequestStatus Status { get; private set; }
    public DateTime? SubmittedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public Guid? ProcessedByUserId { get; private set; }
    public string? ProcessedByName { get; private set; }
    public string? RejectionReason { get; private set; }

    private FirmLeaveRequest() { }

    public static Result<FirmLeaveRequest> Create(
        Guid firmTenantId,
        Guid userId,
        Guid leaveTypeId,
        DateTime startDate,
        DateTime endDate,
        FirmLeaveDayUnit startUnit,
        FirmLeaveDayUnit endUnit,
        decimal days,
        string? reason = null)
    {
        if (firmTenantId == Guid.Empty)
            return Result.Failure<FirmLeaveRequest>(Error.Validation("Tenant", "Cabinet requis."));
        if (userId == Guid.Empty)
            return Result.Failure<FirmLeaveRequest>(Error.Validation("UserId", "Collaborateur requis."));
        if (leaveTypeId == Guid.Empty)
            return Result.Failure<FirmLeaveRequest>(Error.Validation("LeaveTypeId", "Type d'absence requis."));
        if (endDate.Date < startDate.Date)
            return Result.Failure<FirmLeaveRequest>(Error.Validation("EndDate", "La date de fin ne peut pas être antérieure à la date de début."));
        if (days <= 0)
            return Result.Failure<FirmLeaveRequest>(Error.Validation("Days", "Le nombre de jours doit être strictement positif."));

        return Result.Success(new FirmLeaveRequest
        {
            FirmTenantId = firmTenantId,
            UserId = userId,
            LeaveTypeId = leaveTypeId,
            StartDate = startDate.Date,
            EndDate = endDate.Date,
            StartUnit = startUnit,
            EndUnit = endUnit,
            Days = Math.Round(days, 2),
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            Status = FirmLeaveRequestStatus.Draft
        });
    }

    public Result Update(
        Guid leaveTypeId,
        DateTime startDate,
        DateTime endDate,
        FirmLeaveDayUnit startUnit,
        FirmLeaveDayUnit endUnit,
        decimal days,
        string? reason)
    {
        if (Status is not (FirmLeaveRequestStatus.Draft or FirmLeaveRequestStatus.Rejected))
            return Result.Failure(Error.Validation("Status", "Seules les demandes brouillon ou refusées peuvent être modifiées."));
        if (leaveTypeId == Guid.Empty)
            return Result.Failure(Error.Validation("LeaveTypeId", "Type d'absence requis."));
        if (endDate.Date < startDate.Date)
            return Result.Failure(Error.Validation("EndDate", "La date de fin ne peut pas être antérieure à la date de début."));
        if (days <= 0)
            return Result.Failure(Error.Validation("Days", "Le nombre de jours doit être strictement positif."));

        LeaveTypeId = leaveTypeId;
        StartDate = startDate.Date;
        EndDate = endDate.Date;
        StartUnit = startUnit;
        EndUnit = endUnit;
        Days = Math.Round(days, 2);
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        RejectionReason = null;
        IncrementVersion();
        return Result.Success();
    }

    public Result Submit()
    {
        if (Status is not (FirmLeaveRequestStatus.Draft or FirmLeaveRequestStatus.Rejected))
            return Result.Failure(Error.Validation("Status", "Seules les demandes brouillon ou refusées peuvent être soumises."));

        Status = FirmLeaveRequestStatus.Submitted;
        SubmittedAt = DateTime.UtcNow;
        ProcessedAt = null;
        ProcessedByUserId = null;
        ProcessedByName = null;
        RejectionReason = null;
        IncrementVersion();
        return Result.Success();
    }

    public Result Approve(Guid processorUserId, string? processorName)
    {
        if (Status != FirmLeaveRequestStatus.Submitted)
            return Result.Failure(Error.Validation("Status", "Seules les demandes soumises peuvent être acceptées."));

        Status = FirmLeaveRequestStatus.Approved;
        ProcessedAt = DateTime.UtcNow;
        ProcessedByUserId = processorUserId == Guid.Empty ? null : processorUserId;
        ProcessedByName = string.IsNullOrWhiteSpace(processorName) ? null : processorName.Trim();
        RejectionReason = null;
        IncrementVersion();
        return Result.Success();
    }

    public Result Reject(Guid processorUserId, string? processorName, string? rejectionReason)
    {
        if (Status != FirmLeaveRequestStatus.Submitted)
            return Result.Failure(Error.Validation("Status", "Seules les demandes soumises peuvent être refusées."));

        Status = FirmLeaveRequestStatus.Rejected;
        ProcessedAt = DateTime.UtcNow;
        ProcessedByUserId = processorUserId == Guid.Empty ? null : processorUserId;
        ProcessedByName = string.IsNullOrWhiteSpace(processorName) ? null : processorName.Trim();
        RejectionReason = string.IsNullOrWhiteSpace(rejectionReason) ? null : rejectionReason.Trim();
        IncrementVersion();
        return Result.Success();
    }

    public Result Cancel()
    {
        if (Status is not (FirmLeaveRequestStatus.Draft or FirmLeaveRequestStatus.Submitted))
            return Result.Failure(Error.Validation("Status", "Seules les demandes brouillon ou soumises peuvent être annulées."));

        Status = FirmLeaveRequestStatus.Cancelled;
        IncrementVersion();
        return Result.Success();
    }

    /// <summary>Chevauchement de plages de dates (jours calendaires).</summary>
    public bool Overlaps(DateTime otherStart, DateTime otherEnd) =>
        StartDate.Date <= otherEnd.Date && otherStart.Date <= EndDate.Date;
}
