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
using System.Security.Cryptography;
using System.Text;

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

    /// <summary>
    /// Review R4 — logs must never carry a raw, client-supplied segment/domain string that hasn't
    /// yet been validated against the catalog (an unrecognized value could be arbitrary free text
    /// typed into a registration form field). Returns a length + short SHA-256 fingerprint instead
    /// — enough to correlate repeated/identical rejected inputs in logs and metrics without ever
    /// exposing their content. Once a segment/domain IS a known catalog code (the "incoherent
    /// couple" rejection below), it is safe to log verbatim — it can only be one of a small,
    /// non-sensitive, publicly documented set of values.
    /// </summary>
    private static string SafeInputSummary(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "empty";

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..8];
        return $"len={raw.Length};sha256_8={hash}";
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
            _logger.LogWarning(
                "RegistrationSectorService.ResolveProfile: rejected domain without segment (domain={DomainSummary}).",
                SafeInputSummary(normalizedDomain));

            return Result.Failure<SectorProfile?>(
                Error.Validation("CompanySegment", "Type de société requis lorsque le domaine est fourni."));
        }

        var snapshot = _catalogProvider.GetSnapshot();
        var segmentSnapshot = normalizedSegment is null
            ? null
            : snapshot.Segments.FirstOrDefault(s => string.Equals(s.Code, normalizedSegment, StringComparison.Ordinal));

        if (normalizedSegment is not null && segmentSnapshot is null)
        {
            _logger.LogWarning(
                "RegistrationSectorService.ResolveProfile: rejected unknown segment (segmentSummary={SegmentSummary}).",
                SafeInputSummary(normalizedSegment));

            return Result.Failure<SectorProfile?>(
                Error.Validation("CompanySegment", "Type de société invalide."));
        }

        if (normalizedDomain is not null && !snapshot.Domains.Any(d => string.Equals(d.Code, normalizedDomain, StringComparison.Ordinal)))
        {
            _logger.LogWarning(
                "RegistrationSectorService.ResolveProfile: rejected unknown domain (domainSummary={DomainSummary}).",
                SafeInputSummary(normalizedDomain));

            return Result.Failure<SectorProfile?>(
                Error.Validation("BusinessDomain", "Domaine d'activité invalide."));
        }

        // Phase 1 dynamic configuration (plan §3.2 D4): enforced whenever the segment has an
        // explicit, non-empty domain link list — regardless of the snapshot source (static catalog
        // or DB rules). An empty list means "no restriction" (defense against an admin
        // accidentally pruning every link and bricking registration), and the kill-switch
        // `EnforceSegmentDomainLinks=false` restores the fully permissive Phase 0 behavior
        // instantly without a redeploy.
        if (_options.EnforceSegmentDomainLinks
            && normalizedDomain is not null
            && segmentSnapshot is not null
            && segmentSnapshot.DomainCodes.Count > 0
            && !segmentSnapshot.DomainCodes.Contains(normalizedDomain, StringComparer.Ordinal))
        {
            _logger.LogWarning(
                "RegistrationSectorService.ResolveProfile: rejected incoherent segment/domain couple (segment={Segment}, domain={Domain}, snapshotSource={Source}).",
                normalizedSegment,
                normalizedDomain,
                snapshot.Source);

            return Result.Failure<SectorProfile?>(
                Error.Validation("BusinessDomain", "Domaine d'activité non disponible pour ce type de société."));
        }

        var profile = snapshot.Resolve(normalizedSegment, normalizedDomain);
        return Result.Success(profile);
    }

    public async Task<ModuleSelectionOutcome> ApplyModuleSelectionAsync(
        Guid userId,
        SectorProfile? profile,
        IReadOnlyList<int>? requestedModules,
        SubscriptionPlan plan,
        CancellationToken cancellationToken)
    {
        // Kill-switch gate: a disabled deployment must never write grant rows, even if a caller
        // still sends enabledModules (e.g. a stale/misbehaving client, or the flag toggled off
        // mid-rollout). This mirrors the same gate ResolveProfile applies for the profile itself.
        // Plan §1.2: report back that the selection was ignored so the caller can warn the client
        // instead of silently applying legacy all-modules behavior with no explanation.
        if (!_options.Enabled)
        {
            if (requestedModules is { Count: > 0 })
            {
                _logger.LogWarning(
                    "RegistrationSectorService.ApplyModuleSelectionAsync: kill-switch disabled — ignoring {Count} requested module id(s) for user {UserId}.",
                    requestedModules.Count,
                    userId);
                return ModuleSelectionOutcome.IgnoredKillSwitch;
            }

            return ModuleSelectionOutcome.Empty;
        }

        // Absent/empty selection ⇒ write nothing. UserModuleGrant absence means "all modules
        // enabled" (EffectivePermissionService.ResolveEnabledModules) — this is the exact legacy
        // behavior for payloads that don't opt into the wizard's module step.
        if (requestedModules is null || requestedModules.Count == 0)
            return ModuleSelectionOutcome.Empty;

        var coreModules = profile?.CoreModules ?? SectorConfigurationCatalog.CoreModules;
        var dependencyEdges = _catalogProvider.GetSnapshot().ModuleDependencies;

        // The candidate/plan-intersection logic is shared with the tenant re-configuration flow
        // (plan §WP-B7, D6) via SectorModuleSetCalculator — enum filtering, Honoraires rejection,
        // dependency closure, Administration carve-out and plan ceiling all live in one place.
        var computation = await SectorModuleSetCalculator.ComputeAsync(
            coreModules,
            requestedModules,
            plan,
            dependencyEdges,
            _planResolver,
            userId,
            _logger,
            cancellationToken);

        var finalSet = computation.EnabledModules;

        // Full selection ⇒ canonical legacy representation is "no grant rows".
        if (finalSet.Count == AppModuleExtensions.AllValues.Length)
        {
            return new ModuleSelectionOutcome
            {
                EnabledModules = finalSet.ToList(),
                DeniedByPlan = computation.DeniedByPlan,
                DroppedInvalidIds = computation.DroppedInvalidIds
            };
        }

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

        return new ModuleSelectionOutcome
        {
            EnabledModules = finalSet.ToList(),
            DeniedByPlan = computation.DeniedByPlan,
            DroppedInvalidIds = computation.DroppedInvalidIds
        };
    }
}
