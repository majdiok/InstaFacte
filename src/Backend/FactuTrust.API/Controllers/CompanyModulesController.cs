using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.SectorConfiguration;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services.SectorRules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Plan §2.2 — self-service module configuration for the current tenant, exposed after
/// registration (unlike the registration wizard's one-shot selection). Reuses the same
/// dependency/plan-ceiling computation (<see cref="SectorModuleSetCalculator"/>) and grant-writing
/// logic (<see cref="UserModuleGrantWriter"/>) as the wizard and the platform-admin sector
/// reconfiguration flow.
///
/// Decision D4 (plan): a change applies to every active user of the tenant — there is no separate
/// tenant-wide grant table, <c>UserModuleGrant</c> stays the per-user source of truth and is kept in
/// sync across all active users by this endpoint.
/// </summary>
[ApiController]
[Route("api/company/modules")]
[Authorize]
public sealed class CompanyModulesController : ControllerBase
{
    private readonly MasterDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IPlanResolver _planResolver;
    private readonly ISectorCatalogProvider _catalogProvider;
    private readonly IRegistrationSectorService _registrationSectorService;
    private readonly ILogger<CompanyModulesController> _logger;

    public CompanyModulesController(
        MasterDbContext db,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IPlanResolver planResolver,
        ISectorCatalogProvider catalogProvider,
        IRegistrationSectorService registrationSectorService,
        ILogger<CompanyModulesController> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _planResolver = planResolver;
        _catalogProvider = catalogProvider;
        _registrationSectorService = registrationSectorService;
        _logger = logger;
    }

    /// <summary>
    /// Current module configuration for the tenant. <c>isEnabled</c> reflects the acting user's own
    /// grants — legitimate here because this endpoint is admin-only (<see cref="PermissionPolicies.SettingsRead"/>)
    /// and <c>PUT</c> keeps every active user's grants in sync (D4), so the acting admin's grants are
    /// representative of the whole tenant.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.SettingsRead)]
    [ProducesResponseType(typeof(ApiResponse<CompanyModulesDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetModules(CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(ApiResponse<CompanyModulesDto>.Fail("Contexte tenant introuvable."));

        var actorId = _currentUser.UserId;
        if (!actorId.HasValue)
            return Unauthorized(ApiResponse<CompanyModulesDto>.Fail("Non authentifié."));

        var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId.Value, cancellationToken);
        if (tenant is null)
            return NotFound(ApiResponse<CompanyModulesDto>.Fail("Société introuvable."));

        var plan = await ResolveTenantPlanAsync(tenantId.Value, cancellationToken);
        var snapshot = _catalogProvider.GetSnapshot();
        var coreSet = new HashSet<AppModule>(SectorConfigurationCatalog.CoreModules);
        var (requiresMap, requiredByMap) = BuildDependencyMaps(snapshot.ModuleDependencies);
        var recommendedSet = ResolveRecommendedModules(tenant);
        var currentEnabled = await ComputeEnabledModuleSetAsync(actorId.Value, cancellationToken);

        var modules = new List<CompanyModuleItemDto>();
        foreach (var module in AppModuleExtensions.AllValues.Where(m => m != AppModule.Honoraires))
        {
            var allowedByPlan = module == AppModule.Administration
                || await _planResolver.IsModuleAllowedAsync(plan, (int)module, cancellationToken);

            modules.Add(new CompanyModuleItemDto
            {
                Id = (int)module,
                Code = module.ToString(),
                LabelFr = module.ToDisplayString(),
                IsCore = coreSet.Contains(module),
                IsEnabled = currentEnabled.Contains(module),
                AllowedByPlan = allowedByPlan,
                Requires = requiresMap.TryGetValue((int)module, out var requires) ? requires : Array.Empty<int>(),
                RequiredBy = requiredByMap.TryGetValue((int)module, out var requiredBy) ? requiredBy : Array.Empty<int>(),
                RecommendedForSector = recommendedSet.Contains(module),
                IsPaidPlanOnly = module.IsPaidPlanOnly()
            });
        }

        return Ok(ApiResponse<CompanyModulesDto>.Ok(new CompanyModulesDto
        {
            PlanCode = plan.ToString(),
            Modules = modules
        }));
    }

    /// <summary>
    /// Rewrites the tenant's module configuration. Rejects (400) disabling a core module or a
    /// module required by another still-enabled module ("Requis par X"). Modules denied by the
    /// subscription plan are NOT a hard failure — they are dropped from the effective set and
    /// reported back as non-blocking <c>warnings</c> (same no-escalation ceiling as the
    /// registration wizard). Applies to every active user of the tenant (plan §2.2 D4) and writes a
    /// <see cref="ModuleGrantAuditEntry"/>.
    /// </summary>
    [HttpPut]
    [Authorize(Policy = PermissionPolicies.SettingsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<UpdateCompanyModulesResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateModules([FromBody] UpdateCompanyModulesRequestDto dto, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(ApiResponse<UpdateCompanyModulesResultDto>.Fail("Contexte tenant introuvable."));

        var actorId = _currentUser.UserId;
        if (!actorId.HasValue)
            return Unauthorized(ApiResponse<UpdateCompanyModulesResultDto>.Fail("Non authentifié."));

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId.Value, cancellationToken);
        if (tenant is null)
            return NotFound(ApiResponse<UpdateCompanyModulesResultDto>.Fail("Société introuvable."));

        var requestedSet = new HashSet<AppModule>(
            (dto.EnabledModuleIds ?? Array.Empty<int>())
                .Where(id => Enum.IsDefined(typeof(AppModule), id))
                .Select(id => (AppModule)id));

        var snapshot = _catalogProvider.GetSnapshot();

        // 400 — core modules can never be disabled.
        var coreErrorMessage = CompanyModuleReconfigurationValidator.ValidateCoreModules(requestedSet, SectorConfigurationCatalog.CoreModules);
        if (coreErrorMessage is not null)
            return BadRequest(ApiResponse<UpdateCompanyModulesResultDto>.Fail(coreErrorMessage));

        // 400 — a module required by another still-requested module cannot be disabled ("Requis par X").
        var dependencyErrorMessage = CompanyModuleReconfigurationValidator.ValidateDependencies(requestedSet, snapshot.ModuleDependencies);
        if (dependencyErrorMessage is not null)
            return BadRequest(ApiResponse<UpdateCompanyModulesResultDto>.Fail(dependencyErrorMessage));

        var plan = await ResolveTenantPlanAsync(tenantId.Value, cancellationToken);
        var computation = await SectorModuleSetCalculator.ComputeAsync(
            SectorConfigurationCatalog.CoreModules,
            requestedSet.Select(m => (int)m),
            plan,
            snapshot.ModuleDependencies,
            _planResolver,
            actorId.Value,
            _logger,
            cancellationToken);

        var finalSet = computation.EnabledModules;
        var warnings = new List<string>();
        foreach (var denied in computation.DeniedByPlan)
        {
            warnings.Add($"Module « {denied.ToDisplayString()} » non disponible avec votre abonnement actuel — non activé.");
        }
        if (computation.DroppedInvalidIds.Count > 0)
        {
            warnings.Add($"{computation.DroppedInvalidIds.Count} identifiant(s) de module invalide(s) ignoré(s).");
        }

        // D4 — the change applies to EVERY active user of the tenant (no cap): a tenant's active-user
        // count is bounded by its subscription plan quota, so a single self-service admin action
        // safely keeps all active users in sync and never silently leaves a tail with stale grants.
        var users = await _db.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId.Value && u.IsActive)
            .OrderBy(u => u.Id)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var userId in users)
            {
                await UserModuleGrantWriter.RewriteGrantsAsync(_db, userId, finalSet, saveChanges: false, cancellationToken);
            }

            var diffJson = JsonSerializer.Serialize(new
            {
                requestedIds = requestedSet.Select(m => (int)m).OrderBy(x => x).ToList(),
                enabledIds = finalSet.Select(m => (int)m).OrderBy(x => x).ToList(),
                deniedByPlan = computation.DeniedByPlan.Select(m => (int)m).OrderBy(x => x).ToList(),
                droppedInvalidIds = computation.DroppedInvalidIds,
                affectedUserIds = users
            });
            _db.ModuleGrantAuditEntries.Add(
                ModuleGrantAuditEntry.Create(tenantId.Value, actorId, "company-modules-update", diffJson));

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "CompanyModulesController.UpdateModules failed for tenant {TenantId}", tenantId.Value);
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<UpdateCompanyModulesResultDto>.Fail("Une erreur est survenue lors de la mise à jour des modules."));
        }

        _logger.LogInformation(
            "CompanyModulesController.UpdateModules: tenant {TenantId} actor {ActorId} updated {UserCount} user(s), enabled={Enabled}",
            tenantId.Value, actorId.Value, users.Count, string.Join(",", finalSet.Select(m => (int)m).OrderBy(x => x)));

        return Ok(ApiResponse<UpdateCompanyModulesResultDto>.Ok(new UpdateCompanyModulesResultDto
        {
            EnabledModuleIds = finalSet.Select(m => (int)m).OrderBy(x => x).ToList(),
            Warnings = warnings
        }, "Configuration des modules mise à jour."));
    }

    private async Task<SubscriptionPlan> ResolveTenantPlanAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        // Deterministic plan resolution: only Active/Trial subscriptions carry an effective plan
        // (Expired/Cancelled/Suspended/PastDue rows are historical), and when more than one active
        // row exists we take the most recently started one — same active-subscription filter used by
        // the platform tenant metrics resolver (PlatformTenantQueryService). Without the status
        // filter + ordering, FirstOrDefault over a tenant with several subscription rows is
        // non-deterministic and could resolve a stale/expired plan.
        return await _db.Subscriptions.AsNoTracking()
            .Where(s => s.TenantId == tenantId
                && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trial))
            .OrderByDescending(s => s.StartDate)
            .Select(s => s.Plan)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Current enabled module set for a user: no grant rows ⇒ all modules (legacy canonical form,
    /// mirrors <c>EffectivePermissionService.ResolveEnabledModules</c>); otherwise the modules with
    /// <c>IsEnabled=true</c>.
    /// </summary>
    /// <summary>
    /// Délègue à <see cref="TenantModuleAvailability.ReadGrantedModulesAsync"/> : une seule
    /// définition de « les modules de cette société », partagée avec <c>TenantUsersController</c>.
    /// </summary>
    private Task<HashSet<AppModule>> ComputeEnabledModuleSetAsync(Guid userId, CancellationToken cancellationToken)
        => TenantModuleAvailability.ReadGrantedModulesAsync(_db, userId, cancellationToken);

    private static (Dictionary<int, IReadOnlyList<int>> Requires, Dictionary<int, IReadOnlyList<int>> RequiredBy) BuildDependencyMaps(
        IReadOnlyList<ModuleDependencySnapshot> edges)
    {
        var requires = edges.GroupBy(e => e.ModuleId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<int>)g.Select(e => e.RequiredModuleId).ToList());
        var requiredBy = edges.GroupBy(e => e.RequiredModuleId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<int>)g.Select(e => e.ModuleId).ToList());
        return (requires, requiredBy);
    }

    private HashSet<AppModule> ResolveRecommendedModules(Tenant tenant)
    {
        var profileResult = _registrationSectorService.ResolveProfile(tenant.CompanySegment, tenant.BusinessDomain);
        if (profileResult.IsFailure || profileResult.Value is null)
            return new HashSet<AppModule>();

        return new HashSet<AppModule>(profileResult.Value.RecommendedModules);
    }
}
