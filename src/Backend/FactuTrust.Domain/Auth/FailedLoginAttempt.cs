using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Auth;

/// <summary>Raison d'un échec de login — utile pour distinguer brute-force vs mauvais mot de passe.</summary>
public enum FailedLoginReason
{
    UserNotFound = 0,
    WrongPassword = 1,
    AccountLocked = 2,
    AccountInactive = 3,
    NotAPlatformAdmin = 4,
    BoundToTenant = 5,
    TwoFactorInvalid = 6
}

/// <summary>
/// Lot B4 — Tentative de login échouée (master DB).
///
/// Tracée à chaque échec dans <c>PlatformAuthController.Login</c> ou son équivalent tenant.
/// Permet :
/// <list type="bullet">
///   <item>Détection brute-force (ex: &gt; 20 attempts / IP / heure → notification).</item>
///   <item>Audit / forensics ("qui a tenté de pénétrer ce compte le 12 mars ?").</item>
///   <item>Statistiques de sécurité (total/24h, top IPs suspectes…).</item>
/// </list>
/// </summary>
public sealed class FailedLoginAttempt : Entity
{
    public string Email { get; private set; } = null!;
    public Guid? UserId { get; private set; }
    public string IpAddress { get; private set; } = null!;
    public string? UserAgent { get; private set; }
    public DateTime AttemptAt { get; private set; }
    public FailedLoginReason Reason { get; private set; }
    public Guid? TenantId { get; private set; }

    private FailedLoginAttempt() { }

    public static FailedLoginAttempt Record(
        string email,
        Guid? userId,
        string ipAddress,
        string? userAgent,
        FailedLoginReason reason,
        Guid? tenantId = null)
    {
        return new FailedLoginAttempt
        {
            Email = (email ?? string.Empty).Trim().ToLowerInvariant(),
            UserId = userId,
            IpAddress = string.IsNullOrEmpty(ipAddress) ? "unknown" : ipAddress,
            UserAgent = userAgent,
            AttemptAt = DateTime.UtcNow,
            Reason = reason,
            TenantId = tenantId
        };
    }
}
