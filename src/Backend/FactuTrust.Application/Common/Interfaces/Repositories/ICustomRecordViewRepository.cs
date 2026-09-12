using FactuTrust.Domain.Entities.Studio;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>
/// Accès aux vues enregistrées (<see cref="CustomRecordViewDefinition"/>) d'une table Studio
/// (PR 2.3). Le filtre global <c>IsDeleted</c> du contexte s'applique à toutes les lectures.
/// </summary>
public interface ICustomRecordViewRepository
{
    /// <summary>Vues actives (et inactives si <paramref name="includeInactive"/>) d'une table, non supprimées.</summary>
    Task<IReadOnlyList<CustomRecordViewDefinition>> ListByEntityAsync(
        Guid tenantId, Guid entityDefinitionId, bool includeInactive = false, CancellationToken cancellationToken = default);

    Task<CustomRecordViewDefinition?> GetByIdAsync(Guid tenantId, Guid entityDefinitionId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>True si une vue non supprimée porte déjà cette clé pour la table (unicité (tenant, table, clé)).</summary>
    Task<bool> KeyExistsAsync(Guid tenantId, Guid entityDefinitionId, string key, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>Nombre de vues non supprimées de la table (quota plan).</summary>
    Task<int> CountByEntityAsync(Guid tenantId, Guid entityDefinitionId, CancellationToken cancellationToken = default);

    /// <summary>Défaut exclusif : repasse <c>IsDefault = false</c> à toutes les vues de la table.</summary>
    Task ClearDefaultAsync(Guid tenantId, Guid entityDefinitionId, CancellationToken cancellationToken = default);

    Task AddAsync(CustomRecordViewDefinition view, CancellationToken cancellationToken = default);

    Task UpdateAsync(CustomRecordViewDefinition view, CancellationToken cancellationToken = default);
}
