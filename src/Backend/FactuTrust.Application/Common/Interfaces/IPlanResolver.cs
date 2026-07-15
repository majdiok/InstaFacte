using FactuTrust.Domain.Billing;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Lot C1 — Résolveur de plan : retourne les limites/features/modules effectifs pour un tenant.
///
/// <b>Stratégie de résolution</b> (dans l'ordre) :
/// <list type="number">
///   <item>Si la table <c>Plans</c> contient un plan correspondant au <see cref="SubscriptionPlan"/>
///         de la subscription, ses lignes <c>PlanLimits</c>/<c>PlanFeatures</c>/<c>PlanModules</c> sont utilisées.</item>
///   <item>Sinon, fallback sur l'enum <see cref="SubscriptionLimits"/> hardcodé (rétro-compat 100 %).</item>
/// </list>
///
/// La résolution est cachée pour 30 secondes par tenant pour éviter N requêtes par check.
/// </summary>
public interface IPlanResolver
{
    Task<Plan?> GetPlanByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<bool> HasFeatureAsync(SubscriptionPlan plan, string featureKey, CancellationToken cancellationToken = default);

    Task<int> GetIntLimitAsync(SubscriptionPlan plan, string limitKey, int fallback, CancellationToken cancellationToken = default);

    Task<bool> IsModuleAllowedAsync(SubscriptionPlan plan, int module, CancellationToken cancellationToken = default);
}
