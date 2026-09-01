using System.Reflection;
using System.Security.Claims;
using FactuTrust.API.Authorization;
using FactuTrust.API.Controllers;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Auth;
using FactuTrust.Domain.Authorization;
using FactuTrust.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FactuTrust.API.Tests;

/// <summary>
/// Plan §5.2 — proves feature-level enforcement at the API is real, not just wired.
///
/// A true end-to-end test would boot the API host (WebApplicationFactory) and issue a real HTTP
/// GET against a quotes-permission endpoint (e.g. QuotesController, [Authorize(Policy =
/// PermissionPolicies.QuotesRead)]). That is infeasible in this sandbox: ~30 pre-existing
/// FactuTrust.API.Tests WebApplicationFactory integration tests already fail here because there is
/// no real SQL Server/Hangfire available. Rather than add another test that would be red for
/// infrastructure reasons unrelated to the behavior under test, this instead exercises the exact
/// two links of the real enforcement chain at the unit level:
///
///  1. <see cref="EffectivePermissionsCalculator"/> + a DI-resolved <see cref="IAuthorizationService"/>
///     running the REAL <see cref="PermissionPolicyProvider"/> + <see cref="PermissionAuthorizationHandler"/>
///     pair registered exactly as in Program.cs (review R5c: previously this test new'd up
///     PermissionAuthorizationHandler and an AuthorizationHandlerContext by hand, bypassing the
///     policy-name-to-requirement resolution done by PermissionPolicyProvider.GetPolicyAsync —
///     a bug there could pass unnoticed). QuotesController's list endpoint uses
///     PermissionPolicies.QuotesRead == "perm:" + Permissions.Quotes.Read, so denial of that policy
///     name here is equivalent to a 403 on that endpoint.
///  2. TenantUsersController.ValidateModuleAccessItems — the private static grant-write guard that
///     rejects unknown feature keys before any grant row is persisted — invoked via reflection
///     since it is intentionally private (no public surface widened just for a test).
/// </summary>
public sealed class FeatureLevelAuthorizationTests
{
    private static IAuthorizationService BuildRealAuthorizationService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    [Fact]
    public async Task Utilisateur_Sales_limite_a_invoices_recoit_403_sur_quotes()
    {
        var grants = Enum.GetValues<AppModule>().ToDictionary(m => m, _ => false);
        grants[AppModule.Sales] = true;

        var features = new Dictionary<AppModule, IReadOnlyList<string>>
        {
            [AppModule.Sales] = new[] { "invoices" }
        };

        var effective = EffectivePermissionsCalculator.Compute(UserRole.Accountant, grants, features);

        // Same claims shape TenantAuthTokenService.cs puts on the JWT for a module-scoped user.
        var identity = new ClaimsIdentity("Bearer");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));
        identity.AddClaim(new Claim(AuthClaimTypes.PermissionSource, "modules"));
        foreach (var permission in effective)
            identity.AddClaim(new Claim(AuthClaimTypes.Permission, permission));
        var user = new ClaimsPrincipal(identity);

        var authorizationService = BuildRealAuthorizationService();

        var quotesResult = await authorizationService.AuthorizeAsync(user, resource: null, PermissionPolicies.QuotesRead);
        Assert.False(quotesResult.Succeeded); // 403 equivalent on the quotes-permission endpoint

        var invoicesResult = await authorizationService.AuthorizeAsync(user, resource: null, PermissionPolicies.InvoicesRead);
        Assert.True(invoicesResult.Succeeded); // invoices stays allowed: the denial is feature-scoped, not a global lockout
    }

    [Fact]
    public void Feature_key_inconnue_rejetee_a_l_ecriture_des_grants()
    {
        var method = typeof(TenantUsersController).GetMethod(
            "ValidateModuleAccessItems", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var invalidItems = new List<UserModuleAccessItemDto>
        {
            new() { Module = AppModule.Sales, Enabled = true, EnabledFeatureKeys = new[] { "not_a_real_feature" } }
        };
        var invalidResult = (string?)method!.Invoke(null, new object?[] { UserRole.Administrator, invalidItems });
        Assert.NotNull(invalidResult);
        Assert.Contains("invalide", invalidResult, StringComparison.OrdinalIgnoreCase);

        // Control: the same shape with a real feature key for the same module passes — proves the
        // rejection above is about the unknown key, not an unrelated validation error.
        var validItems = new List<UserModuleAccessItemDto>
        {
            new() { Module = AppModule.Sales, Enabled = true, EnabledFeatureKeys = new[] { "invoices" } }
        };
        var validResult = (string?)method!.Invoke(null, new object?[] { UserRole.Administrator, validItems });
        Assert.Null(validResult);
    }

    [Fact]
    public void ModuleFeatureCatalog_rejette_une_cle_inconnue_et_accepte_une_cle_connue()
    {
        Assert.False(ModuleFeatureCatalog.IsValidFeatureKey(AppModule.Sales, "not_a_real_feature"));
        Assert.True(ModuleFeatureCatalog.IsValidFeatureKey(AppModule.Sales, "invoices"));
    }
}
