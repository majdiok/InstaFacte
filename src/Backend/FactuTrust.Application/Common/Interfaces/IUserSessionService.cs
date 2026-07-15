using FactuTrust.Application.DTOs;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Lot B4 — Service de gestion des sessions actives.
///
/// Toutes les méthodes touchent la base master (table <c>UserSessions</c>).
/// </summary>
public interface IUserSessionService
{
    /// <summary>
    /// Crée une session pour un utilisateur (appelé après login réussi par
    /// <c>PlatformAuthController</c>).
    /// </summary>
    Task RecordAsync(
        Guid userId,
        Guid jwtId,
        string? refreshToken,
        string ipAddress,
        string? userAgent,
        DateTime expiresAt,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Liste paginée des sessions d'un utilisateur (toutes, actives + révoquées).</summary>
    Task<UserSessionsPageDto> ListByUserAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Liste paginée de toutes les sessions plateforme (admins) — filtrable par état.</summary>
    Task<UserSessionsPageDto> ListPlatformSessionsAsync(
        bool? activeOnly,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Révoque une session précise (par son Id de ligne).</summary>
    Task<bool> RevokeAsync(
        Guid sessionId,
        Guid actorUserId,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>Révoque toutes les sessions actives d'un utilisateur (force re-login).</summary>
    Task<int> RevokeAllByUserAsync(
        Guid userId,
        Guid actorUserId,
        string reason,
        CancellationToken cancellationToken = default);
}
