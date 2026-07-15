using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Channels;

public sealed class ChannelIdentityLink : Entity
{
    public Guid UserId { get; private set; }
    public ChannelType ChannelType { get; private set; }
    public string ExternalUserId { get; private set; } = string.Empty;
    public string ExternalChatId { get; private set; } = string.Empty;
    public DateTime VerifiedAt { get; private set; }
    public DateTime? LastSeenAt { get; private set; }
    public bool IsActive { get; private set; }

    private ChannelIdentityLink()
    {
    }

    public static ChannelIdentityLink Create(
        Guid userId,
        ChannelType channelType,
        string externalUserId,
        string externalChatId,
        DateTime verifiedAtUtc)
    {
        return new ChannelIdentityLink
        {
            UserId = userId,
            ChannelType = channelType,
            ExternalUserId = externalUserId.Trim(),
            ExternalChatId = externalChatId.Trim(),
            VerifiedAt = verifiedAtUtc,
            IsActive = true
        };
    }

    /// <summary>
    /// Re-lie le même utilisateur à une (nouvelle) identité externe : réactive la ligne existante et
    /// remplace les identifiants externes. Indispensable au re-lien — l'index unique
    /// (UserId, ChannelType) interdit d'insérer une seconde ligne même désactivée.
    /// </summary>
    public void Rebind(string externalUserId, string externalChatId, DateTime verifiedAtUtc)
    {
        ExternalUserId = externalUserId.Trim();
        ExternalChatId = externalChatId.Trim();
        VerifiedAt = verifiedAtUtc;
        IsActive = true;
        UpdatedAt = verifiedAtUtc;
    }

    public void TouchLastSeen(DateTime utcNow)
    {
        LastSeenAt = utcNow;
        UpdatedAt = utcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
