using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Exchange;

public sealed class ExchangeRequest : AggregateRoot
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 4000;

    public Guid ThreadId { get; private set; }
    public int Number { get; private set; }
    public string Title { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public ExchangeRequestCategory Category { get; private set; }
    public ExchangeRequestPriority Priority { get; private set; }
    public ExchangeRequestStatus Status { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid CreatedByTenantId { get; private set; }
    public Guid? AssigneeUserId { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }

    private ExchangeRequest() { }

    public static Result<ExchangeRequest> Create(
        Guid threadId,
        int number,
        string title,
        string description,
        ExchangeRequestCategory category,
        ExchangeRequestPriority priority,
        Guid createdByUserId,
        Guid createdByTenantId)
    {
        if (threadId == Guid.Empty)
            return Result.Failure<ExchangeRequest>(Error.Validation("Thread", "Thread invalide"));
        if (number < 1)
            return Result.Failure<ExchangeRequest>(Error.Validation("Number", "Numéro invalide"));
        if (createdByUserId == Guid.Empty || createdByTenantId == Guid.Empty)
            return Result.Failure<ExchangeRequest>(Error.Validation("Author", "Auteur invalide"));

        var trimmedTitle = title?.Trim();
        if (string.IsNullOrEmpty(trimmedTitle))
            return Result.Failure<ExchangeRequest>(Error.Validation("Title", "Le titre est obligatoire"));

        return Result.Success(new ExchangeRequest
        {
            ThreadId = threadId,
            Number = number,
            Title = Truncate(trimmedTitle, TitleMaxLength)!,
            Description = Truncate(description?.Trim() ?? string.Empty, DescriptionMaxLength)!,
            Category = category,
            Priority = priority,
            Status = ExchangeRequestStatus.Open,
            CreatedByUserId = createdByUserId,
            CreatedByTenantId = createdByTenantId
        });
    }

    public Result Assign(Guid assigneeUserId)
    {
        if (assigneeUserId == Guid.Empty)
            return Result.Failure(Error.Validation("Assignee", "Assigné invalide"));
        if (Status is ExchangeRequestStatus.Closed or ExchangeRequestStatus.Resolved)
            return Result.Failure(Error.Validation("Status", "Impossible d'assigner une demande close ou résolue"));

        AssigneeUserId = assigneeUserId;
        if (Status == ExchangeRequestStatus.Open)
            Status = ExchangeRequestStatus.InProgress;
        IncrementVersion();
        return Result.Success();
    }

    public Result ChangeStatus(ExchangeRequestStatus newStatus)
    {
        if (Status == newStatus)
            return Result.Success();

        if (Status == ExchangeRequestStatus.Closed)
            return Result.Failure(Error.Validation("Status", "Une demande close ne peut plus changer de statut"));

        if (newStatus == ExchangeRequestStatus.Open && Status != ExchangeRequestStatus.Open)
            return Result.Failure(Error.Validation("Status", "Impossible de revenir au statut Ouvert"));

        Status = newStatus;
        if (newStatus == ExchangeRequestStatus.Resolved)
            ResolvedAt = DateTime.UtcNow;
        if (newStatus == ExchangeRequestStatus.Closed)
            ClosedAt = DateTime.UtcNow;

        IncrementVersion();
        return Result.Success();
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
