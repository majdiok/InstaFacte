using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Billing;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.SectorConfiguration;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Lot C1 — Résolveur de plan basé sur la BD master, avec fallback transparent vers
/// l'enum <see cref="SubscriptionLimits"/> hardcodé.
///
/// Les valeurs résolues sont cachées 30 secondes dans <see cref="IMemoryCache"/> par <c>code</c>.
/// </summary>
public sealed class DbPlanResolver : IPlanResolver
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    private readonly MasterDbContext _db;
    private readonly IMemoryCache _cache;

    public DbPlanResolver(MasterDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<Plan?> GetPlanByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var cacheKey = $"plan:by-code:{code.Trim()}";
        if (_cache.TryGetValue<Plan>(cacheKey, out var cached) && cached is not null)
            return cached;

        var plan = await _db.Plans
            .AsNoTracking()
            .Include(p => p.Limits)
            .Include(p => p.Features)
            .Include(p => p.Modules)
            .FirstOrDefaultAsync(p => p.Code == code.Trim(), cancellationToken);

        if (plan is not null)
        {
            _cache.Set(cacheKey, plan, CacheDuration);
        }
        return plan;
    }

    public async Task<bool> HasFeatureAsync(SubscriptionPlan plan, string featureKey, CancellationToken cancellationToken = default)
    {
        var dbPlan = await GetPlanByCodeAsync(plan.ToString(), cancellationToken);
        if (dbPlan is not null)
        {
            var feature = dbPlan.Features.FirstOrDefault(f =>
                string.Equals(f.FeatureKey, featureKey, StringComparison.OrdinalIgnoreCase));
            if (feature is not null) return feature.Enabled;
        }
        // Fallback enum
        return SubscriptionLimits.HasFeature(plan, featureKey);
    }

    public async Task<int> GetIntLimitAsync(SubscriptionPlan plan, string limitKey, int fallback, CancellationToken cancellationToken = default)
    {
        var dbPlan = await GetPlanByCodeAsync(plan.ToString(), cancellationToken);
        if (dbPlan is not null)
        {
            var limit = dbPlan.Limits.FirstOrDefault(l =>
                string.Equals(l.Key, limitKey, StringComparison.OrdinalIgnoreCase));
            if (limit is not null && TryParseLimit(limit.Value, out var parsed))
                return parsed;
        }
        return fallback;
    }

    /// <summary>
    /// Modules cœur (Clients, Produits, Ventes, Trésorerie, Rapports, Paramètres) — jamais soumis
    /// au plafond du plan. Le produit les traite déjà comme non négociables partout ailleurs :
    /// l'assistant d'inscription les présente en « Inclus dans votre espace » sans interrupteur,
    /// <c>CompanyModuleReconfigurationValidator.ValidateCoreModules</c> refuse de les désactiver, et
    /// <c>/settings/modules</c> les affiche avec le badge « Cœur ».
    ///
    /// La règle est portée ICI, dans l'unique garde que tous les appelants traversent déjà
    /// (<c>SectorModuleSetCalculator</c>, <c>CompanyModulesController</c>, <c>TenantUsersController</c>),
    /// et non dans le calculateur : celui-ci documente à juste titre qu'il ne doit jamais forcer un
    /// module en contournant le plafond, puisque la résolution aval
    /// (<c>EffectivePermissionsCalculator</c>) ne rejoue jamais <c>IPlanResolver</c>. On ne contourne
    /// donc pas le gardien — on corrige sa réponse, et tous les consommateurs héritent de la même règle.
    /// </summary>
    private static readonly IReadOnlySet<int> CoreModuleIds =
        SectorConfigurationCatalog.CoreModules.Select(m => (int)m).ToHashSet();

    public async Task<bool> IsModuleAllowedAsync(SubscriptionPlan plan, int module, CancellationToken cancellationToken = default)
    {
        // Une ligne de plan mal configurée ne doit jamais pouvoir produire un espace sans clients,
        // sans produits ni facturation (voir CoreModuleIds).
        if (CoreModuleIds.Contains(module))
            return true;

        var dbPlan = await GetPlanByCodeAsync(plan.ToString(), cancellationToken);
        if (dbPlan is not null)
        {
            // Plan BD "plat" (zéro PlanModule) → rétro-compat plans pré-Lot C1 :
            // tous les modules sont autorisés tant que l'admin n'a pas configuré
            // explicitement la liste.
            if (dbPlan.Modules.Count == 0) return true;

            // Plan BD doté d'une configuration de modules → la BD fait foi.
            // L'absence d'une ligne PlanModule pour ce module signifie « décoché dans l'UI »
            // donc on REFUSE l'accès (et non plus l'autoriser silencieusement).
            var entry = dbPlan.Modules.FirstOrDefault(m => m.Module == module);
            return entry?.IsIncluded ?? false;
        }
        // dbPlan null → tenant sur plan enum-only legacy : on garde la rétro-compat permissive.
        return true;
    }

    private static bool TryParseLimit(string value, out int parsed)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            parsed = 0;
            return false;
        }
        var trimmed = value.Trim();
        if (trimmed == "∞" || trimmed.Equals("unlimited", StringComparison.OrdinalIgnoreCase))
        {
            parsed = int.MaxValue;
            return true;
        }
        return int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
    }
}
