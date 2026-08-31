using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.SectorConfiguration;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Phase 1 implementation of <see cref="IRegistrationSectorService"/> — plan §3 C8, §6.1 B4.
/// Resolves against the static <see cref="SectorConfigurationCatalog"/>; Phase 2 swaps this class's
/// internals to a DB-backed rule engine behind the same interface (zero controller changes).
/// </summary>
public sealed class RegistrationSectorService : IRegistrationSectorService
{
    /// <summary>Defensive cap on the size of an incoming <c>enabledModules</c> array (plan §6.1 B4).</summary>
    private const int MaxRequestedModules = 64;

    private readonly MasterDbContext _db;
    private readonly IPlanResolver _planResolver;
    private readonly ISectorCatalogProvider _catalogProvider;
    private readonly RegistrationSectorOptions _options;
    private readonly ILogger<RegistrationSectorService> _logger;

    public RegistrationSectorService(
        MasterDbContext db,
        IPlanResolver planResolver,
        ISectorCatalogProvider catalogProvider,
        IOptions<RegistrationSectorOptions> options,
        ILogger<RegistrationSectorService> logger)
    {
        _db = db;
        _planResolver = planResolver;
        _catalogProvider = catalogProvider;
        _options = options.Value;
        _logger = logger;
    }

    public Result<SectorProfile?> ResolveProfile(string? companySegment, string? businessDomain)
    {
        if (!_options.Enabled)
            return Result.Success<SectorProfile?>(null);

        var normalizedSegment = CompanySegments.Normalize(companySegment);
        var normalizedDomain = BusinessDomains.Normalize(businessDomain);

        if (normalizedSegment is null && normalizedDomain is null)
            return Result.Success<SectorProfile?>(null);

        if (normalizedDomain is not null && normalizedSegment is null)
        {
            return Result.Failure<SectorProfile?>(
                Error.Validation("CompanySegment", "Type de société requis lorsque le domaine est fourni."));
        }

        var snapshot = _catalogProvider.GetSnapshot();
        var segmentSnapshot = normalizedSegment is null
            ? null
            : snapshot.Segments.FirstOrDefault(s => string.Equals(s.Code, normalizedSegment, StringComparison.Ordinal));

        if (normalizedSegment is not null && segmentSnapshot is null)
        {
            return Result.Failure<SectorProfile?>(
                Error.Validation("CompanySegment", "Type de société invalide."));
        }

        if (normalizedDomain is not null && !snapshot.Domains.Any(d => string.Equals(d.Code, normalizedDomain, StringComparison.Ordinal)))
        {
            return Result.Failure<SectorProfile?>(
                Error.Validation("BusinessDomain", "Domaine d'activité invalide."));
        }

        // Phase 2 (plan §WP-B4, D4): only enforced when the active snapshot comes from the DB
        // AND the segment has an explicit, non-empty domain link list — an empty list (or the
        // static snapshot, which always lists every domain) means "no restriction", preserving
        // Phase 1 behavior and protecting against an admin accidentally bricking registration by
        // pruning every link.
        if (normalizedDomain is not null
            && snapshot.Source == SectorRuleSource.Db
            && segmentSnapshot is not null
            && segmentSnapshot.DomainCodes.Count > 0
            && !segmentSnapshot.DomainCodes.Contains(normalizedDomain, StringComparer.Ordinal))
        {
            return Result.Failure<SectorProfile?>(
                Error.Validation("BusinessDomain", "Domaine d'activité non disponible pour ce type de société."));
        }

        var profile = snapshot.Resolve(normalizedSegment, normalizedDomain);
        return Result.Success(profile);
    }

    public async Task ApplyModuleSelectionAsync(
        Guid userId,
        SectorProfile? profile,
        IReadOnlyList<int>? requestedModules,
        SubscriptionPlan plan,
        CancellationToken cancellationToken)
    {
        // Kill-switch gate: a disabled deployment must never write grant rows, even if a caller
        // still sends enabledModules (e.g. a stale/misbehaving client, or the flag toggled off
        // mid-rollout). This mirrors the same gate ResolveProfile applies for the profile itself.
        if (!_options.Enabled)
            return;

        // Absent/empty selection ⇒ write nothing. UserModuleGrant absence means "all modules
        // enabled" (EffectivePermissionService.ResolveEnabledModules) — this is the exact legacy
        // behavior for payloads that don't opt into the wizard's module step.
        if (requestedModules is null || requestedModules.Count == 0)
            return;

        var coreModules = profile?.CoreModules ?? SectorConfigurationCatalog.CoreModules;
        var coreModuleSet = new HashSet<AppModule>(coreModules);

        var candidateSet = new HashSet<AppModule>(coreModuleSet);
        var droppedValues = new List<int>();
        var consideredCount = 0;

        foreach (var rawValue in requestedModules)
        {
            if (consideredCount >= MaxRequestedModules)
            {
                droppedValues.Add(rawValue);
                continue;
            }

            consideredCount++;

            if (!Enum.IsDefined(typeof(AppModule), rawValue))
            {
                droppedValues.Add(rawValue);
                continue;
            }

            var module = (AppModule)rawValue;

            // Honoraires is firm-native and never offered through the registration wizard.
            if (module == AppModule.Honoraires)
            {
                droppedValues.Add(rawValue);
                continue;
            }

            candidateSet.Add(module);
        }

        if (droppedValues.Count > 0)
        {
            _logger.LogWarning(
                "RegistrationSectorService.ApplyModuleSelectionAsync: dropped {Count} invalid/disallowed module id(s) for user {UserId}: {Values}",
                droppedValues.Count,
                userId,
                string.Join(",", droppedValues));
        }

        // Plan-intersection (review fix — plan §review item 2): the downstream resolution
        // pipeline (EffectivePermissionsCalculator.Compute / EffectivePermissionService) is
        // role- and grant-based only — it never re-checks IPlanResolver, and
        // IsModuleAllowedAsync has no other caller in the codebase. That means anything we mark
        // IsEnabled=true here rides straight through to login/JWT with no second gate. So core
        // modules must NOT be forced on unconditionally: every candidate — core or not — is
        // intersected with the plan, with a single carve-out for Administration, which mirrors
        // the existing TenantUsersController self-lockout rule (a user can never disable their
        // own Administration access) and must never be switched off regardless of plan
        // configuration. A plan that denies any other core module is a misconfiguration; we
        // honor the plan (deny) but log it loudly so it gets fixed upstream.
        var finalSet = new HashSet<AppModule>();
        foreach (var module in candidateSet)
        {
            if (module == AppModule.Administration)
            {
                finalSet.Add(module);
                continue;
            }

            var isAllowedByPlan = await _planResolver.IsModuleAllowedAsync(plan, (int)module, cancellationToken);
            if (isAllowedByPlan)
            {
                finalSet.Add(module);
                continue;
            }

            if (coreModuleSet.Contains(module))
            {
                _logger.LogWarning(
                    "RegistrationSectorService.ApplyModuleSelectionAsync: plan {Plan} denies core module {Module} for user {UserId} — excluding it from the grant (inconsistent plan configuration; core modules are normally always allowed).",
                    plan,
                    module,
                    userId);
            }
        }

        // Full selection ⇒ canonical legacy representation is "no grant rows".
        if (finalSet.Count == AppModuleExtensions.AllValues.Length)
            return;

        foreach (var module in AppModuleExtensions.AllValues)
        {
            _db.UserModuleGrants.Add(new UserModuleGrant
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Module = module,
                IsEnabled = finalSet.Contains(module),
                EnabledFeatureKeys = null
            });
        }

        _logger.LogInformation(
            "RegistrationSectorService.ApplyModuleSelectionAsync: wrote {Count} module grant row(s) for user {UserId} (segment={Segment}, domain={Domain}, enabled={Enabled})",
            AppModuleExtensions.AllValues.Length,
            userId,
            profile?.SegmentCode,
            profile?.DomainCode,
            string.Join(",", finalSet));
    }
}
