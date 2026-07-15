using FactuTrust.Domain.Enums;

namespace FactuTrust.API.Services.Channels;

/// <summary>
/// Instantané d'identité utilisé pendant le traitement d'un message de canal (webhook/job, donc
/// sans HttpContext). Les permissions sont les permissions EFFECTIVES (rôle ∩ droits de modules,
/// via <c>IEffectivePermissionService</c>) — mêmes droits que le JWT web de l'utilisateur.
/// </summary>
public sealed record ChannelUserSnapshot(
    Guid UserId,
    Guid TenantId,
    string? Email,
    UserRole Role,
    IReadOnlySet<string> Permissions);

/// <summary>
/// Contexte ambiant AsyncLocal (patron <c>TenantContext</c>) portant l'identité impersonnée d'un
/// traitement de canal. Posé uniquement par <see cref="ChannelInboundOrchestrator"/> autour de
/// l'invocation du pipeline IA ; jamais posé sur le chemin HTTP web — le décorateur
/// <see cref="ChannelAwareCurrentUser"/> y reste alors transparent.
/// </summary>
public static class ChannelUserContext
{
    private static readonly AsyncLocal<ChannelUserSnapshot?> _current = new();

    public static ChannelUserSnapshot? Current => _current.Value;

    public static void Set(ChannelUserSnapshot snapshot) => _current.Value = snapshot;

    public static void Clear() => _current.Value = null;
}
