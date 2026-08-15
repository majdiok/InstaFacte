using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces.Services;

public interface IAccountingAuditEngine
{
    /// <summary>Exécute le contrôle sur le tenant de la requête courante.</summary>
    Task<Result<AccountingAuditRunResultDto>> RunAsync(
        AccountingAuditRunRequestDto request,
        Guid? userId,
        string? userName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exécute le contrôle sur une base dossier désignée par sa chaîne de connexion, hors tenant
    /// ambiant : job de contrôle planifié et balayage de portefeuille cabinet, qui s'exécutent sans
    /// contexte de requête HTTP.
    /// </summary>
    Task<Result<AccountingAuditRunResultDto>> RunForConnectionAsync(
        string connectionString,
        AccountingAuditRunRequestDto request,
        Guid? userId,
        string? userName,
        CancellationToken cancellationToken = default);

    Task<Result<AccountingAuditRunStatusDto>> GetRunStatusAsync(
        Guid runId,
        CancellationToken cancellationToken = default);
}
