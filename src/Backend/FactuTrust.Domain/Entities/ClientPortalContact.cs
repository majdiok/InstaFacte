using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Tenant-side directory of portal contacts bound to a CRM <see cref="Client"/>.
/// The Identity user lives in the master database; <see cref="UserId"/> is a logical FK.
/// </summary>
public sealed class ClientPortalContact : Entity
{
    public Guid ClientId { get; private set; }
    public Guid UserId { get; private set; }
    public string Email { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public ClientPortalContactStatus Status { get; private set; }
    public DateTime InvitedAt { get; private set; }
    public DateTime? AcceptedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public DateTime? LastAccessAt { get; private set; }

    private ClientPortalContact() { }

    public static ClientPortalContact Invite(Guid clientId, Guid userId, string email, string displayName)
    {
        return new ClientPortalContact
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            UserId = userId,
            Email = email.Trim(),
            DisplayName = displayName.Trim(),
            Status = ClientPortalContactStatus.Invited,
            InvitedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkInvitedAgain()
    {
        Status = ClientPortalContactStatus.Invited;
        InvitedAt = DateTime.UtcNow;
        AcceptedAt = null;
        RevokedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAccepted()
    {
        Status = ClientPortalContactStatus.Active;
        AcceptedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Revoke()
    {
        Status = ClientPortalContactStatus.Revoked;
        RevokedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void TouchLastAccess()
    {
        LastAccessAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
