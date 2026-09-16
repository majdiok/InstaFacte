using FactuTrust.Domain.Entities.Studio.Workflows;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>
/// Accès aux définitions, instances, exécutions d'étapes et approbations des workflows Studio
/// (PR 4.1). Toutes les méthodes filtrent sur <c>TenantId</c> ; le filtre global
/// <c>IsDeleted</c> du contexte s'applique aux définitions (aucune définition supprimée
/// n'est exposée en 4.1 — la corbeille arrive en 4.2).
/// </summary>
public interface IStudioWorkflowRepository
{
    /// <summary>Définitions non supprimées d'une table (actives, et inactives si <paramref name="includeInactive"/>), triées par nom.</summary>
    Task<IReadOnlyList<StudioWorkflowDefinition>> ListByEntityAsync(
        Guid tenantId, Guid entityDefinitionId, bool includeInactive = true, CancellationToken cancellationToken = default);

    /// <summary>Définitions actives non supprimées d'une table pour un déclencheur donné, triées par nom.</summary>
    Task<IReadOnlyList<StudioWorkflowDefinition>> ListActiveByTriggerAsync(
        Guid tenantId, Guid entityDefinitionId, StudioWorkflowTriggerKind trigger, CancellationToken cancellationToken = default);

    Task<StudioWorkflowDefinition?> GetDefinitionAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Définition non supprimée par sa clé (comparaison collation SQL, insensible à la casse par défaut).</summary>
    Task<StudioWorkflowDefinition?> GetDefinitionByKeyAsync(
        Guid tenantId, Guid entityDefinitionId, string key, CancellationToken cancellationToken = default);

    /// <summary>Nombre de définitions non supprimées de la table (quota plan).</summary>
    Task<int> CountByEntityAsync(Guid tenantId, Guid entityDefinitionId, CancellationToken cancellationToken = default);

    Task AddDefinitionAsync(StudioWorkflowDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Met à jour une définition avec contrôle de concurrence optimiste : si
    /// <paramref name="expectedRowVersion"/> est fourni, il devient la valeur d'origine du jeton
    /// et EF lève <c>DbUpdateConcurrencyException</c> si la ligne a changé depuis sa lecture
    /// (patron <c>CustomRecordRepository.UpdateWithConcurrencyAsync</c>).
    /// </summary>
    Task UpdateDefinitionWithConcurrencyAsync(
        StudioWorkflowDefinition definition, byte[]? expectedRowVersion, CancellationToken cancellationToken = default);

    Task<StudioWorkflowInstance?> GetInstanceAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Instances d'un enregistrement, les plus récentes d'abord ; <paramref name="max"/> borné à [1, 200].</summary>
    Task<IReadOnlyList<StudioWorkflowInstance>> ListInstancesForRecordAsync(
        Guid tenantId, Guid recordId, int max, CancellationToken cancellationToken = default);

    /// <summary>Instances d'une définition, les plus récentes d'abord ; <paramref name="max"/> borné à [1, 200].</summary>
    Task<IReadOnlyList<StudioWorkflowInstance>> ListInstancesForDefinitionAsync(
        Guid tenantId, Guid definitionId, int max, CancellationToken cancellationToken = default);

    /// <summary>Instances ouvertes (Running, Waiting, WaitingApproval) d'une définition, les plus récentes d'abord.</summary>
    Task<IReadOnlyList<StudioWorkflowInstance>> ListOpenInstancesForDefinitionAsync(
        Guid tenantId, Guid definitionId, CancellationToken cancellationToken = default);

    /// <summary>Nombre d'instances d'un enregistrement ; ouvertes seulement si <paramref name="openOnly"/> (quota anti-boucle).</summary>
    Task<int> CountInstancesForRecordAsync(Guid tenantId, Guid recordId, bool openOnly, CancellationToken cancellationToken = default);

    /// <summary>
    /// True si une instance ouverte de la même définition existe pour l'enregistrement dans la
    /// chaîne d'origine (<paramref name="originInstanceId"/> null ⇒ n'importe quelle origine ;
    /// sinon l'origine directe ou l'instance elle-même). Verrou anti-doublon au démarrage.
    /// </summary>
    Task<bool> HasOpenInstanceInChainAsync(
        Guid tenantId, Guid definitionId, Guid recordId, Guid? originInstanceId, CancellationToken cancellationToken = default);

    Task AddInstanceAsync(StudioWorkflowInstance instance, CancellationToken cancellationToken = default);

    /// <summary>Met à jour une instance ; <c>DbUpdateConcurrencyException</c> remonte telle quelle (jeton RowVersion).</summary>
    Task UpdateInstanceAsync(StudioWorkflowInstance instance, CancellationToken cancellationToken = default);

    /// <summary>Ajoute une exécution d'étape (journal append-only).</summary>
    Task AddStepRunAsync(StudioWorkflowStepRun stepRun, CancellationToken cancellationToken = default);

    /// <summary>Exécutions d'étapes d'une instance, triées par <c>StepIndex</c> puis <c>StartedAt</c>.</summary>
    Task<IReadOnlyList<StudioWorkflowStepRun>> ListStepRunsAsync(Guid tenantId, Guid instanceId, CancellationToken cancellationToken = default);

    Task AddApprovalAsync(StudioWorkflowApproval approval, CancellationToken cancellationToken = default);

    Task<StudioWorkflowApproval?> GetApprovalAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Approbations en attente d'une instance, les plus anciennes d'abord.</summary>
    Task<IReadOnlyList<StudioWorkflowApproval>> ListPendingApprovalsForInstanceAsync(
        Guid tenantId, Guid instanceId, CancellationToken cancellationToken = default);

    Task UpdateApprovalAsync(StudioWorkflowApproval approval, CancellationToken cancellationToken = default);

    /// <summary>Nombre d'instances ouvertes d'une définition (D9 : alimente <c>WorkflowDefinitionDto.OpenInstances</c>).</summary>
    Task<int> CountOpenInstancesForDefinitionAsync(Guid tenantId, Guid definitionId, CancellationToken cancellationToken = default);
}
