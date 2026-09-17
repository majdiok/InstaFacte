using FactuTrust.Application.Common.Identity;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Auth;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services.Identity;

/// <summary>
/// Résolveur fail-closed de l'identité impersonnée (plan Studio IA 4.2a). Même séquence que
/// <c>ChannelInboundOrchestrator</c> (utilisateur maître → rôles Identity → permissions effectives)
/// mais sans aucun repli : un rôle plateforme ou l'absence de rôle applicatif reconnu ⇒ refus.
/// Chaque appel relit la base maître : une désactivation est prise en compte à la reprise suivante.
/// </summary>
internal sealed class ImpersonationSnapshotResolver : IImpersonationSnapshotResolver
{
    private readonly MasterDbContext _master;
    private readonly IEffectivePermissionService _effectivePermissions;
    private readonly ILogger<ImpersonationSnapshotResolver> _logger;

    public ImpersonationSnapshotResolver(
        MasterDbContext master,
        IEffectivePermissionService effectivePermissions,
        ILogger<ImpersonationSnapshotResolver> logger)
    {
        _master = master;
        _effectivePermissions = effectivePermissions;
        _logger = logger;
    }

    public async Task<ImpersonatedUserSnapshot?> ResolveAsync(Guid tenantId, Guid userId, string origin, CancellationToken ct)
    {
        var user = await _master.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Id, u.Email, u.TenantId, u.IsActive })
            .FirstOrDefaultAsync(ct);

        if (user is null)
            return Refuse("utilisateur introuvable", userId, origin);
        if (!user.IsActive)
            return Refuse("utilisateur inactif", userId, origin);
        if (user.TenantId != tenantId)
            return Refuse("tenant différent", userId, origin);

        var roleNames = await (
                from userRole in _master.UserRoles
                join identityRole in _master.Roles on userRole.RoleId equals identityRole.Id
                where userRole.UserId == userId
                select identityRole.Name)
            .ToListAsync(ct);

        if (roleNames.Any(r => string.Equals(r, PlatformRoles.PlatformAdmin, StringComparison.Ordinal)))
            return Refuse("rôle plateforme", userId, origin);

        UserRole? role = null;
        foreach (var name in roleNames)
        {
            if (Enum.TryParse<UserRole>(name, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed))
            {
                role = parsed;
                break;
            }
        }

        if (role is null)
            return Refuse("aucun rôle applicatif reconnu", userId, origin);

        var access = await _effectivePermissions.GetUserAccessSnapshotAsync(userId, ct);
        return new ImpersonatedUserSnapshot(user.Id, user.TenantId, user.Email, role.Value, access.EffectivePermissions, origin);
    }

    private ImpersonatedUserSnapshot? Refuse(string reason, Guid userId, string origin)
    {
        // Identifiants seulement : jamais d'e-mail ni de nom dans les journaux.
        _logger.LogWarning("Impersonation refusée ({Reason}) pour l'utilisateur {UserId}, origine {Origin}", reason, userId, origin);
        return null;
    }
}
