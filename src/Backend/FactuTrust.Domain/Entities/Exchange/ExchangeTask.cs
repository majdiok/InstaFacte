using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Exchange;

public sealed class ExchangeTask : AggregateRoot
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

    public Guid ThreadId { get; private set; }
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public DateTime? DueDate { get; private set; }
    public Guid? AssigneeUserId { get; private set; }
    public Guid? AssigneeTenantId { get; private set; }
    public ExchangeTaskStatus Status { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid CreatedByTenantId { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private ExchangeTask() { }

    public static Result<ExchangeTask> Create(
        Guid threadId,
        string title,
        string? description,
        DateTime? dueDate,
        Guid? assigneeUserId,
        Guid? assigneeTenantId,
        Guid createdByUserId,
        Guid createdByTenantId)
    {
        if (threadId == Guid.Empty)
            return Result.Failure<ExchangeTask>(Error.Validation("Thread", "Thread invalide"));
        if (createdByUserId == Guid.Empty || createdByTenantId == Guid.Empty)
            return Result.Failure<ExchangeTask>(Error.Validation("Author", "Auteur invalide"));

        var trimmedTitle = title?.Trim();
        if (string.IsNullOrEmpty(trimmedTitle))
            return Result.Failure<ExchangeTask>(Error.Validation("Title", "Le titre est obligatoire"));

        return Result.Success(new ExchangeTask
        {
            ThreadId = threadId,
            Title = Truncate(trimmedTitle, TitleMaxLength)!,
            Description = TruncateNullable(description?.Trim(), DescriptionMaxLength),
            DueDate = dueDate,
            AssigneeUserId = assigneeUserId == Guid.Empty ? null : assigneeUserId,
            AssigneeTenantId = assigneeTenantId == Guid.Empty ? null : assigneeTenantId,
            Status = ExchangeTaskStatus.Todo,
            CreatedByUserId = createdByUserId,
            CreatedByTenantId = createdByTenantId
        });
    }

    public Result ChangeStatus(ExchangeTaskStatus newStatus)
    {
        if (Status == ExchangeTaskStatus.Cancelled && newStatus != ExchangeTaskStatus.Cancelled)
            return Result.Failure(Error.Validation("Status", "Une tâche annulée ne peut pas être réactivée"));

        Status = newStatus;
        CompletedAt = newStatus == ExchangeTaskStatus.Done ? DateTime.UtcNow : null;
        IncrementVersion();
        return Result.Success();
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static string? TruncateNullable(string? value, int maxLength) =>
        value is null ? null : Truncate(value, maxLength);
}
