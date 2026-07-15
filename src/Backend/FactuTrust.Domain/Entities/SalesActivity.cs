using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities;

public sealed class SalesActivity : Entity
{
    public ActivityType Type { get; private set; }
    public string Subject { get; private set; } = null!;
    public string? Description { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid? OpportunityId { get; private set; }
    public Guid AssignedUserId { get; private set; }
    public string AssignedUserName { get; private set; } = null!;
    public DateTime? DueDate { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public bool IsCompleted => CompletedAt.HasValue;
    public ActivityPriority Priority { get; private set; }
    public DateTime? ReminderDate { get; private set; }
    public string? LinkedEntityType { get; private set; }
    public Guid? LinkedEntityId { get; private set; }

    private SalesActivity() { }

    public static Result<SalesActivity> Create(
        ActivityType type,
        string subject,
        Guid clientId,
        Guid assignedUserId,
        string assignedUserName,
        ActivityPriority priority = ActivityPriority.Medium,
        string? description = null,
        Guid? opportunityId = null,
        DateTime? dueDate = null,
        DateTime? reminderDate = null,
        string? linkedEntityType = null,
        Guid? linkedEntityId = null)
    {
        subject = subject?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(subject))
            return Result.Failure<SalesActivity>(Error.Validation("Subject", "Le sujet est obligatoire"));

        return Result.Success(new SalesActivity
        {
            Type = type,
            Subject = subject,
            Description = description?.Trim(),
            ClientId = clientId,
            OpportunityId = opportunityId,
            AssignedUserId = assignedUserId,
            AssignedUserName = assignedUserName?.Trim() ?? string.Empty,
            DueDate = dueDate?.Date,
            Priority = priority,
            ReminderDate = reminderDate,
            LinkedEntityType = linkedEntityType?.Trim(),
            LinkedEntityId = linkedEntityId
        });
    }

    public Result Update(
        ActivityType type,
        string subject,
        string? description,
        ActivityPriority priority,
        DateTime? dueDate,
        DateTime? reminderDate,
        Guid? opportunityId)
    {
        if (IsCompleted)
            return Result.Failure(Error.Validation("Completed", "Impossible de modifier une activité terminée"));

        subject = subject?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(subject))
            return Result.Failure(Error.Validation("Subject", "Le sujet est obligatoire"));

        Type = type;
        Subject = subject;
        Description = description?.Trim();
        Priority = priority;
        DueDate = dueDate?.Date;
        ReminderDate = reminderDate;
        OpportunityId = opportunityId;
        return Result.Success();
    }

    public Result UpdateAssignee(Guid assignedUserId, string assignedUserName)
    {
        if (IsCompleted)
            return Result.Failure(Error.Validation("Completed", "Impossible de réassigner une activité terminée"));

        assignedUserName = assignedUserName?.Trim() ?? string.Empty;
        if (assignedUserName.Length == 0)
            return Result.Failure(Error.Validation("AssignedUserName", "Le nom du commercial assigné est obligatoire"));

        AssignedUserId = assignedUserId;
        AssignedUserName = assignedUserName;
        return Result.Success();
    }

    public void Complete()
    {
        CompletedAt = DateTime.UtcNow;
    }
}
