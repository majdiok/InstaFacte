using FactuTrust.Application.DTOs;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Active tenant users for CRM assignment pickers (not admin-only).
/// </summary>
public interface IAssignableTenantUsersSource
{
    Task<IReadOnlyList<CrmAssignableUserDto>> ListActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Utilisateurs actifs d'un tenant explicite (ex. échéancier fiscal en mode délégué :
    /// les responsables proposés sont les collaborateurs du cabinet — home tenant — et non
    /// les utilisateurs du dossier client courant).
    /// </summary>
    Task<IReadOnlyList<CrmAssignableUserDto>> ListActiveForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
