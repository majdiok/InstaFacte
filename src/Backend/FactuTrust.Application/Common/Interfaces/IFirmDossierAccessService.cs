using FactuTrust.Application.Common;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Point unique de vérité pour l'ACL dossier client (affectation gestionnaire comptable).
/// </summary>
public interface IFirmDossierAccessService
{
    /// <summary>
    /// Indique si l'utilisateur peut ouvrir / rester sur le dossier client (company tenant).
    /// FirmManager : liaison Active suffisante.
    /// FirmAccountant : liaison Active + PermanentFile.AssignedAccountantUserId == user.
    /// </summary>
    Task<bool> CanAccessClientDossierAsync(
        Guid firmTenantId,
        FirmDossierAccessScope scope,
        Guid companyTenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Indique si l'utilisateur peut accéder au dossier via son FirmClientAssignmentId.
    /// </summary>
    Task<bool> CanAccessAssignmentAsync(
        Guid firmTenantId,
        FirmDossierAccessScope scope,
        Guid firmClientAssignmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Company tenant IDs accessibles. Null = pas de filtre (FirmManager / rôle non restreint).
    /// Ensemble vide = aucun dossier accessible.
    /// </summary>
    Task<IReadOnlySet<Guid>?> GetAccessibleCompanyTenantIdsAsync(
        Guid firmTenantId,
        FirmDossierAccessScope scope,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Assignment IDs accessibles. Null = pas de filtre (FirmManager).
    /// </summary>
    Task<IReadOnlySet<Guid>?> GetAccessibleAssignmentIdsAsync(
        Guid firmTenantId,
        FirmDossierAccessScope scope,
        CancellationToken cancellationToken = default);
}
