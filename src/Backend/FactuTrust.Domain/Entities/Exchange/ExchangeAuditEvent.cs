using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Exchange;

public sealed class ExchangeAuditEvent : Entity
{
    public const int ActorDisplayNameMaxLength = 200;
    public const int PayloadMaxLength = 4000;

    public Guid ThreadId { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public Guid ActorUserId { get; private set; }
    public string ActorDisplayName { get; private set; } = null!;
    public ExchangeAuditEventType EventType { get; private set; }
    public string? PayloadJson { get; private set; }

    private ExchangeAuditEvent() { }

    public static Result<ExchangeAuditEvent> Create(
        Guid threadId,
        Guid actorUserId,
        string actorDisplayName,
        ExchangeAuditEventType eventType,
        string? payloadJson = null)
    {
        if (threadId == Guid.Empty || actorUserId == Guid.Empty)
            return Result.Failure<ExchangeAuditEvent>(Error.Validation("Audit", "Identifiants invalides"));

        var name = string.IsNullOrWhiteSpace(actorDisplayName) ? "Utilisateur" : actorDisplayName.Trim();

        return Result.Success(new ExchangeAuditEvent
        {
            ThreadId = threadId,
            OccurredAt = DateTime.UtcNow,
            ActorUserId = actorUserId,
            ActorDisplayName = Truncate(name, ActorDisplayNameMaxLength)!,
            EventType = eventType,
            PayloadJson = TruncateNullable(payloadJson, PayloadMaxLength)
        });
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static string? TruncateNullable(string? value, int maxLength) =>
        value is null ? null : Truncate(value, maxLength);
}
