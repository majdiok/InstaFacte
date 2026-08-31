using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.SectorConfiguration;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services.SectorRules;
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
        var dependencyEdges = _catalogProvider.GetSnapshot().ModuleDependencies;

        // The candidate/plan-intersection logic is shared with the tenant re-configuration flow
        // (plan §WP-B7, D6) via SectorModuleSetCalculator — enum filtering, Honoraires rejection,
        // dependency closure, Administration carve-out and plan ceiling all live in one place.
        var finalSet = await SectorModuleSetCalculator.ComputeEnabledSetAsync(
            coreModules,
            requestedModules,
            plan,
            dependencyEdges,
            _planResolver,
            userId,
            _logger,
            cancellationToken);

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
