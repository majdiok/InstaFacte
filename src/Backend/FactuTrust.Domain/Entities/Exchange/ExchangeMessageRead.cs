using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.Exchange;

public sealed class ExchangeMessageRead : Entity
{
    public Guid MessageId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime ReadAt { get; private set; }

    private ExchangeMessageRead() { }

    public static Result<ExchangeMessageRead> Create(Guid messageId, Guid userId)
    {
        if (messageId == Guid.Empty || userId == Guid.Empty)
            return Result.Failure<ExchangeMessageRead>(Error.Validation("Read", "Identifiants invalides"));

        return Result.Success(new ExchangeMessageRead
        {
            MessageId = messageId,
            UserId = userId,
            ReadAt = DateTime.UtcNow
        });
    }
}
