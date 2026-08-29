using System.Text.Json;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Authorization;
using FactuTrust.Domain.ClientPortal;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.API.Controllers;

[ApiController]
[Route("api/tenant-users")]
[Authorize(Roles = nameof(UserRole.Administrator))]
public sealed class TenantUsersController : ControllerBase
{
    private readonly MasterDbContext _masterContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IEffectivePermissionService _permissionService;
    private readonly ISubscriptionResolver _subscriptionResolver;
    private readonly IAuditService _auditService;
    private readonly ISecurityStampTokenValidator _securityStampTokenValidator;
    private readonly ILogger<TenantUsersController> _logger;

    public TenantUsersController(
        MasterDbContext masterContext,
        UserManager<ApplicationUser> userManager,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IEffectivePermissionService permissionService,
        ISubscriptionResolver subscriptionResolver,
        IAuditService auditService,
        ISecurityStampTokenValidator securityStampTokenValidator,
        ILogger<TenantUsersController> logger)
    {
        _masterContext = masterContext;
        _userManager = userManager;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _permissionService = permissionService;
        _subscriptionResolver = subscriptionResolver;
        _auditService = auditService;
        _securityStampTokenValidator = securityStampTokenValidator;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TenantUserListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(ApiResponse<IReadOnlyList<TenantUserListItemDto>>.Fail("Contexte entreprise introuvable"));

        var users = await _masterContext.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId.Value && u.PortalClientId == null)
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync(cancellationToken);

        var userIds = users.Select(u => u.Id).ToList();
        var allGrants = await _masterContext.UserModuleGrants.AsNoTracking()
            .Where(g => userIds.Contains(g.UserId))
            .ToListAsync(cancellationToken);
        var grantsByUser = allGrants
            .GroupBy(g => g.UserId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Module).ToList());

        // Pre-fetch all user-role mappings in a single query to avoid N+1
        var userRoleEntities = await _masterContext.Set<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>>()
            .AsNoTracking()
            .Where(ur => userIds.Contains(ur.UserId))
            .ToListAsync(cancellationToken);
        var rolesById = await _masterContext.Roles.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Name ?? UserRole.Accountant.ToString(), cancellationToken);
        var userRoleMap = userRoleEntities
            .GroupBy(ur => ur.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(ur => rolesById.GetValueOrDefault(ur.RoleId, UserRole.Accountant.ToString())).FirstOrDefault() ?? UserRole.Accountant.ToString());

        var items = new List<TenantUserListItemDto>();
        foreach (var u in users)
        {
            var roleName = userRoleMap.GetValueOrDefault(u.Id, UserRole.Accountant.ToString());
            var roleEnum = Enum.TryParse<UserRole>(roleName, out var r) ? r : UserRole.Accountant;

            // Compute snapshot in-memory using pre-fetched grants (no extra DB call)
            var userGrants = grantsByUser.GetValueOrDefault(u.Id) ?? new List<UserModuleGrant>();
            IReadOnlyDictionary<AppModule, bool>? grantDict = null;
            if (userGrants.Count > 0)
                grantDict = userGrants.ToDictionary(g => g.Module, g => g.IsEnabled);
            var featureMap = BuildFeatureKeysByModuleStatic(userGrants);
            var effective = EffectivePermissionsCalculator.Compute(roleEnum, grantDict, featureMap);
            var enabledModules = ResolveEnabledModulesStatic(userGrants, effective);

            IReadOnlyList<TenantUserModuleFeaturesDto> moduleFeatures = Array.Empty<TenantUserModuleFeaturesDto>();
            if (userGrants.Count > 0)
            {
                moduleFeatures = userGrants.Select(g => new TenantUserModuleFeaturesDto
                {
                    Module = g.Module,
                    Enabled = g.IsEnabled,
                    FeatureKeys = ParseFeatureKeysForListDto(g.EnabledFeatureKeys)
                }).ToList();
            }

            items.Add(new TenantUserListItemDto
            {
                Id = u.Id,
                Email = u.Email ?? "",
                FirstName = u.FirstName,
                LastName = u.LastName,
                Role = roleEnum,
                RoleDisplay = roleEnum.ToDisplayString(),
                IsActive = u.IsActive,
                LastLoginAt = u.LastLoginAt,
                EnabledModuleIds = enabledModules.Select(m => (int)m).ToList(),
                ModuleFeatures = moduleFeatures
            });
        }

        return Ok(ApiResponse<IReadOnlyList<TenantUserListItemDto>>.Ok(items));
    }

    /// <summary>
    /// Pure Domain calculation (no DB access) — plan §6 Phase 2.1. Excluded roles (Client, FirmManager,
    /// FirmAccountant) always return 200 with a non-grantable, feature-less catalog, never 400: a single
    /// unified "empty ceiling" behavior consistent with <see cref="RoleModuleGrantCeilingExtensions.GetGrantCeiling"/>.
    /// </summary>
    [HttpGet("module-catalog")]
    [ProducesResponseType(typeof(ApiResponse<ModuleCatalogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult GetModuleCatalog([FromQuery] UserRole role)
    {
        if (!Enum.IsDefined(role))
            return BadRequest(ApiResponse<ModuleCatalogDto>.Fail("Rôle invalide"));

        return Ok(ApiResponse<ModuleCatalogDto>.Ok(BuildModuleCatalog(role)));
    }

    /// <summary>Builds the module/feature catalog for <paramref name="role"/> — see <see cref="GetModuleCatalog"/>.</summary>
    private static ModuleCatalogDto BuildModuleCatalog(UserRole role)
    {
        var basePermissions = new HashSet<string>(role.GetPermissions(), StringComparer.Ordinal);
        var modules = new List<ModuleCatalogModuleDto>();
        // Excluded roles (Client/FirmManager/FirmAccountant) always have an empty grant ceiling
        // (RoleModuleGrantCeilingExtensions.IsExcludedFromModuleGrants) — plan §5.3/§6-2.1 requires
        // the unified "empty/non-grantable" catalog for them: grantable=false on every module AND
        // no feature listed at all, never a 400. Without this short-circuit, a feature whose base
        // permissions happen to coincide with the excluded role's own base permissions could still
        // surface with DefaultSelected=true, which would violate the "aucune feature listée" contract.
        var isExcludedRole = RoleModuleGrantCeilingExtensions.IsExcludedFromModuleGrants(role);

        foreach (var module in AppModuleExtensions.AllValues)
        {
            var ceiling = RoleModuleGrantCeilingExtensions.GetGrantCeiling(role, module);
            var universe = module.GetModulePermissionUniverse();
            var baseInModule = new HashSet<string>(basePermissions, StringComparer.Ordinal);
            baseInModule.IntersectWith(universe);

            var features = new List<ModuleCatalogFeatureDto>();
            foreach (var featureKey in isExcludedRole ? Array.Empty<string>() : ModuleFeatureCatalog.GetValidFeatureKeys(module))
            {
                var featurePermissions = ModuleFeatureCatalog.GetPermissionsForFeature(module, featureKey);
                var featureBase = featurePermissions.Where(basePermissions.Contains).ToList();
                var featureAllowed = featurePermissions.Where(ceiling.Contains).ToList();
                var isExtension = featureAllowed.Except(featureBase, StringComparer.Ordinal).Any();

                features.Add(new ModuleCatalogFeatureDto
                {
                    Key = featureKey,
                    BasePermissions = featureBase,
                    AllowedPermissions = featureAllowed,
                    DefaultSelected = featureBase.Count > 0,
                    IsExtension = isExtension
                });
            }

            modules.Add(new ModuleCatalogModuleDto
            {
                Module = module,
                DisplayName = module.ToDisplayString(),
                Grantable = ceiling.Count > 0,
                DefaultEnabled = baseInModule.Count > 0,
                Features = features
            });
        }

        return new ModuleCatalogDto { Role = role, Modules = modules };
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<IActionResult> Create([FromBody] CreateTenantUserRequest request, CancellationToken cancellationToken)
        => CreateUsersCore(new[] { request }, cancellationToken);

    [HttpPost("batch")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<IActionResult> BatchCreate([FromBody] BatchCreateTenantUsersRequest request, CancellationToken cancellationToken)
    {
        if (request.Users.Count == 0)
            return Task.FromResult<IActionResult>(BadRequest(ApiResponse<object>.Fail("Aucun utilisateur à créer")));
        return CreateUsersCore(request.Users, cancellationToken);
    }

    private async Task<IActionResult> CreateUsersCore(
        IReadOnlyList<CreateTenantUserRequest> requests,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(ApiResponse<object>.Fail("Contexte entreprise introuvable"));

        var tenantKind = await _masterContext.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId.Value)
            .Select(t => t.Kind)
            .FirstOrDefaultAsync(cancellationToken);

        var plan = await _subscriptionResolver.GetPlanForTenantAsync(tenantId.Value, cancellationToken);
        var maxUsers = SubscriptionLimits.GetMaxUsers(plan);
        var currentCount = await _masterContext.Users.CountAsync(
            u => u.TenantId == tenantId.Value && u.IsActive && u.PortalClientId == null, cancellationToken);
        if (currentCount + requests.Count > maxUsers)
        {
            return BadRequest(ApiResponse<object>.Fail(
                $"Limite d'utilisateurs du plan atteinte (max {maxUsers}). Réduisez le nombre d'utilisateurs ou mettez à niveau l'abonnement."));
        }

        var createdUsers = new List<ApplicationUser>();
        try
        {
            foreach (var req in requests)
            {
                var err = ValidateCreateRequest(req);
                if (err is not null)
                {
                    await RollbackCreatedUsersAsync(createdUsers);
                    return BadRequest(ApiResponse<object>.Fail(err));
                }

                if (!TenantRoleCompatibility.IsRoleAllowedForTenantKind(req.Role, tenantKind))
                {
                    await RollbackCreatedUsersAsync(createdUsers);
                    return BadRequest(ApiResponse<object>.Fail(TenantRoleCompatibility.GetRejectionMessage(req.Role, tenantKind)));
                }

                var errModules = ValidateModuleAccessItems(req.Role, req.ModuleAccess);
                if (errModules is not null)
                {
                    await RollbackCreatedUsersAsync(createdUsers);
                    return BadRequest(ApiResponse<object>.Fail(errModules));
                }

                var user = new ApplicationUser
                {
                    UserName = req.Email.Trim(),
                    Email = req.Email.Trim(),
                    FirstName = req.FirstName.Trim(),
                    LastName = req.LastName.Trim(),
                    TenantId = tenantId.Value,
                    EmailConfirmed = true,
                    PhoneNumber = string.IsNullOrWhiteSpace(req.PhoneNumber) ? null : req.PhoneNumber.Trim()
                };
                user.ApplyNewInteractiveProductOnboarding();

                var create = await _userManager.CreateAsync(user, req.Password);
                if (!create.Succeeded)
                {
                    await RollbackCreatedUsersAsync(createdUsers);
                    var msg = IdentityErrorTranslator.TranslateToFrench(create.Errors);
                    return BadRequest(ApiResponse<object>.Fail(msg));
                }

                await _userManager.AddToRoleAsync(user, req.Role.ToString());
                createdUsers.Add(user);
                await ApplyModuleAccessAsync(user.Id, req.ModuleAccess, cancellationToken);
            }

            await _masterContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Created {Count} user(s) for tenant {TenantId}", requests.Count, tenantId);

            // Audit — best effort, only after the whole batch has committed successfully (plan §6 Phase 2.4:
            // never emitted on rollback paths above, since RollbackCreatedUsersAsync already ran for those).
            for (var i = 0; i < requests.Count; i++)
            {
                var req = requests[i];
                await LogAuditBestEffortAsync(
                    AuditActions.User.Created,
                    createdUsers[i].Id,
                    oldValues: null,
                    newValues: new { req.Email, Role = req.Role.ToString(), ModuleAccess = BuildAuditModuleAccessSnapshot(req.ModuleAccess) },
                    cancellationToken);
            }

            return StatusCode(StatusCodes.Status201Created, ApiResponse<object>.Ok(new { count = requests.Count }, "Utilisateur(s) créé(s)"));
        }
        catch (Exception ex)
        {
            await RollbackCreatedUsersAsync(createdUsers);
            _logger.LogError(ex, "Batch user create failed for tenant {TenantId}", tenantId);
            return StatusCode(500, ApiResponse<object>.Fail("Erreur lors de la création des utilisateurs"));
        }
    }

    private async Task RollbackCreatedUsersAsync(List<ApplicationUser> createdUsers)
    {
        // Clear change tracker first to discard any unsaved grants added to the context
        _masterContext.ChangeTracker.Clear();

        foreach (var u in createdUsers.AsEnumerable().Reverse())
        {
            try
            {
                // Re-attach user for deletion since we cleared the tracker
                var freshUser = await _userManager.FindByIdAsync(u.Id.ToString());
                if (freshUser is not null)
                {
                    // Remove any grants that were already saved for this user
                    var savedGrants = await _masterContext.UserModuleGrants
                        .Where(g => g.UserId == freshUser.Id)
                        .ToListAsync();
                    if (savedGrants.Count > 0)
                        _masterContext.UserModuleGrants.RemoveRange(savedGrants);
                    await _masterContext.SaveChangesAsync();

                    await _userManager.DeleteAsync(freshUser);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to rollback user {UserId} after batch error", u.Id);
            }
        }

        createdUsers.Clear();
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTenantUserRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(ApiResponse<object>.Fail("Contexte entreprise introuvable"));

        var user = await _masterContext.Users.FirstOrDefaultAsync(
            u => u.Id == id && u.TenantId == tenantId.Value, cancellationToken);
        if (user is null)
            return NotFound(ApiResponse<object>.Fail("Utilisateur introuvable"));

        if (request.IsActive == false && user.Id == _currentUser.UserId)
            return BadRequest(ApiResponse<object>.Fail("Vous ne pouvez pas désactiver votre propre compte"));

        if (request.ModuleAccess is { Count: > 0 } moduleRows && user.Id == _currentUser.UserId)
        {
            if (moduleRows.Any(x => x.Module == AppModule.Administration && !x.Enabled))
                return BadRequest(ApiResponse<object>.Fail(
                    "Vous ne pouvez pas désactiver le module Paramètres et utilisateurs pour votre propre compte."));
        }

        // Resolved BEFORE any mutation — plan §6 Phase 2.2: grant validation and tenant/role compatibility
        // are checked against the FINAL role (request.Role if provided, otherwise the current role).
        var previousRole = await GetUserRoleAsync(user);
        var previousIsActive = user.IsActive;
        var effectiveRole = request.Role ?? previousRole;

        if (request.Role.HasValue)
        {
            if (!ClientPortalStaffRules.IsAssignableByStaff(request.Role.Value))
                return BadRequest(ApiResponse<object>.Fail(ClientPortalStaffRules.InviteFromClientCardMessage));

            if (user.Id == _currentUser.UserId && request.Role.Value != previousRole)
                return BadRequest(ApiResponse<object>.Fail("Vous ne pouvez pas modifier votre propre rôle"));

            var tenantKind = await _masterContext.Tenants.AsNoTracking()
                .Where(t => t.Id == tenantId.Value)
                .Select(t => t.Kind)
                .FirstOrDefaultAsync(cancellationToken);
            if (!TenantRoleCompatibility.IsRoleAllowedForTenantKind(request.Role.Value, tenantKind))
                return BadRequest(ApiResponse<object>.Fail(TenantRoleCompatibility.GetRejectionMessage(request.Role.Value, tenantKind)));
        }

        if (request.ModuleAccess is not null)
        {
            var errModules = ValidateModuleAccessItems(effectiveRole, request.ModuleAccess);
            if (errModules is not null)
                return BadRequest(ApiResponse<object>.Fail(errModules));
        }

        // Last-administrator protection (plan §6 Phase 2.3) — only when this PATCH would demote or
        // deactivate an administrator who is currently active; serialized in the transaction below via
        // an UPDLOCK/HOLDLOCK on the tenant row + a fresh admin count inside the SAME transaction.
        var mustProtectLastAdmin =
            previousRole == UserRole.Administrator && previousIsActive &&
            ((request.Role.HasValue && request.Role.Value != UserRole.Administrator) || request.IsActive == false);

        // Role, activation state or module grants changing must revoke the OLD access token immediately
        // (plan §6 Phase 2.5) — rotate the security stamp and purge the refresh token, atomically with
        // the mutation itself.
        var mustRevokeCurrentToken = request.Role.HasValue || request.IsActive.HasValue || request.ModuleAccess is not null;

        IReadOnlyList<UserModuleGrant> oldGrantsSnapshot = Array.Empty<UserModuleGrant>();
        if (request.ModuleAccess is not null)
        {
            oldGrantsSnapshot = await _masterContext.UserModuleGrants.AsNoTracking()
                .Where(g => g.UserId == user.Id)
                .ToListAsync(cancellationToken);
        }

        string? failureMessage = null;
        var strategy = _masterContext.Database.CreateExecutionStrategy();
        var succeeded = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _masterContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                if (mustProtectLastAdmin)
                {
                    // Tenant-row mutex: serializes concurrent PATCHes for the same tenant so two
                    // simultaneous demotions of the last two admins can never both succeed.
                    await _masterContext.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT TOP(1) Id FROM Tenants WITH (UPDLOCK, HOLDLOCK) WHERE Id = {tenantId.Value}",
                        cancellationToken);

                    var activeAdminCount = await (
                        from u in _masterContext.Users
                        join ur in _masterContext.Set<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>>() on u.Id equals ur.UserId
                        join r in _masterContext.Roles on ur.RoleId equals r.Id
                        where u.TenantId == tenantId.Value && u.IsActive && r.Name == nameof(UserRole.Administrator)
                        select u.Id).CountAsync(cancellationToken);

                    if (activeAdminCount <= 1)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        failureMessage = "Impossible de rétrograder ou désactiver le dernier administrateur actif de l'entreprise.";
                        return false;
                    }
                }

                if (!string.IsNullOrWhiteSpace(request.FirstName))
                    user.FirstName = request.FirstName.Trim();
                if (!string.IsNullOrWhiteSpace(request.LastName))
                    user.LastName = request.LastName.Trim();
                if (request.PhoneNumber is not null)
                    user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
                if (request.IsActive.HasValue)
                    user.IsActive = request.IsActive.Value;

                if (request.Role.HasValue)
                {
                    var currentRoles = await _userManager.GetRolesAsync(user);
                    await _userManager.RemoveFromRolesAsync(user, currentRoles);
                    await _userManager.AddToRoleAsync(user, request.Role.Value.ToString());
                }

                if (!string.IsNullOrWhiteSpace(request.NewPassword))
                {
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    var pwd = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);
                    if (!pwd.Succeeded)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        failureMessage = IdentityErrorTranslator.TranslateToFrench(pwd.Errors);
                        return false;
                    }
                }

                if (mustRevokeCurrentToken)
                {
                    // Atomic with the mutation above (same transaction): either both commit, or neither
                    // does — no state where grants/role changed but the old token is still trusted.
                    user.RefreshToken = null;
                    user.RefreshTokenExpiryTime = null;
                    await _userManager.UpdateSecurityStampAsync(user);
                }

                await _userManager.UpdateAsync(user);

                if (request.ModuleAccess is not null)
                {
                    if (request.ModuleAccess.Count == 0)
                        await ClearModuleGrantsAsync(user.Id, cancellationToken);
                    else
                        await ReplaceModuleGrantsAsync(user.Id, request.ModuleAccess, cancellationToken);
                }

                await _masterContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return true;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });

        if (!succeeded)
            return BadRequest(ApiResponse<object>.Fail(failureMessage ?? "Erreur lors de la mise à jour de l'utilisateur"));

        // Post-commit cache eviction (plan §6 Phase 2.5): the mutation transaction just rotated the
        // security stamp — evict this user's cached stamp snapshot on THIS node so the very next
        // request rejects the old token immediately, instead of waiting out the up-to-5s TTL. Other
        // nodes still rely on that TTL bound alone.
        if (mustRevokeCurrentToken)
            _securityStampTokenValidator.Invalidate(user.Id);

        // Audit — best effort, emitted only after the master transaction has committed (plan §6 Phase 2.4).
        if (request.Role.HasValue && request.Role.Value != previousRole)
        {
            await LogAuditBestEffortAsync(
                AuditActions.User.RoleChanged, user.Id,
                oldValues: new { Role = previousRole.ToString() },
                newValues: new { Role = request.Role.Value.ToString() },
                cancellationToken);
        }

        if (request.IsActive.HasValue && request.IsActive.Value != previousIsActive)
        {
            await LogAuditBestEffortAsync(
                request.IsActive.Value ? AuditActions.User.Reactivated : AuditActions.User.Deactivated,
                user.Id, oldValues: new { IsActive = previousIsActive }, newValues: new { IsActive = request.IsActive.Value },
                cancellationToken);
        }

        if (request.ModuleAccess is not null)
        {
            await LogAuditBestEffortAsync(
                AuditActions.User.ModuleGrantsChanged, user.Id,
                oldValues: BuildAuditGrantsSnapshot(oldGrantsSnapshot),
                newValues: BuildAuditModuleAccessSnapshot(request.ModuleAccess),
                cancellationToken);
        }

        var message = mustRevokeCurrentToken
            ? "Utilisateur mis à jour ; ses accès effectifs ont changé, ses sessions en cours sont révoquées."
            : "Utilisateur mis à jour";
        return Ok(ApiResponse<object>.Ok(null!, message));
    }

    private async Task<UserRole> GetUserRoleAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var roleName = roles.FirstOrDefault() ?? UserRole.Accountant.ToString();
        return Enum.TryParse<UserRole>(roleName, out var r) ? r : UserRole.Accountant;
    }

    private static string? ValidateCreateRequest(CreateTenantUserRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email)) return "L'email est requis";
        if (string.IsNullOrWhiteSpace(req.FirstName)) return "Le prénom est requis";
        if (string.IsNullOrWhiteSpace(req.LastName)) return "Le nom est requis";
        if (string.IsNullOrWhiteSpace(req.Password)) return "Le mot de passe est requis";
        if (!Enum.IsDefined(req.Role)) return "Rôle invalide";
        if (!ClientPortalStaffRules.IsAssignableByStaff(req.Role))
            return ClientPortalStaffRules.InviteFromClientCardMessage;
        return null;
    }

    /// <summary>
    /// Validates module-access items against the FINAL resolved role (plan §6 Phase 2.2): a module can only
    /// be enabled if <see cref="RoleModuleGrantCeilingExtensions.GetGrantCeiling"/> is non-empty for
    /// (role, module) — this also covers the excluded roles (Client/FirmManager/FirmAccountant), whose
    /// ceiling is always empty, so any attempt to enable a module for them is rejected. A feature key is
    /// only accepted when at least one of its permissions is within that ceiling. Explicit rejection,
    /// never a silent trim.
    /// </summary>
    private static string? ValidateModuleAccessItems(UserRole role, IReadOnlyList<UserModuleAccessItemDto>? list)
    {
        if (list is null || list.Count == 0)
            return null;

        // P3: Reject duplicate modules in the payload
        var seenModules = new HashSet<AppModule>();
        foreach (var x in list)
        {
            if (!seenModules.Add(x.Module))
                return $"Le module {x.Module} apparaît plusieurs fois dans la requête.";

            if (!x.Enabled)
            {
                if (x.EnabledFeatureKeys is { Count: > 0 })
                    return "Les sous-modules ne s'appliquent pas lorsque le module parent est désactivé.";
                continue;
            }

            var ceiling = RoleModuleGrantCeilingExtensions.GetGrantCeiling(role, x.Module);
            if (ceiling.Count == 0)
                return $"Le module « {x.Module.ToDisplayString()} » n'est pas disponible pour le rôle {role.ToDisplayString()}.";

            if (x.EnabledFeatureKeys is null || x.EnabledFeatureKeys.Count == 0)
                continue;

            foreach (var key in x.EnabledFeatureKeys)
            {
                if (string.IsNullOrWhiteSpace(key))
                    return "Clé de sous-module invalide.";
                var trimmed = key.Trim();
                if (!ModuleFeatureCatalog.IsValidFeatureKey(x.Module, trimmed))
                    return $"Clé de sous-module invalide pour le module {x.Module}: {trimmed}";

                var featurePermissions = ModuleFeatureCatalog.GetPermissionsForFeature(x.Module, trimmed);
                if (!featurePermissions.Any(ceiling.Contains))
                    return $"Le sous-module « {trimmed} » n'est pas disponible pour le rôle {role.ToDisplayString()}.";
            }
        }

        return null;
    }

    private async Task ApplyModuleAccessAsync(Guid userId, IReadOnlyList<UserModuleAccessItemDto>? moduleAccess, CancellationToken cancellationToken)
    {
        if (moduleAccess is null || moduleAccess.Count == 0)
            return;
        await ReplaceModuleGrantsAsync(userId, moduleAccess, cancellationToken);
    }

    /// <summary>
    /// Best-effort audit write (plan §6 Phase 2.4): <see cref="IAuditService"/> writes to the TENANT
    /// database (isolated context) and cannot join the master transaction that already committed the
    /// mutation — a failure here must never fail the request. Traced with an AUDIT_WRITE_FAILED marker
    /// so it can be alerted on.
    /// </summary>
    private async Task LogAuditBestEffortAsync(
        string action, Guid entityId, object? oldValues, object? newValues, CancellationToken cancellationToken)
    {
        try
        {
            await _auditService.LogAsync(action, "User", entityId, oldValues, newValues, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AUDIT_WRITE_FAILED action={Action} entityId={EntityId}", action, entityId);
        }
    }

    private static IReadOnlyList<object> BuildAuditModuleAccessSnapshot(IReadOnlyList<UserModuleAccessItemDto>? items) =>
        items is null
            ? Array.Empty<object>()
            : items.Select(x => (object)new { module = x.Module.ToString(), enabled = x.Enabled, featureKeys = x.EnabledFeatureKeys }).ToList();

    private static IReadOnlyList<object> BuildAuditGrantsSnapshot(IReadOnlyList<UserModuleGrant> grants) =>
        grants.Select(g => (object)new
        {
            module = g.Module.ToString(),
            enabled = g.IsEnabled,
            featureKeys = ParseFeatureKeysForListDto(g.EnabledFeatureKeys)
        }).ToList();

    private async Task ReplaceModuleGrantsAsync(Guid userId, IReadOnlyList<UserModuleAccessItemDto> items, CancellationToken cancellationToken)
    {
        var existing = await _masterContext.UserModuleGrants.Where(g => g.UserId == userId).ToListAsync(cancellationToken);
        _masterContext.UserModuleGrants.RemoveRange(existing);

        foreach (var x in items)
        {
            string? json = null;
            if (x.Enabled)
            {
                if (x.EnabledFeatureKeys is null)
                    json = null;
                else if (x.EnabledFeatureKeys.Count == 0)
                    json = "[]";
                else
                {
                    var keys = x.EnabledFeatureKeys
                        .Select(k => k.Trim())
                        .Where(k => k.Length > 0)
                        .Distinct(StringComparer.Ordinal)
                        .ToList();
                    json = keys.Count > 0 ? JsonSerializer.Serialize(keys) : "[]";
                }
            }

            _masterContext.UserModuleGrants.Add(new UserModuleGrant
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Module = x.Module,
                IsEnabled = x.Enabled,
                EnabledFeatureKeys = json
            });
        }

        // P4: Audit log for grant changes
        _logger.LogInformation(
            "Replaced module grants for user {UserId}: {ModuleCount} module(s) configured [{Modules}]",
            userId,
            items.Count,
            string.Join(", ", items.Select(i => $"{i.Module}={i.Enabled}")));
    }

    private async Task ClearModuleGrantsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var existing = await _masterContext.UserModuleGrants.Where(g => g.UserId == userId).ToListAsync(cancellationToken);
        _masterContext.UserModuleGrants.RemoveRange(existing);

        // P4: Audit log
        if (existing.Count > 0)
            _logger.LogInformation("Cleared {Count} module grants for user {UserId}", existing.Count, userId);
    }

    /// <summary>
    /// In-memory equivalent of EffectivePermissionService.BuildFeatureKeysByModule (for List optimization).
    /// </summary>
    private static IReadOnlyDictionary<AppModule, IReadOnlyList<string>>? BuildFeatureKeysByModuleStatic(
        IReadOnlyList<UserModuleGrant> grants)
    {
        Dictionary<AppModule, IReadOnlyList<string>>? map = null;
        foreach (var g in grants)
        {
            if (!g.IsEnabled || string.IsNullOrWhiteSpace(g.EnabledFeatureKeys))
                continue;

            List<string>? parsed = null;
            try
            {
                parsed = JsonSerializer.Deserialize<List<string>>(g.EnabledFeatureKeys);
            }
            catch (JsonException)
            {
                continue;
            }

            if (parsed is null)
                continue;

            map ??= new Dictionary<AppModule, IReadOnlyList<string>>();
            map[g.Module] = parsed;
        }

        return map;
    }

    /// <summary>
    /// <c>null</c> column = all sub-features; otherwise parsed keys (empty JSON = explicit none).
    /// </summary>
    private static IReadOnlyList<string>? ParseFeatureKeysForListDto(string? json)
    {
        if (json is null)
            return null;

        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<string>();

        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json);
            if (list is null)
                return Array.Empty<string>();

            return list
                .Select(k => k.Trim())
                .Where(k => k.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// In-memory equivalent of EffectivePermissionService.ResolveEnabledModules (for List optimization).
    /// </summary>
    private static IReadOnlyList<AppModule> ResolveEnabledModulesStatic(
        IReadOnlyList<UserModuleGrant> grants,
        HashSet<string> effectivePermissions)
    {
        if (grants.Count == 0)
            return AppModuleExtensions.AllValues.ToList();

        var dict = grants.ToDictionary(g => g.Module, g => g.IsEnabled);
        var toggledOn = new List<AppModule>();
        foreach (var m in AppModuleExtensions.AllValues)
        {
            var on = !dict.TryGetValue(m, out var flag) || flag;
            if (on)
                toggledOn.Add(m);
        }

        return AppModuleExtensions.FilterToModulesWithEffectivePermissions(toggledOn, effectivePermissions);
    }
}
