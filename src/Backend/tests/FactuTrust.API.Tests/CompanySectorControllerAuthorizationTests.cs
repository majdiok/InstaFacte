using System.Reflection;
using System.Security.Claims;
using FactuTrust.API.Authorization;
using FactuTrust.API.Controllers;
using FactuTrust.Domain.Auth;
using FactuTrust.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FactuTrust.API.Tests;

/// <summary>
/// Plan §2.3 — <see cref="CompanySectorController"/> POST preview / PUT must require BOTH the
/// SettingsUpdate permission AND the Administrateur role, so a non-admin with the permission cannot
/// trigger tenant-wide sector re-provisioning; GET stays read-only (SettingsRead only). These tests
/// assert the declarative guard via reflection and prove the AND semantics through the REAL
/// authorization chain (<see cref="PermissionPolicyProvider"/> + <see cref="PermissionAuthorizationHandler"/>),
/// combining the action's <see cref="AuthorizeAttribute"/>s exactly as the MVC pipeline does —
/// without booting WebApplicationFactory (no SQL available in this sandbox).
/// </summary>
public sealed class CompanySectorControllerAuthorizationTests
{
    [Fact]
    public void Preview_and_Update_require_Administrator_role_and_SettingsUpdate()
    {
        Assert.True(HasAuthorizeRoles(nameof(CompanySectorController.Preview), nameof(UserRole.Administrator)));
        Assert.True(HasAuthorizeRoles(nameof(CompanySectorController.Update), nameof(UserRole.Administrator)));
        Assert.True(HasAuthorizePolicy(nameof(CompanySectorController.Preview), PermissionPolicies.SettingsUpdate));
        Assert.True(HasAuthorizePolicy(nameof(CompanySectorController.Update), PermissionPolicies.SettingsUpdate));
    }

    [Fact]
    public void Get_is_read_only_and_does_not_require_Administrator_role()
    {
        Assert.True(HasAuthorizePolicy(nameof(CompanySectorController.Get), PermissionPolicies.SettingsRead));
        Assert.False(HasAuthorizeRoles(nameof(CompanySectorController.Get), nameof(UserRole.Administrator)));
    }

    [Fact]
    public async Task Non_admin_with_SettingsUpdate_is_denied_on_preview_while_admin_is_allowed()
    {
        var (authz, provider) = BuildRealAuthorization();
        var previewPolicy = await CombineActionPolicyAsync(provider, nameof(CompanySectorController.Preview));

        // Has SettingsUpdate (+ SettingsRead) as permission claims but is NOT an Administrator.
        var nonAdmin = PrincipalWith(new[] { Permissions.Settings.Update, Permissions.Settings.Read }, role: null);
        var denied = await authz.AuthorizeAsync(nonAdmin, previewPolicy);
        Assert.False(denied.Succeeded); // 403 equivalent: permission alone is not enough

        // Same permissions AND the Administrateur role ⇒ allowed.
        var admin = PrincipalWith(new[] { Permissions.Settings.Update, Permissions.Settings.Read }, role: UserRole.Administrator);
        var allowed = await authz.AuthorizeAsync(admin, previewPolicy);
        Assert.True(allowed.Succeeded);
    }

    [Fact]
    public async Task Non_admin_with_SettingsRead_is_allowed_on_get()
    {
        var (authz, provider) = BuildRealAuthorization();
        var getPolicy = await CombineActionPolicyAsync(provider, nameof(CompanySectorController.Get));

        var nonAdmin = PrincipalWith(new[] { Permissions.Settings.Read }, role: null);
        var allowed = await authz.AuthorizeAsync(nonAdmin, getPolicy);
        Assert.True(allowed.Succeeded); // GET is read-only — no Administrateur role required
    }

    private static bool HasAuthorizeRoles(string methodName, string role)
    {
        var method = typeof(CompanySectorController).GetMethod(methodName)!;
        return method.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Any(a => !string.IsNullOrEmpty(a.Roles)
                      && a.Roles.Split(',').Select(r => r.Trim()).Contains(role));
    }

    private static bool HasAuthorizePolicy(string methodName, string policy)
    {
        var method = typeof(CompanySectorController).GetMethod(methodName)!;
        return method.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any(a => a.Policy == policy);
    }

    private static async Task<AuthorizationPolicy> CombineActionPolicyAsync(IAuthorizationPolicyProvider provider, string methodName)
    {
        // Mirrors the MVC pipeline: combine every AuthorizeAttribute on the class AND the action into
        // a single policy (permission requirement from [Authorize(Policy=...)] + roles requirement
        // from [Authorize(Roles=...)] + DenyAnonymous from the class-level [Authorize]).
        var method = typeof(CompanySectorController).GetMethod(methodName)!;
        var authorizeData = typeof(CompanySectorController)
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Cast<IAuthorizeData>()
            .Concat(method.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Cast<IAuthorizeData>())
            .ToArray();

        var policy = await AuthorizationPolicy.CombineAsync(provider, authorizeData);
        Assert.NotNull(policy);
        return policy!;
    }

    private static ClaimsPrincipal PrincipalWith(IEnumerable<string> permissions, UserRole? role)
    {
        // Same claims shape TenantAuthTokenService puts on the JWT: authenticated Bearer identity,
        // explicit permission claims (so the permission requirement is satisfied by claims, not by a
        // role fallback), and an optional ClaimTypes.Role claim.
        var identity = new ClaimsIdentity("Bearer", ClaimTypes.NameIdentifier, ClaimTypes.Role);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));
        foreach (var permission in permissions)
            identity.AddClaim(new Claim(AuthClaimTypes.Permission, permission));
        if (role is not null)
            identity.AddClaim(new Claim(ClaimTypes.Role, role.Value.ToString()));

        return new ClaimsPrincipal(identity);
    }

    private static (IAuthorizationService Authz, IAuthorizationPolicyProvider Provider) BuildRealAuthorization()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        var sp = services.BuildServiceProvider();
        return (sp.GetRequiredService<IAuthorizationService>(), sp.GetRequiredService<IAuthorizationPolicyProvider>());
    }
}
