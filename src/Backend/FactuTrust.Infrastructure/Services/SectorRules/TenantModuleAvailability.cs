using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services.SectorRules;

/// <summary>
/// Définition unique de « quels modules cette société possède ».
///
/// Extraite de <c>CompanyModulesController.ComputeEnabledModuleSetAsync</c> pour que la page
/// Paramètres &gt; Modules et la boîte « Ajouter un utilisateur » répondent exactement la même
/// chose : sans cela, l'écran d'ajout d'utilisateur proposait des modules que la société n'a pas,
/// et le serveur les accordait.
///
/// Référence de lecture : les autorisations de l'administrateur agissant. C'est la convention déjà
/// posée par la décision D4 du plan §2.2 — il n'existe pas de table d'autorisations au niveau
/// société, <c>UserModuleGrant</c> reste la source de vérité par utilisateur et le <c>PUT
/// /api/company/modules</c> maintient tous les utilisateurs actifs en phase. Les deux endpoints
/// concernés sont réservés aux administrateurs, donc la référence est toujours un administrateur.
/// </summary>
public static class TenantModuleAvailability
{
    /// <summary>
    /// Modules accordés à <paramref name="referenceUserId"/>.
    ///
    /// Aucune ligne d'autorisation ⇒ <b>tous</b> les modules. C'est la convention héritée
    /// (<c>EffectivePermissionService.ResolveEnabledModules</c>) et elle est ce qui garantit
    /// l'absence de régression : un espace créé avant la sélection de modules, ou sans elle, n'a
    /// aucune ligne, donc tout filtrage bâti là-dessus y est neutre. Ne pas « durcir » ce repli.
    /// </summary>
    public static async Task<HashSet<AppModule>> ReadGrantedModulesAsync(
        MasterDbContext db,
        Guid referenceUserId,
        CancellationToken cancellationToken)
    {
        var grants = await db.UserModuleGrants.AsNoTracking()
            .Where(g => g.UserId == referenceUserId)
            .ToListAsync(cancellationToken);

        if (grants.Count == 0)
            return new HashSet<AppModule>(AppModuleExtensions.AllValues);

        return grants.Where(g => g.IsEnabled).Select(g => g.Module).ToHashSet();
    }

    /// <summary>
    /// Ce que la société peut réellement accorder : modules accordés <b>∩</b> modules autorisés par
    /// le plan. Les deux plafonds sont nécessaires — le premier traduit un choix de la société, le
    /// second une limite de l'abonnement.
    ///
    /// <c>Administration</c> est conservé quoi qu'il arrive, en écho à la règle anti-auto-exclusion
    /// déjà appliquée par <see cref="SectorModuleSetCalculator"/> : personne ne doit pouvoir se
    /// retrouver enfermé hors des paramètres de son propre espace.
    /// </summary>
    public static async Task<HashSet<AppModule>> ComputeAvailableModulesAsync(
        MasterDbContext db,
        IPlanResolver planResolver,
        SubscriptionPlan plan,
        Guid referenceUserId,
        CancellationToken cancellationToken)
    {
        var granted = await ReadGrantedModulesAsync(db, referenceUserId, cancellationToken);
        var available = new HashSet<AppModule>();

        foreach (var module in granted)
        {
            if (module == AppModule.Administration)
            {
                available.Add(module);
                continue;
            }

            if (await planResolver.IsModuleAllowedAsync(plan, (int)module, cancellationToken))
                available.Add(module);
        }

        return available;
    }
}
