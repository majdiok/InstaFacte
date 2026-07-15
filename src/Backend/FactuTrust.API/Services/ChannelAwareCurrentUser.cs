using FactuTrust.API.Services.Channels;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Enums;

namespace FactuTrust.API.Services;

/// <summary>
/// Décorateur d'<see cref="ICurrentUser"/> : sert l'instantané <see cref="ChannelUserContext"/>
/// quand un traitement de canal (WhatsApp) est en cours — le pipeline IA et ses outils lisent
/// alors l'identité impersonnée — et délègue sinon au <see cref="CurrentUser"/> HTTP historique
/// (chemin web strictement inchangé : l'AsyncLocal n'est jamais posé hors traitement de canal).
/// </summary>
public sealed class ChannelAwareCurrentUser : ICurrentUser
{
    private readonly CurrentUser _inner;

    public ChannelAwareCurrentUser(CurrentUser inner)
    {
        _inner = inner;
    }

    private static ChannelUserSnapshot? Snapshot => ChannelUserContext.Current;

    public Guid? UserId => Snapshot is { } s ? s.UserId : _inner.UserId;

    public string? Email => Snapshot is { } s ? s.Email : _inner.Email;

    public Guid? TenantId => Snapshot is { } s ? s.TenantId : _inner.TenantId;

    public UserRole? Role => Snapshot is { } s ? s.Role : _inner.Role;

    public bool IsAuthenticated => Snapshot is not null || _inner.IsAuthenticated;

    public bool HasPermission(string permission) =>
        Snapshot is { } s ? s.Permissions.Contains(permission) : _inner.HasPermission(permission);

    public string? IpAddress => Snapshot is not null ? null : _inner.IpAddress;

    public string? UserAgent => Snapshot is not null ? null : _inner.UserAgent;
}
