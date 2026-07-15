using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Auth;

/// <summary>
/// Lot B4 — Trace une session d'authentification active dans la base master.
///
/// Une ligne est créée à chaque login réussi (plateforme ou tenant) ; elle est
/// invalidée par <see cref="Revoke"/> :
/// <list type="bullet">
///   <item>Volontairement par l'admin (depuis la page Sessions du backoffice).</item>
///   <item>Automatiquement quand le refresh token expire ou est rotationné.</item>
///   <item>Lors d'un changement de rôle / mot de passe / reset 2FA (force re-login).</item>
/// </list>
///
/// Usage middleware (futur Lot B5) : un endpoint d'authentification peut vérifier
/// <c>RevokedAt is null AND ExpiresAt &gt; now</c> avant d'accepter le token,
/// permettant une invalidation immédiate sans attendre l'expiration JWT.
/// </summary>
public sealed class UserSession : Entity
{
    public Guid UserId { get; private set; }
    /// <summary>JwtId (claim <c>jti</c>) — UNIQUE, lien direct avec le token émis.</summary>
    public Guid JwtId { get; private set; }
    /// <summary>SHA-256 du refresh token (jamais stocké en clair).</summary>
    public string? RefreshTokenHash { get; private set; }

    public string IpAddress { get; private set; } = null!;
    public string? UserAgent { get; private set; }

    public DateTime IssuedAt { get; private set; }
    public DateTime LastUsedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    public DateTime? RevokedAt { get; private set; }
    public Guid? RevokedByUserId { get; private set; }
    public string? RevocationReason { get; private set; }

    /// <summary>Tenant ID (si la session vient d'un user tenant), <c>null</c> pour platform admins.</summary>
    public Guid? TenantId { get; private set; }

    private UserSession() { }

    public static UserSession Create(
        Guid userId,
        Guid jwtId,
        string? refreshTokenHash,
        string ipAddress,
        string? userAgent,
        DateTime expiresAt,
        Guid? tenantId = null)
    {
        var nowUtc = DateTime.UtcNow;
        return new UserSession
        {
            UserId = userId,
            JwtId = jwtId,
            RefreshTokenHash = refreshTokenHash,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            IssuedAt = nowUtc,
            LastUsedAt = nowUtc,
            ExpiresAt = expiresAt,
            TenantId = tenantId
        };
    }

    public void TouchUsage() => LastUsedAt = DateTime.UtcNow;

    public void Revoke(Guid? actorUserId, string reason)
    {
        if (RevokedAt is not null) return;
        RevokedAt = DateTime.UtcNow;
        RevokedByUserId = actorUserId;
        RevocationReason = reason;
    }

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;
}
