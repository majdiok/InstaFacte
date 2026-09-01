using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.SectorConfiguration;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services.SectorRules;

/// <summary>
/// Shared module-set computation extracted from
/// <c>RegistrationSectorService.ApplyModuleSelectionAsync</c>'s candidate/plan-intersection logic
/// (plan §WP-B7, D6) so the registration wizard and the tenant re-configuration flow share one
/// implementation: enum filtering, Honoraires rejection, dependency transitive closure,
/// Administration carve-out, plan ceiling. The "full selection ⇒ no grant rows" canonicalization is
/// the caller's responsibility (the calculator only returns the final enabled set).
/// </summary>
public static class SectorModuleSetCalculator
{
    /// <summary>Defensive cap on the size of an incoming module-id seed (mirrors the registration guard).</summary>
    private const int MaxRequestedModules = 64;

    /// <summary>
    /// Computes the canonical enabled module set for the given seed.
    /// <list type="bullet">
    ///   <item><paramref name="coreModules"/> are always present in the candidate set (and survive the plan intersection, except Administration which is unconditional).</item>
    ///   <item><paramref name="seedModuleIds"/> are filtered: undefined enum values and <c>Honoraires</c> are dropped (ids only in the warning log — no PII).</item>
    ///   <item>The transitive closure of <paramref name="dependencyEdges"/> auto-pulls every required module (also filtered; Honoraires can never be pulled in — CRUD validation forbids it).</item>
    ///   <item>Every non-Administration candidate is intersected with the plan via <paramref name="planResolver"/>; Administration always survives regardless of plan configuration.</item>
    /// </list>
    /// Returns the final enabled <see cref="AppModule"/> set (caller writes grant rows, or nothing when it equals the full module universe).
    /// </summary>
    public static async Task<HashSet<AppModule>> ComputeEnabledSetAsync(
        IReadOnlyList<AppModule> coreModules,
        IEnumerable<int> seedModuleIds,
        SubscriptionPlan plan,
        IReadOnlyList<ModuleDependencySnapshot> dependencyEdges,
        IPlanResolver planResolver,
        Guid userId,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var result = await ComputeAsync(
            coreModules, seedModuleIds, plan, dependencyEdges, planResolver, userId, logger, cancellationToken);
        return result.EnabledModules;
    }

    /// <summary>
    /// Same computation as <see cref="ComputeEnabledSetAsync"/>, but also reports what happened to
    /// the client's explicitly-requested modules (plan §1.1/§1.2 — no silent rejections): which
    /// requested-but-valid modules got excluded by the plan ceiling, and which raw ids were dropped
    /// outright (undefined enum values, or Honoraires). Additive — the original method above is
    /// unchanged and keeps its exact historical signature/behavior for existing callers/tests.
    /// </summary>
    public static async Task<ModuleSetComputationResult> ComputeAsync(
        IReadOnlyList<AppModule> coreModules,
        IEnumerable<int> seedModuleIds,
        SubscriptionPlan plan,
        IReadOnlyList<ModuleDependencySnapshot> dependencyEdges,
        IPlanResolver planResolver,
        Guid userId,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var coreModuleSet = new HashSet<AppModule>(coreModules);
        var candidateSet = new HashSet<AppModule>(coreModuleSet);
        var requestedValidModules = new HashSet<AppModule>();
        var droppedValues = new List<int>();
        var consideredCount = 0;

        foreach (var rawValue in seedModuleIds)
        {
            if (consideredCount >= MaxRequestedModules)
            {
                droppedValues.Add(rawValue);
                continue;
            }

            consideredCount++;

            // Undefined enum values and Honoraires (firm-native, never offered through the registration
            // wizard) are dropped — ids only in the warning log, no PII.
            if (!TryAsOfferedModule(rawValue, out var module))
            {
                droppedValues.Add(rawValue);
                continue;
            }

            candidateSet.Add(module);
            requestedValidModules.Add(module);
        }

        if (droppedValues.Count > 0)
        {
            logger.LogWarning(
                "SectorModuleSetCalculator: dropped {Count} invalid/disallowed module id(s) for user {UserId}: {Values}",
                droppedValues.Count,
                userId,
                string.Join(",", droppedValues));
        }

        // Dependency transitive closure (plan §WP-B4): each auto-added module flows through the
        // same plan-ceiling intersection below as a user-requested one — a plan that denies the
        // required module still wins. Honoraires can never be pulled in (firm-native; CRUD
        // validation forbids it as a dependency target — WP-B5).
        if (dependencyEdges.Count > 0)
        {
            var requiredModulesByModuleId = dependencyEdges
                .ToLookup(edge => edge.ModuleId, edge => edge.RequiredModuleId);

            var autoAddedModules = new List<AppModule>();
            var pending = new Queue<AppModule>(candidateSet);
            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                foreach (var requiredModuleId in requiredModulesByModuleId[(int)current])
                {
                    if (!TryAsOfferedModule(requiredModuleId, out var requiredModule))
                        continue;

                    if (candidateSet.Add(requiredModule))
                    {
                        autoAddedModules.Add(requiredModule);
                        pending.Enqueue(requiredModule);
                    }
                }
            }

            if (autoAddedModules.Count > 0)
            {
                logger.LogInformation(
                    "SectorModuleSetCalculator: auto-pulled {Count} dependency module id(s) for user {UserId}: {Values}",
                    autoAddedModules.Count,
                    userId,
                    string.Join(",", autoAddedModules.Select(m => (int)m)));
            }
        }

        // Plan-intersection (review fix — plan §review item 2): the downstream resolution pipeline
        // (EffectivePermissionsCalculator / EffectivePermissionService) is role- and grant-based only
        // — it never re-checks IPlanResolver. Anything marked IsEnabled=true here rides straight
        // through to login/JWT with no second gate. So core modules must NOT be forced on
        // unconditionally: every candidate — core or not — is intersected with the plan, with a
        // single carve-out for Administration (mirrors the existing self-lockout rule). A plan that
        // denies any other core module is a misconfiguration; we honor the plan (deny) but log it.
        var finalSet = new HashSet<AppModule>();
        foreach (var module in candidateSet)
        {
            if (module == AppModule.Administration)
            {
                finalSet.Add(module);
                continue;
            }

            var isAllowedByPlan = await planResolver.IsModuleAllowedAsync(plan, (int)module, cancellationToken);
            if (isAllowedByPlan)
            {
                finalSet.Add(module);
                continue;
            }

            if (coreModuleSet.Contains(module))
            {
                logger.LogWarning(
                    "SectorModuleSetCalculator: plan {Plan} denies core module {Module} for user {UserId} — excluding it from the grant (inconsistent plan configuration; core modules are normally always allowed).",
                    plan,
                    module,
                    userId);
            }
        }

        // Denied-by-plan report (plan §1.1/§1.2): only modules the client explicitly asked for
        // (requestedValidModules) — never core/dependency-auto-pulled ones the client never
        // requested — count as a denial worth surfacing back to them.
        var deniedByPlan = requestedValidModules.Where(m => !finalSet.Contains(m)).ToList();

        return new ModuleSetComputationResult
        {
            EnabledModules = finalSet,
            DeniedByPlan = deniedByPlan,
            DroppedInvalidIds = droppedValues
        };
    }

    /// <summary>
    /// Maps a raw module id to a wizard-offered <see cref="AppModule"/>: undefined enum values and
    /// <see cref="AppModule.Honoraires"/> (firm-native, never offered) are rejected. Shared by the
    /// seed filter and the dependency transitive closure so both apply the same eligibility rule.
    /// </summary>
    private static bool TryAsOfferedModule(int rawValue, out AppModule module)
    {
        if (Enum.IsDefined(typeof(AppModule), rawValue) && (AppModule)rawValue != AppModule.Honoraires)
        {
            module = (AppModule)rawValue;
            return true;
        }

        module = default;
        return false;
    }
}

/// <summary>
/// Detailed result of <see cref="SectorModuleSetCalculator.ComputeAsync"/> (plan §1.1/§1.2) — lets
/// the caller report denied/dropped modules instead of silently swallowing that information.
/// </summary>
public sealed record ModuleSetComputationResult
{
    public required HashSet<AppModule> EnabledModules { get; init; }
    public required IReadOnlyList<AppModule> DeniedByPlan { get; init; }
    public required IReadOnlyList<int> DroppedInvalidIds { get; init; }
}
