using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.Exchange;

public sealed class ExchangeRequestComment : Entity
{
    public const int BodyMaxLength = 4000;
    public const int AuthorDisplayNameMaxLength = 200;

    public Guid ThreadId { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public Guid AuthorTenantId { get; private set; }
    public string AuthorDisplayName { get; private set; } = null!;
    public string Body { get; private set; } = null!;

    private ExchangeRequestComment() { }

    public static Result<ExchangeRequestComment> Create(
        Guid threadId,
        Guid requestId,
        Guid authorUserId,
        Guid authorTenantId,
        string authorDisplayName,
        string body)
    {
        if (threadId == Guid.Empty)
            return Result.Failure<ExchangeRequestComment>(Error.Validation("Thread", "Thread invalide"));
        if (requestId == Guid.Empty)
            return Result.Failure<ExchangeRequestComment>(Error.Validation("Request", "Demande invalide"));
        if (authorUserId == Guid.Empty || authorTenantId == Guid.Empty)
            return Result.Failure<ExchangeRequestComment>(Error.Validation("Author", "Auteur invalide"));

        var trimmed = body?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return Result.Failure<ExchangeRequestComment>(Error.Validation("Body", "Le commentaire ne peut pas être vide"));

        var name = string.IsNullOrWhiteSpace(authorDisplayName) ? "Utilisateur" : authorDisplayName.Trim();

        return Result.Success(new ExchangeRequestComment
        {
            ThreadId = threadId,
            RequestId = requestId,
            AuthorUserId = authorUserId,
            AuthorTenantId = authorTenantId,
            AuthorDisplayName = Truncate(name, AuthorDisplayNameMaxLength)!,
            Body = Truncate(trimmed, BodyMaxLength)!
        });
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
