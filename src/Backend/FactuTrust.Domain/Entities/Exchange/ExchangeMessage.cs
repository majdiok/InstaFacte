using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Exchange;

public sealed class ExchangeMessage : Entity
{
    public const int BodyMaxLength = 8000;
    public const int AuthorDisplayNameMaxLength = 200;

    public Guid ThreadId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public Guid AuthorTenantId { get; private set; }
    public string AuthorDisplayName { get; private set; } = null!;
    public ExchangeMessageVisibility Visibility { get; private set; }
    public string Body { get; private set; } = null!;
    public DateTime SentAt { get; private set; }

    private ExchangeMessage() { }

    public static Result<ExchangeMessage> Create(
        Guid threadId,
        Guid authorUserId,
        Guid authorTenantId,
        string authorDisplayName,
        string body,
        ExchangeMessageVisibility visibility)
    {
        if (threadId == Guid.Empty)
            return Result.Failure<ExchangeMessage>(Error.Validation("Thread", "Thread invalide"));
        if (authorUserId == Guid.Empty || authorTenantId == Guid.Empty)
            return Result.Failure<ExchangeMessage>(Error.Validation("Author", "Auteur invalide"));

        var trimmed = body?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return Result.Failure<ExchangeMessage>(Error.Validation("Body", "Le message ne peut pas être vide"));

        var name = string.IsNullOrWhiteSpace(authorDisplayName) ? "Utilisateur" : authorDisplayName.Trim();

        return Result.Success(new ExchangeMessage
        {
            ThreadId = threadId,
            AuthorUserId = authorUserId,
            AuthorTenantId = authorTenantId,
            AuthorDisplayName = Truncate(name, AuthorDisplayNameMaxLength)!,
            Visibility = visibility,
            Body = Truncate(trimmed, BodyMaxLength)!,
            SentAt = DateTime.UtcNow
        });
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
