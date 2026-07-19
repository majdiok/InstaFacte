using System.Security.Claims;
using FactuTrust.API.Authorization;
using FactuTrust.Domain.Auth;
using FactuTrust.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace FactuTrust.API.Tests;

public sealed class FirmDelegatedContextAuthorizationHandlerTests
{
    private static ClaimsPrincipal BuildUser(
        string tenantKind,
        string accessMode,
        string? contextTenantId = null,
        string? validatePermission = null)
    {
        var id = new ClaimsIdentity("Bearer");
        id.AddClaim(new Claim(AuthClaimTypes.TenantKind, tenantKind));
        id.AddClaim(new Claim(AuthClaimTypes.AccessMode, accessMode));
        if (!string.IsNullOrEmpty(contextTenantId))
            id.AddClaim(new Claim(AuthClaimTypes.ContextTenantId, contextTenantId));
        if (!string.IsNullOrEmpty(validatePermission))
            id.AddClaim(new Claim(AuthClaimTypes.Permission, validatePermission));
        return new ClaimsPrincipal(id);
    }

    [Fact]
    public async Task Succeeds_for_delegated_accounting_firm_with_context_tenant()
    {
        var handler = new FirmDelegatedContextAuthorizationHandler();
        var user = BuildUser("accountingFirm", "delegated", Guid.NewGuid().ToString());
        var ctx = new AuthorizationHandlerContext(
            new IAuthorizationRequirement[] { new FirmDelegatedContextRequirement() },
            user,
            resource: null);

        await handler.HandleAsync(ctx);

        Assert.True(ctx.HasSucceeded);
    }

    [Fact]
    public async Task Fails_for_company_native_user()
    {
        var handler = new FirmDelegatedContextAuthorizationHandler();
        var user = BuildUser("company", "native");
        var ctx = new AuthorizationHandlerContext(
            new IAuthorizationRequirement[] { new FirmDelegatedContextRequirement() },
            user,
            resource: null);

        await handler.HandleAsync(ctx);

        Assert.False(ctx.HasSucceeded);
    }

    [Fact]
    public async Task Fails_for_accounting_firm_native_user()
    {
        var handler = new FirmDelegatedContextAuthorizationHandler();
        var user = BuildUser("accountingFirm", "native");
        var ctx = new AuthorizationHandlerContext(
            new IAuthorizationRequirement[] { new FirmDelegatedContextRequirement() },
            user,
            resource: null);

        await handler.HandleAsync(ctx);

        Assert.False(ctx.HasSucceeded);
    }

    [Fact]
    public async Task Fails_for_delegated_firm_without_context_tenant_id()
    {
        var handler = new FirmDelegatedContextAuthorizationHandler();
        var user = BuildUser("accountingFirm", "delegated");
        var ctx = new AuthorizationHandlerContext(
            new IAuthorizationRequirement[] { new FirmDelegatedContextRequirement() },
            user,
            resource: null);

        await handler.HandleAsync(ctx);

        Assert.False(ctx.HasSucceeded);
    }

    [Theory]
    [InlineData("company", "native")]
    [InlineData("company", "delegated")]
    [InlineData("accountingFirm", "native")]
    public void AccountingValidationAccess_IsFalse_for_non_delegated_firm_context(string tenantKind, string accessMode)
    {
        var user = BuildUser(tenantKind, accessMode, Guid.NewGuid().ToString());
        Assert.False(AccountingValidationAccess.IsAccountingFirmDelegatedContext(user));
    }

    [Fact]
    public void AccountingValidationAccess_IsTrue_for_delegated_firm_with_context()
    {
        var user = BuildUser("accountingFirm", "delegated", Guid.NewGuid().ToString());
        Assert.True(AccountingValidationAccess.IsAccountingFirmDelegatedContext(user));
    }
}
