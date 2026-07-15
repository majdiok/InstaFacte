using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Auth;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Lot B4 — Service de tracking des tentatives de login échouées.
/// Toutes les méthodes touchent la base master (table <c>FailedLoginAttempts</c>).
/// </summary>
public interface IFailedLoginAttemptService
{
    /// <summary>Enregistre une tentative échouée (appelé par les controllers d'auth).</summary>
    Task RecordAsync(
        string email,
        Guid? userId,
        string ipAddress,
        string? userAgent,
        FailedLoginReason reason,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Liste paginée des tentatives échouées avec filtres optionnels.</summary>
    Task<FailedLoginAttemptsPageDto> ListAsync(
        string? email,
        string? ipAddress,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Compte les tentatives échouées d'une IP sur la dernière heure (utile pour brute-force detection).</summary>
    Task<int> CountByIpInLastHourAsync(string ipAddress, CancellationToken cancellationToken = default);
}
