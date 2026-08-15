using FactuTrust.Application.Common;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Révision assistée à l'échelle du portefeuille cabinet : lit les anomalies déjà détectées dans
/// chaque base dossier et les consolide pour le chef de mission.
///
/// <para><b>Le périmètre d'accès est toujours explicite.</b> Chaque méthode reçoit son
/// <see cref="FirmDossierAccessScope"/> plutôt que de le déduire de l'utilisateur courant : hors
/// requête HTTP — dans un job de nuit — <c>ICurrentUser</c> est vide, et l'idiome fail-closed
/// retomberait silencieusement sur « aucun filtre ». Un <c>null</c> signifie donc « portefeuille
/// complet », choisi et non subi.</para>
/// </summary>
public interface IFirmRevisionService
{
    /// <summary>Vue consolidée : compteurs par sévérité, impact chiffré, dossiers, familles.</summary>
    Task<Result<FirmRevisionOverviewDto>> GetOverviewAsync(
        Guid firmTenantId,
        FirmDossierAccessScope? scope,
        int fiscalYear,
        CancellationToken cancellationToken = default);

    /// <summary>Détail d'un dossier : anomalies ouvertes et note de révision si elle existe.</summary>
    Task<Result<FirmRevisionDossierDetailDto>> GetDossierAsync(
        Guid firmTenantId,
        FirmDossierAccessScope? scope,
        Guid companyTenantId,
        int fiscalYear,
        CancellationToken cancellationToken = default);

    /// <summary>File de travail priorisée, ventilée par collaborateur affecté.</summary>
    Task<Result<FirmRevisionWorkQueueDto>> GetWorkQueueAsync(
        Guid firmTenantId,
        FirmDossierAccessScope? scope,
        int fiscalYear,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Produit — ou régénère — le dossier de révision d'un dossier client, à partir de son dernier
    /// contrôle terminé. Une note par contrôle : régénérer remplace.
    /// </summary>
    Task<Result<FirmRevisionNoteDto>> GenerateNoteAsync(
        Guid firmTenantId,
        FirmDossierAccessScope? scope,
        Guid companyTenantId,
        int fiscalYear,
        Guid? userId,
        string? userName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rend le dossier de révision d'un dossier client au format PDF. Échoue si aucune note n'a
    /// encore été produite : le PDF met en page, il ne génère pas.
    /// </summary>
    Task<Result<byte[]>> ExportNoteAsync(
        Guid firmTenantId,
        FirmDossierAccessScope? scope,
        Guid companyTenantId,
        int fiscalYear,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lance un contrôle sur chaque dossier du périmètre. L'échec d'un dossier n'interrompt jamais
    /// le balayage : il est compté et signalé.
    /// </summary>
    Task<Result<FirmRevisionSweepResultDto>> SweepAsync(
        Guid firmTenantId,
        FirmDossierAccessScope? scope,
        int fiscalYear,
        Guid? triggeredByUserId,
        string? triggeredByUserName,
        CancellationToken cancellationToken = default);
}
