using System.Text.Json;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Auth;
using FactuTrust.Domain.Authorization;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services;

public sealed class EffectivePermissionService : IEffectivePermissionService
{
    private readonly MasterDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public EffectivePermissionService(MasterDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<UserAccessSnapshot> GetUserAccessSnapshotAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return new UserAccessSnapshot(new HashSet<string>(), Array.Empty<AppModule>(), false);

        var role = await ResolveRoleAsync(user);
        var grants = await _db.UserModuleGrants.AsNoTracking()
            .Where(g => g.UserId == userId)
            .ToListAsync(cancellationToken);

        IReadOnlyDictionary<AppModule, bool>? grantDict = null;
        if (grants.Count > 0)
            grantDict = grants.ToDictionary(g => g.Module, g => g.IsEnabled);

        var featureMap = BuildFeatureKeysByModule(grants);
        var effective = EffectivePermissionsCalculator.Compute(role, grantDict, featureMap);
        var enabledModules = ResolveEnabledModules(grants, effective);
        var scoped = grants.Count > 0;
        return new UserAccessSnapshot(effective, enabledModules, scoped);
    }

    private async Task<UserRole> ResolveRoleAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var roleName = roles.FirstOrDefault(r => !string.Equals(r, PlatformRoles.PlatformAdmin, StringComparison.Ordinal))
            ?? UserRole.Accountant.ToString();
        return Enum.TryParse<UserRole>(roleName, out var r) ? r : UserRole.Accountant;
    }

    /// <summary>
    /// When no grant rows: all modules enabled in UI/JWT (legacy). When rows exist: toggled-on modules
    /// restricted to those with at least one effective permission (role ∩ module union).
    /// </summary>
    /// <summary>
    /// Pure function over the granted modules (no DB/instance dependency) — shared with
    /// <c>TenantUsersController</c> (which batches grants across users to avoid N+1) so the
    /// List optimization and the service stay in lock-step (single source of truth).
    /// </summary>
    public static IReadOnlyList<AppModule> ResolveEnabledModules(
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

    /// <summary>Pure function over the granted modules — see <see cref="ResolveEnabledModules"/>.</summary>
    public static IReadOnlyDictionary<AppModule, IReadOnlyList<string>>? BuildFeatureKeysByModule(
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
}
