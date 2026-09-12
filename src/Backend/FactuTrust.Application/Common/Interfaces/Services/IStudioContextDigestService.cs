namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Résumés compacts du contexte Studio d'un tenant, injectés dans le prompt StudioBuilder pour que le
/// modèle raisonne sur les VRAIES clés (tables, champs) plutôt que sur des noms inventés.
/// Aucune valeur d'enregistrement n'est jamais incluse : uniquement des métadonnées de schéma.
/// </summary>
public interface IStudioContextDigestService
{
    /// <summary>
    /// Une ligne par table Studio active du tenant :
    /// <c>- cle « Libellé » (systeme:cle_sys) : champ1:type, champ2:type…</c>, champs inactifs exclus.
    /// Tronqué à <paramref name="maxChars"/> avec le suffixe <c>… (+N tables)</c>. Chaîne vide si aucune table.
    /// Mis en cache par tenant (TTL court) : le coût N+1 (une lecture de champs par table) n'est payé
    /// qu'une fois par fenêtre de cache.
    /// </summary>
    Task<string> BuildSchemaDigestAsync(Guid tenantId, int maxChars, CancellationToken cancellationToken);

    /// <summary>
    /// Dernier plan de l'utilisateur : plan <c>Pending</c> (non expiré) et dernier plan <c>Completed</c>
    /// de moins de 24 h, sous la forme
    /// <c>- [Terminé 12:04] CreateSystem « Gestion congés » : 4 tables (employes, …)</c>.
    /// <c>null</c> s'il n'y a rien à résumer. Tronqué à <paramref name="maxChars"/>.
    /// </summary>
    Task<string?> BuildLastPlanDigestAsync(Guid tenantId, string userId, int maxChars, CancellationToken cancellationToken);
}
