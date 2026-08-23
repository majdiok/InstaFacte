using System.Text.Json;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Authorization;
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
    private readonly ILogger<TenantUsersController> _logger;

    public TenantUsersController(
        MasterDbContext masterContext,
        UserManager<ApplicationUser> userManager,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IEffectivePermissionService permissionService,
        ISubscriptionResolver subscriptionResolver,
        ILogger<TenantUsersController> logger)
    {
        _masterContext = masterContext;
        _userManager = userManager;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _permissionService = permissionService;
        _subscriptionResolver = subscriptionResolver;
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
            .Where(u => u.TenantId == tenantId.Value)
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

        var plan = await _subscriptionResolver.GetPlanForTenantAsync(tenantId.Value, cancellationToken);
        var maxUsers = SubscriptionLimits.GetMaxUsers(plan);
        var currentCount = await _masterContext.Users.CountAsync(
            u => u.TenantId == tenantId.Value && u.IsActive, cancellationToken);
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

                var errModules = ValidateModuleAccessItems(req.ModuleAccess);
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

        if (request.ModuleAccess is not null)
        {
            var errModules = ValidateModuleAccessItems(request.ModuleAccess);
            if (errModules is not null)
                return BadRequest(ApiResponse<object>.Fail(errModules));
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
            var previousRole = await GetUserRoleAsync(user);
            if (user.Id == _currentUser.UserId && request.Role.Value != previousRole)
                return BadRequest(ApiResponse<object>.Fail("Vous ne pouvez pas modifier votre propre rôle"));

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
                return BadRequest(ApiResponse<object>.Fail(IdentityErrorTranslator.TranslateToFrench(pwd.Errors)));
            }
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

        return Ok(ApiResponse<object>.Ok(null!, "Utilisateur mis à jour"));
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
        return null;
    }

    private static string? ValidateModuleAccessItems(IReadOnlyList<UserModuleAccessItemDto>? list)
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

            if (x.EnabledFeatureKeys is null || x.EnabledFeatureKeys.Count == 0)
                continue;

            foreach (var key in x.EnabledFeatureKeys)
            {
                if (string.IsNullOrWhiteSpace(key))
                    return "Clé de sous-module invalide.";
                var trimmed = key.Trim();
                if (!ModuleFeatureCatalog.IsValidFeatureKey(x.Module, trimmed))
                    return $"Clé de sous-module invalide pour le module {x.Module}: {trimmed}";
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
