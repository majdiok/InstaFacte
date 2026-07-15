using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Channels;

/// <summary>
/// Index de routage master (dénormalisé) : associe une identité externe de canal (ex. numéro
/// WhatsApp) au tenant et à l'utilisateur internes. Permet au webhook anonyme de résoudre la base
/// tenant à interroger ; la source de vérité du lien reste <see cref="ChannelIdentityLink"/>
/// (base tenant), revalidée à chaque traitement.
/// </summary>
public sealed class ChannelExternalRoute : Entity
{
    public ChannelType ChannelType { get; private set; }
    public string ExternalUserId { get; private set; } = string.Empty;
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public bool IsActive { get; private set; }

    private ChannelExternalRoute()
    {
    }

    public static ChannelExternalRoute Create(
        ChannelType channelType,
        string externalUserId,
        Guid tenantId,
        Guid userId)
    {
        return new ChannelExternalRoute
        {
            ChannelType = channelType,
            ExternalUserId = externalUserId.Trim(),
            TenantId = tenantId,
            UserId = userId,
            IsActive = true
        };
    }

    /// <summary>Réassigne la route à un autre couple tenant/utilisateur (re-lien du même numéro).</summary>
    public void Rebind(Guid tenantId, Guid userId, DateTime utcNow)
    {
        TenantId = tenantId;
        UserId = userId;
        IsActive = true;
        UpdatedAt = utcNow;
    }

    public void Deactivate(DateTime utcNow)
    {
        IsActive = false;
        UpdatedAt = utcNow;
    }
}
