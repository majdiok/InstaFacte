using System.Security.Claims;
using FactuTrust.API.Authorization;
using FactuTrust.Domain.Auth;
using FactuTrust.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace FactuTrust.API.Tests;

public sealed class PermissionAuthorizationHandlerTests
{
    [Fact]
    public async Task Succeeds_when_jwt_style_perm_claim_present()
    {
        var handler = new PermissionAuthorizationHandler();
        var id = new ClaimsIdentity("Bearer");
        id.AddClaim(new Claim(AuthClaimTypes.Permission, Permissions.Settings.Update));
        var user = new ClaimsPrincipal(id);
        var requirement = new PermissionRequirement(Permissions.Settings.Update);
        var ctx = new AuthorizationHandlerContext(new IAuthorizationRequirement[] { requirement }, user, resource: null);

        await handler.HandleAsync(ctx);

        Assert.True(ctx.HasSucceeded);
    }

    [Fact]
    public async Task Fails_when_perm_claim_missing_for_requirement()
    {
        var handler = new PermissionAuthorizationHandler();
        var id = new ClaimsIdentity("Bearer");
        id.AddClaim(new Claim(AuthClaimTypes.Permission, Permissions.Invoices.Read));
        var user = new ClaimsPrincipal(id);
        var requirement = new PermissionRequirement(Permissions.Settings.Update);
        var ctx = new AuthorizationHandlerContext(new IAuthorizationRequirement[] { requirement }, user, resource: null);

        await handler.HandleAsync(ctx);

        Assert.False(ctx.HasSucceeded);
    }

    [Fact]
    public async Task Legacy_token_without_perm_claims_falls_back_to_role_matrix()
    {
        var handler = new PermissionAuthorizationHandler();
        var id = new ClaimsIdentity("Bearer");
        id.AddClaim(new Claim(ClaimTypes.Role, UserRole.Accountant.ToString()));
        var user = new ClaimsPrincipal(id);
        var requirement = new PermissionRequirement(Permissions.Clients.Read);
        var ctx = new AuthorizationHandlerContext(new IAuthorizationRequirement[] { requirement }, user, resource: null);

        await handler.HandleAsync(ctx);

        Assert.True(ctx.HasSucceeded);
    }

    [Fact]
    public async Task Legacy_accountant_denied_settings_update()
    {
        var handler = new PermissionAuthorizationHandler();
        var id = new ClaimsIdentity("Bearer");
        id.AddClaim(new Claim(ClaimTypes.Role, UserRole.Accountant.ToString()));
        var user = new ClaimsPrincipal(id);
        var requirement = new PermissionRequirement(Permissions.Settings.Update);
        var ctx = new AuthorizationHandlerContext(new IAuthorizationRequirement[] { requirement }, user, resource: null);

        await handler.HandleAsync(ctx);

        Assert.False(ctx.HasSucceeded);
    }

    [Fact]
    public async Task Module_scoped_without_perm_claims_denies_even_if_admin_role()
    {
        var handler = new PermissionAuthorizationHandler();
        var id = new ClaimsIdentity("Bearer");
        id.AddClaim(new Claim(ClaimTypes.Role, UserRole.Administrator.ToString()));
        id.AddClaim(new Claim(AuthClaimTypes.PermissionSource, "modules"));
        var user = new ClaimsPrincipal(id);
        var requirement = new PermissionRequirement(Permissions.Clients.Read);
        var ctx = new AuthorizationHandlerContext(new IAuthorizationRequirement[] { requirement }, user, resource: null);

        await handler.HandleAsync(ctx);

        Assert.False(ctx.HasSucceeded);
    }

    [Fact]
    public async Task Module_scoped_with_matching_perm_succeeds()
    {
        var handler = new PermissionAuthorizationHandler();
        var id = new ClaimsIdentity("Bearer");
        id.AddClaim(new Claim(ClaimTypes.Role, UserRole.Administrator.ToString()));
        id.AddClaim(new Claim(AuthClaimTypes.PermissionSource, "modules"));
        id.AddClaim(new Claim(AuthClaimTypes.Permission, Permissions.Clients.Read));
        var user = new ClaimsPrincipal(id);
        var requirement = new PermissionRequirement(Permissions.Clients.Read);
        var ctx = new AuthorizationHandlerContext(new IAuthorizationRequirement[] { requirement }, user, resource: null);

        await handler.HandleAsync(ctx);

        Assert.True(ctx.HasSucceeded);
    }
}
