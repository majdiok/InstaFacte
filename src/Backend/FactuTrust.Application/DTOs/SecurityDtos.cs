using FactuTrust.Domain.Auth;

namespace FactuTrust.Application.DTOs;

/// <summary>Lot B4 — Session active de l'utilisateur (vue platform admin).</summary>
public sealed record UserSessionDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string UserEmail { get; init; } = null!;
    public Guid? TenantId { get; init; }
    public string? TenantName { get; init; }
    public string IpAddress { get; init; } = null!;
    public string? UserAgent { get; init; }
    public DateTime IssuedAt { get; init; }
    public DateTime LastUsedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
    public DateTime? RevokedAt { get; init; }
    public string? RevocationReason { get; init; }
    public bool IsActive { get; init; }
}

/// <summary>Lot B4 — Page paginée des sessions.</summary>
public sealed record UserSessionsPageDto
{
    public IReadOnlyList<UserSessionDto> Items { get; init; } = Array.Empty<UserSessionDto>();
    public int TotalCount { get; init; }
    public int ActiveCount { get; init; }
    public int RevokedCount { get; init; }
}

/// <summary>Lot B4 — Body POST /api/platform/sessions/{id}/revoke</summary>
public sealed record RevokeSessionRequest
{
    public string Reason { get; init; } = "ManualRevoke";
}

/// <summary>Lot B4 — Body POST /api/platform/users/{userId}/revoke-all-sessions</summary>
public sealed record RevokeAllSessionsRequest
{
    public string Reason { get; init; } = "AdminRevokeAll";
}

/// <summary>Lot B4 — Tentative de login échouée (vue admin).</summary>
public sealed record FailedLoginAttemptDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = null!;
    public Guid? UserId { get; init; }
    public string IpAddress { get; init; } = null!;
    public string? UserAgent { get; init; }
    public DateTime AttemptAt { get; init; }
    public FailedLoginReason Reason { get; init; }
    public string ReasonDisplay { get; init; } = null!;
    public Guid? TenantId { get; init; }
}

/// <summary>Lot B4 — Page paginée des tentatives échouées + KPIs.</summary>
public sealed record FailedLoginAttemptsPageDto
{
    public IReadOnlyList<FailedLoginAttemptDto> Items { get; init; } = Array.Empty<FailedLoginAttemptDto>();
    public int TotalCount { get; init; }
    /// <summary>Total sur les dernières 24 heures (sur l'ensemble des données, pas limité aux filtres).</summary>
    public int Last24h { get; init; }
    /// <summary>Total sur la dernière heure (pour détection brute-force visible).</summary>
    public int LastHour { get; init; }
    /// <summary>Top IP suspectes (count &gt; 10 sur 1h).</summary>
    public IReadOnlyList<TopIpDto> TopSuspiciousIps { get; init; } = Array.Empty<TopIpDto>();
}

public sealed record TopIpDto
{
    public string IpAddress { get; init; } = null!;
    public int Count { get; init; }
    public DateTime LastSeen { get; init; }
}

/// <summary>Lot B4 — Helper d'affichage du motif d'échec.</summary>
public static class FailedLoginReasonExtensions
{
    public static string ToDisplayString(this FailedLoginReason reason) => reason switch
    {
        FailedLoginReason.UserNotFound => "Utilisateur introuvable",
        FailedLoginReason.WrongPassword => "Mot de passe incorrect",
        FailedLoginReason.AccountLocked => "Compte verrouillé",
        FailedLoginReason.AccountInactive => "Compte désactivé",
        FailedLoginReason.NotAPlatformAdmin => "Rôle plateforme manquant",
        FailedLoginReason.BoundToTenant => "Compte lié à un tenant",
        FailedLoginReason.TwoFactorInvalid => "Code 2FA invalide",
        _ => reason.ToString()
    };
}
