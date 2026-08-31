using System.Text.Json;
using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.SectorConfiguration;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services.SectorRules;

/// <summary>
/// Phase 2 — tenant sector re-configuration: preview and apply a segment/domain change for an
/// existing tenant from the backoffice (plan §WP-B7, D6). Module grants are recomputed via the
/// shared <see cref="SectorModuleSetCalculator"/>; sector data templates are applied additively via
/// <see cref="ISectorDataTemplateApplier"/>; every step is fault-isolated (fail-continue, per
/// <c>TenantMigrationHelper</c> precedent) and a hash-chained audit row is written to the tenant DB
/// (user GUIDs only — no emails/names).
/// </summary>
public sealed class TenantSectorReconfigurationService : ITenantSectorReconfigurationService
{
    /// <summary>Cap on the number of users a single re-configuration touches (plan §WP-B7 security matrix).</summary>
    private const int MaxUsersPerRun = 500;

    internal const string AuditAction = "sector-reconfiguration";

    private readonly MasterDbContext _db;
    private readonly IRegistrationSectorService _registrationSectorService;
    private readonly ISectorCatalogProvider _catalogProvider;
    private readonly ISectorDataTemplateApplier _templateApplier;
    private readonly IPlanResolver _planResolver;
    private readonly ITenantService _tenantService;
    private readonly ITenantDbContextFactory _tenantContextFactory;
    private readonly ILogger<TenantSectorReconfigurationService> _logger;

    public TenantSectorReconfigurationService(
        MasterDbContext db,
        IRegistrationSectorService registrationSectorService,
        ISectorCatalogProvider catalogProvider,
        ISectorDataTemplateApplier templateApplier,
        IPlanResolver planResolver,
        ITenantService tenantService,
        ITenantDbContextFactory tenantContextFactory,
        ILogger<TenantSectorReconfigurationService> logger)
    {
        _db = db;
        _registrationSectorService = registrationSectorService;
        _catalogProvider = catalogProvider;
        _templateApplier = templateApplier;
        _planResolver = planResolver;
        _tenantService = tenantService;
        _tenantContextFactory = tenantContextFactory;
        _logger = logger;
    }

    public async Task<Result<SectorReconfigurationPreviewDto>> PreviewAsync(
        Guid tenantId,
        SectorReconfigurationRequestDto request,
        Guid? actorAdminId,
        CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant is null)
            return Result.Failure<SectorReconfigurationPreviewDto>(
                Error.Validation(ITenantSectorReconfigurationService.TenantNotFoundCode, $"Tenant {tenantId} introuvable."));

        var (targetSegment, targetDomain) = ComputeTarget(tenant, request);

        var profileResult = _registrationSectorService.ResolveProfile(targetSegment, targetDomain);
        if (profileResult.IsFailure)
            return Result.Failure<SectorReconfigurationPreviewDto>(profileResult.Error);

        var preview = await BuildPreviewAsync(tenant, targetSegment, targetDomain, profileResult.Value, request, cancellationToken);
        return Result.Success(preview);
    }

    public async Task<Result<SectorReconfigurationApplyResultDto>> ApplyAsync(
        Guid tenantId,
        SectorReconfigurationRequestDto request,
        Guid? actorAdminId,
        CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants.FindAsync(new object[] { tenantId }, cancellationToken);
        if (tenant is null)
            return Result.Failure<SectorReconfigurationApplyResultDto>(
                Error.Validation(ITenantSectorReconfigurationService.TenantNotFoundCode, $"Tenant {tenantId} introuvable."));

        var (targetSegment, targetDomain) = ComputeTarget(tenant, request);

        // Re-resolve the profile (same validation/400s as registration, incl. WP-B4 link check) BEFORE
        // any step runs — a validation failure short-circuits with no side effects.
        var profileResult = _registrationSectorService.ResolveProfile(targetSegment, targetDomain);
        if (profileResult.IsFailure)
            return Result.Failure<SectorReconfigurationApplyResultDto>(profileResult.Error);

        var profile = profileResult.Value;
        var afterSegment = profile?.SegmentCode;
        var afterDomain = profile?.DomainCode;
        var beforeSegment = tenant.CompanySegment;
        var beforeDomain = tenant.BusinessDomain;

        var plan = await ResolveTenantPlanAsync(tenantId, cancellationToken);
        var connectionString = await _tenantService.GetConnectionStringAsync(tenantId, cancellationToken);
        var users = await GetActiveUsersAsync(tenantId, request, cancellationToken);

        var steps = new List<StepResultDto>();
        var touchedUserIds = new List<Guid>();

        // Step 1 — classification (master DB).
        await RunFaultIsolatedStepAsync(steps, "classification", "Classification", tenantId, actorAdminId, async () =>
        {
            tenant.SetSectorClassification(afterSegment, afterDomain);
            await _db.SaveChangesAsync(cancellationToken);
        });

        // Step 2 — per-user module grants (master DB), only when RecomputeModuleGrants. Each user is
        // its own fault-isolated step; a failure is recorded and the run continues. Administration is
        // always on (guaranteed by SectorModuleSetCalculator). TenantModuleOverrides/plans/roles are
        // never touched.
        if (request.RecomputeModuleGrants)
        {
            var snapshot = _catalogProvider.GetSnapshot();
            var (coreModules, recommendedSeed) = ResolveModuleInputs(profile);
            var dependencyEdges = snapshot.ModuleDependencies;

            foreach (var user in users)
            {
                try
                {
                    var finalSet = await SectorModuleSetCalculator.ComputeEnabledSetAsync(
                        coreModules,
                        recommendedSeed,
                        plan,
                        dependencyEdges,
                        _planResolver,
                        user.Id,
                        _logger,
                        cancellationToken);

                    await RewriteUserGrantsAsync(user.Id, finalSet, cancellationToken);
                    touchedUserIds.Add(user.Id);
                    steps.Add(new StepResultDto { Step = $"module-grants:{user.Id}", Success = true });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SectorReconfiguration.ModuleGrantsFailed TenantId={TenantId} UserId={UserId} ActorId={ActorId}", tenantId, user.Id, actorAdminId);
                    steps.Add(new StepResultDto { Step = $"module-grants:{user.Id}", Success = false, Error = ex.Message });
                }
            }
        }

        // Step 3 — sector data templates (tenant DB, additive-only), only when ApplyDataTemplates.
        if (request.ApplyDataTemplates)
        {
            await RunFaultIsolatedStepAsync(steps, "templates", "Templates", tenantId, actorAdminId, async () =>
            {
                if (connectionString is null)
                    throw new InvalidOperationException("Base de données tenant indisponible : modèles non appliqués.");

                await _templateApplier.ApplyAsync(tenantId, connectionString, afterSegment, afterDomain, dryRun: false, cancellationToken);
            });
        }

        // Step 4 — audit row in the TENANT DB (hash-chained). Details JSON carries segment/domain
        // before/after + affected user GUIDs only — no emails/names. The actor admin id is a GUID.
        await RunFaultIsolatedStepAsync(steps, "audit", "Audit", tenantId, actorAdminId, async () =>
        {
            if (connectionString is null)
                throw new InvalidOperationException("Base de données tenant indisponible : audit non écrit.");

            await WriteAuditAsync(
                connectionString,
                tenantId,
                beforeSegment,
                beforeDomain,
                afterSegment,
                afterDomain,
                touchedUserIds,
                actorAdminId,
                cancellationToken);
        });

        // Effective change reflects the now-persisted state.
        var reloadedTenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        var effectiveChange = await BuildPreviewAsync(reloadedTenant ?? tenant, targetSegment, targetDomain, profile, request, cancellationToken);

        var successCount = steps.Count(s => s.Success);
        var failureCount = steps.Count - successCount;
        _logger.LogInformation(
            "SectorReconfiguration.Applied TenantId={TenantId} ActorId={ActorId} SuccessSteps={Success} FailedSteps={Failed}",
            tenantId, actorAdminId, successCount, failureCount);

        return Result.Success(new SectorReconfigurationApplyResultDto
        {
            Steps = steps,
            EffectiveChange = effectiveChange
        });
    }

    // ---------- helpers ----------

    /// <summary>
    /// Resolves the calculator inputs shared by the preview diff and the apply grant rewrite:
    /// the effective core module set (profile core, or the catalog default) and the recommended
    /// module ids as an int seed.
    /// </summary>
    private static (IReadOnlyList<AppModule> CoreModules, IReadOnlyList<int> RecommendedSeed) ResolveModuleInputs(SectorProfile? profile)
    {
        var coreModules = profile?.CoreModules ?? SectorConfigurationCatalog.CoreModules;
        var recommendedSeed = (profile?.RecommendedModules ?? Array.Empty<AppModule>()).Select(m => (int)m).ToList();
        return (coreModules, recommendedSeed);
    }

    /// <summary>
    /// Runs a fault-isolated apply step: on success records a success step; on failure logs the
    /// error (structured event <c>SectorReconfiguration.{logEvent}Failed</c>) and records a failure
    /// step, then returns so the run continues (fail-continue, per <c>TenantMigrationHelper</c>).
    /// Used for the non-per-user steps whose log context is tenant + actor only.
    /// </summary>
    private async Task RunFaultIsolatedStepAsync(
        ICollection<StepResultDto> steps, string step, string logEvent,
        Guid tenantId, Guid? actorAdminId, Func<Task> work)
    {
        try
        {
            await work();
            steps.Add(new StepResultDto { Step = step, Success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SectorReconfiguration.{Event}Failed TenantId={TenantId} ActorId={ActorId}", logEvent, tenantId, actorAdminId);
            steps.Add(new StepResultDto { Step = step, Success = false, Error = ex.Message });
        }
    }

    /// <summary>
    /// Resolves the target segment/domain from the request relative to the tenant's current
    /// classification: a null segment keeps the current one; a null domain keeps the current one
    /// while an empty string clears it.
    /// </summary>
    private static (string? Segment, string? Domain) ComputeTarget(Tenant tenant, SectorReconfigurationRequestDto request)
    {
        var targetSegment = request.CompanySegment ?? tenant.CompanySegment;
        string? targetDomain;
        if (request.BusinessDomain is null)
            targetDomain = tenant.BusinessDomain; // keep
        else if (request.BusinessDomain.Length == 0)
            targetDomain = null; // clear
        else
            targetDomain = request.BusinessDomain;

        return (targetSegment, targetDomain);
    }

    /// <summary>
    /// Builds a side-effect-free preview (templates evaluated as a dry run; no SaveChanges). Reads
    /// current grant state to compute the per-user diff.
    /// </summary>
    private async Task<SectorReconfigurationPreviewDto> BuildPreviewAsync(
        Tenant tenant,
        string? targetSegment,
        string? targetDomain,
        SectorProfile? profile,
        SectorReconfigurationRequestDto request,
        CancellationToken cancellationToken)
    {
        var snapshot = _catalogProvider.GetSnapshot();
        var warnings = new List<string>();
        if (snapshot.Source == SectorRuleSource.Static)
            warnings.Add("Règles BDD inactives : profil résolu via le catalogue statique.");

        var afterSegment = profile?.SegmentCode;
        var afterDomain = profile?.DomainCode;
        var plan = await ResolveTenantPlanAsync(tenant.Id, cancellationToken);

        // Per-user module diff.
        var users = await GetActiveUsersAsync(tenant.Id, request, cancellationToken);
        var userDiffs = new List<UserModuleDiffDto>();
        if (request.RecomputeModuleGrants)
        {
            var (coreModules, recommendedSeed) = ResolveModuleInputs(profile);
            var dependencyEdges = snapshot.ModuleDependencies;

            foreach (var user in users)
            {
                var currentEnabled = await ComputeCurrentEnabledModuleIdsAsync(user.Id, cancellationToken);

                var targetSet = await SectorModuleSetCalculator.ComputeEnabledSetAsync(
                    coreModules,
                    recommendedSeed,
                    plan,
                    dependencyEdges,
                    _planResolver,
                    user.Id,
                    _logger,
                    cancellationToken);

                var targetEnabled = targetSet.Select(m => (int)m).OrderBy(m => m).ToList();
                var toEnable = targetEnabled.Except(currentEnabled).OrderBy(m => m).ToList();
                var toDisable = currentEnabled.Except(targetEnabled).OrderBy(m => m).ToList();

                userDiffs.Add(new UserModuleDiffDto
                {
                    UserId = user.Id,
                    CurrentEnabledModuleIds = currentEnabled,
                    TargetEnabledModuleIds = targetEnabled,
                    ModulesToEnable = toEnable,
                    ModulesToDisable = toDisable
                });
            }
        }

        // Templates preview (dry run, read-only).
        IReadOnlyList<TemplatePreviewDto> templates = Array.Empty<TemplatePreviewDto>();
        if (request.ApplyDataTemplates)
        {
            try
            {
                var connectionString = await _tenantService.GetConnectionStringAsync(tenant.Id, cancellationToken);
                if (connectionString is not null)
                {
                    var applyResult = await _templateApplier.ApplyAsync(
                        tenant.Id, connectionString, afterSegment, afterDomain, dryRun: true, cancellationToken);

                    var matchingTemplates = snapshot.DataTemplates
                        .Where(t => (t.SegmentCode is null || string.Equals(t.SegmentCode, afterSegment, StringComparison.Ordinal))
                                 && (t.DomainCode is null || string.Equals(t.DomainCode, afterDomain, StringComparison.Ordinal)))
                        .OrderBy(t => t.SortOrder)
                        .ToList();

                    var alreadyApplied = applyResult.Skipped.Select(s => s.Code).ToHashSet();
                    var outcomesByCode = applyResult.ItemOutcomes.GroupBy(o => o.TemplateCode)
                        .ToDictionary(g => g.Key, g => (IReadOnlyList<TemplateItemOutcome>)g.ToList());

                    templates = matchingTemplates
                        .Select(t => new TemplatePreviewDto
                        {
                            Code = t.Code,
                            Version = t.Version,
                            AlreadyApplied = alreadyApplied.Contains(t.Code),
                            ItemOutcomes = outcomesByCode.TryGetValue(t.Code, out var outcomes) ? outcomes : Array.Empty<TemplateItemOutcome>()
                        })
                        .ToList();

                    foreach (var w in applyResult.Warnings)
                        warnings.Add(w);
                }
                else
                {
                    warnings.Add("Base de données tenant indisponible : modèles non évalués.");
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"Évaluation des modèles impossible : {ex.Message}");
            }
        }

        // Informational default settings (matching segment/domain).
        var settings = snapshot.DefaultSettings
            .Where(s => (s.SegmentCode is null || string.Equals(s.SegmentCode, afterSegment, StringComparison.Ordinal))
                     && (s.DomainCode is null || string.Equals(s.DomainCode, afterDomain, StringComparison.Ordinal)))
            .OrderBy(s => s.SettingKey)
            .Select(s => new SettingPreviewDto { Key = s.SettingKey, Value = s.SettingValue })
            .ToList();

        return new SectorReconfigurationPreviewDto
        {
            CurrentSegment = tenant.CompanySegment,
            CurrentDomain = tenant.BusinessDomain,
            TargetSegment = afterSegment,
            TargetDomain = afterDomain,
            Users = userDiffs,
            Templates = templates,
            Settings = settings,
            Warnings = warnings
        };
    }

    private async Task<SubscriptionPlan> ResolveTenantPlanAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var plan = await _db.Subscriptions.AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .Select(s => s.Plan)
            .FirstOrDefaultAsync(cancellationToken);
        return plan;
    }

    private async Task<List<ApplicationUser>> GetActiveUsersAsync(Guid tenantId, SectorReconfigurationRequestDto request, CancellationToken cancellationToken)
    {
        IQueryable<ApplicationUser> query = _db.Users.AsNoTracking().Where(u => u.TenantId == tenantId && u.IsActive);

        if (request.UserIds is { Count: > 0 } userIds)
        {
            var ids = userIds.Distinct().Take(MaxUsersPerRun).ToHashSet();
            query = query.Where(u => ids.Contains(u.Id));
        }
        else
        {
            query = query.Take(MaxUsersPerRun);
        }

        return await query.ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Current enabled module ids for a user: no grant rows ⇒ all modules (legacy canonical form);
    /// otherwise the modules with <c>IsEnabled=true</c>. Mirrors
    /// <c>EffectivePermissionService.ResolveEnabledModules</c> semantics.
    /// </summary>
    private async Task<IReadOnlyList<int>> ComputeCurrentEnabledModuleIdsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var grants = await _db.UserModuleGrants.AsNoTracking()
            .Where(g => g.UserId == userId)
            .ToListAsync(cancellationToken);

        if (grants.Count == 0)
            return AppModuleExtensions.AllValues.Select(m => (int)m).OrderBy(m => m).ToList();

        return grants.Where(g => g.IsEnabled).Select(g => (int)g.Module).OrderBy(m => m).ToList();
    }

    /// <summary>
    /// Rewrites a user's <c>UserModuleGrant</c> rows to the canonical restriction-only form: delete
    /// existing rows, then insert one row per module with <c>IsEnabled</c> reflecting the target set
    /// — or nothing when the full set equals all modules (legacy "all enabled" representation).
    /// Administration is always on (guaranteed by the calculator). Never touches
    /// <c>TenantModuleOverrides</c>, plans or role assignments.
    /// </summary>
    private async Task RewriteUserGrantsAsync(Guid userId, HashSet<AppModule> finalSet, CancellationToken cancellationToken)
    {
        var existing = await _db.UserModuleGrants.Where(g => g.UserId == userId).ToListAsync(cancellationToken);
        if (existing.Count > 0)
            _db.UserModuleGrants.RemoveRange(existing);

        // Canonical "all modules enabled" = no rows. We still SaveChanges so any prior rows that were
        // just marked for deletion are actually persisted away — otherwise a user reconfigured to the
        // full set would keep their old (restricted) grants instead of reaching the 0-rows form.
        if (finalSet.Count != AppModuleExtensions.AllValues.Length)
        {
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
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Appends a hash-chained audit row to the tenant DB. Details JSON carries segment/domain
    /// before/after + affected user GUIDs + the actor admin id (all GUIDs/codes — no PII).
    /// </summary>
    private async Task WriteAuditAsync(
        string connectionString,
        Guid tenantId,
        string? beforeSegment,
        string? beforeDomain,
        string? afterSegment,
        string? afterDomain,
 IReadOnlyList<Guid> affectedUserIds,
        Guid? actorAdminId,
        CancellationToken cancellationToken)
    {
        await using var context = _tenantContextFactory.CreateIsolatedContext(connectionString);

        var previousHash = await context.AuditLogs
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Select(a => a.Hash)
            .FirstOrDefaultAsync(cancellationToken) ?? "GENESIS";

        var oldValues = JsonSerializer.Serialize(new { segment = beforeSegment, domain = beforeDomain });
        var newValues = JsonSerializer.Serialize(new
        {
            segment = afterSegment,
            domain = afterDomain,
            actorAdminId,
            affectedUserIds
        });

        var auditLog = AuditLog.Create(
            tenantId: tenantId,
            userId: actorAdminId,
            userEmail: "platform",
            action: AuditAction,
            entityType: "Tenant",
            entityId: tenantId,
            oldValues: oldValues,
            newValues: newValues,
            ipAddress: "platform",
            userAgent: null,
            previousHash: previousHash);

        context.AuditLogs.Add(auditLog);
        await context.SaveChangesAsync(cancellationToken);
    }
}
