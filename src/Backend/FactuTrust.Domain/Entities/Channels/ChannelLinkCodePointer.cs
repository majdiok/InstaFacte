using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Channels;

/// <summary>
/// Pointeur master d'un code de liaison : quand « LIER &lt;code&gt; » arrive par le webhook anonyme,
/// aucun lien d'identité n'existe encore — ce pointeur est le seul moyen de retrouver le tenant à
/// partir du hash du code. La validation finale s'effectue toujours sur le
/// <see cref="ChannelLinkCode"/> de la base tenant (source de vérité).
/// </summary>
public sealed class ChannelLinkCodePointer : Entity
{
    public ChannelType ChannelType { get; private set; }
    public string CodeHash { get; private set; } = string.Empty;
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? ConsumedAt { get; private set; }
    public int AttemptCount { get; private set; }

    private ChannelLinkCodePointer()
    {
    }

    public static ChannelLinkCodePointer Create(
        ChannelType channelType,
        string codeHash,
        Guid tenantId,
        Guid userId,
        DateTime expiresAtUtc)
    {
        return new ChannelLinkCodePointer
        {
            ChannelType = channelType,
            CodeHash = codeHash.Trim(),
            TenantId = tenantId,
            UserId = userId,
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
