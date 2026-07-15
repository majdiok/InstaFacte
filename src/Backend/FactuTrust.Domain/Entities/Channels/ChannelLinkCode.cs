using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Channels;

/// <summary>
/// Hashed one-time or short-lived link code for binding an external channel identity to a user.
/// </summary>
public sealed class ChannelLinkCode : Entity
{
    public Guid UserId { get; private set; }
    public ChannelType ChannelType { get; private set; }
    public string CodeHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? ConsumedAt { get; private set; }
    public int AttemptCount { get; private set; }

    private ChannelLinkCode()
    {
    }

    public static ChannelLinkCode Create(
        Guid userId,
        ChannelType channelType,
        string codeHash,
        DateTime expiresAtUtc)
    {
        return new ChannelLinkCode
        {
            UserId = userId,
            ChannelType = channelType,
            CodeHash = codeHash.Trim(),
            ExpiresAt = expiresAtUtc,
            AttemptCount = 0
        };
    }

    public void RegisterAttempt()
    {
        AttemptCount++;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkConsumed(DateTime consumedAtUtc)
    {
        ConsumedAt = consumedAtUtc;
        UpdatedAt = consumedAtUtc;
    }
}
