using System.Net;
using System.Net.Http.Json;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FactuTrust.API.Tests;

/// <summary>
/// HTTP-level coverage for immediate access-token revocation (plan §6 Phase 2.5, §7.1.F).
/// Complements (does not duplicate) the Infrastructure-level
/// <c>SecurityStampTokenValidatorTests</c>, which already covers fail-closed behavior on any lookup
/// exception and both <c>RequireSecurityStampClaim</c> rollout stages against an isolated
/// <c>ISecurityStampTokenValidator</c> — those scenarios don't need a real DB outage simulated
/// through the full HTTP pipeline. This file instead proves the end-to-end contract that actually
/// matters to a caller: after a sensitive PATCH commits, the OLD token is immediately rejected; and
/// if that PATCH's transaction rolls back, the OLD token is NOT affected (atomicity).
/// Requires a real SQL Server (company registration provisions a real tenant DB).
/// </summary>
public sealed class SecurityStampRevocationTests : IClassFixture<ChannelsDisabledWebApplicationFactory>
{
    private readonly ChannelsDisabledWebApplicationFactory _factory;

    public SecurityStampRevocationTests(ChannelsDisabledWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient NewClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Role_change_revokes_the_old_token_immediately()
    {
        var company = await TenantUsersTestSupport.RegisterCompanyAsync(NewClient());
        var admin1Client = NewClient().WithBearer(company.AccessToken);

        var admin2Email = await TenantUsersTestSupport.CreateAdditionalAdminAsync(admin1Client);
        var admin2OldToken = await TenantUsersTestSupport.LoginAsync(NewClient(), admin2Email);
        var admin2OldClient = NewClient().WithBearer(admin2OldToken);

        // Sanity: the old token works BEFORE the demotion.
        var beforeResponse = await admin2OldClient.GetAsync("/api/tenant-users");
        Assert.Equal(HttpStatusCode.OK, beforeResponse.StatusCode);

        var users = await TenantUsersTestSupport.GetUsersAsync(admin1Client);
        var admin2Id = users.Single(u => u.Email == admin2Email).Id;

        var patchResponse = await admin1Client.PatchAsJsonAsync(
            $"/api/tenant-users/{admin2Id}",
            new UpdateTenantUserRequest { Role = UserRole.SalesRep },
            TenantUsersTestSupport.ApiJsonOptions);
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        // The OLD token (captured before the demotion) must be rejected at the AUTHENTICATION stage
        // (401, via OnTokenValidated's security-stamp check) — not merely at the authorization stage
        // (403) — immediately, no sleep needed: this is the very first lookup for this user's stamp
        // since the demotion, so it can never be served from a stale 5s cache entry.
        var afterListResponse = await admin2OldClient.GetAsync("/api/tenant-users");
        Assert.Equal(HttpStatusCode.Unauthorized, afterListResponse.StatusCode);

        var afterCatalogResponse = await admin2OldClient.GetAsync("/api/tenant-users/module-catalog?role=Administrator");
        Assert.Equal(HttpStatusCode.Unauthorized, afterCatalogResponse.StatusCode);
    }

    [Fact]
    public async Task Deactivation_revokes_the_old_token_immediately()
    {
        var company = await TenantUsersTestSupport.RegisterCompanyAsync(NewClient());
        var admin1Client = NewClient().WithBearer(company.AccessToken);

        var admin2Email = await TenantUsersTestSupport.CreateAdditionalAdminAsync(admin1Client);
        var admin2OldToken = await TenantUsersTestSupport.LoginAsync(NewClient(), admin2Email);
        var admin2OldClient = NewClient().WithBearer(admin2OldToken);

        var users = await TenantUsersTestSupport.GetUsersAsync(admin1Client);
        var admin2Id = users.Single(u => u.Email == admin2Email).Id;

        var patchResponse = await admin1Client.PatchAsJsonAsync(
            $"/api/tenant-users/{admin2Id}",
            new UpdateTenantUserRequest { IsActive = false },
            TenantUsersTestSupport.ApiJsonOptions);
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        var afterResponse = await admin2OldClient.GetAsync("/api/tenant-users");
        Assert.Equal(HttpStatusCode.Unauthorized, afterResponse.StatusCode);
    }

    [Fact]
    public async Task Failed_mutation_rolls_back_atomically_and_leaves_the_old_token_valid()
    {
        var company = await TenantUsersTestSupport.RegisterCompanyAsync(NewClient());
        var admin1Client = NewClient().WithBearer(company.AccessToken);

        var admin2Email = await TenantUsersTestSupport.CreateAdditionalAdminAsync(admin1Client);
        var admin2Token = await TenantUsersTestSupport.LoginAsync(NewClient(), admin2Email);
        var admin2Client = NewClient().WithBearer(admin2Token);

        var users = await TenantUsersTestSupport.GetUsersAsync(admin1Client);
        var admin2Id = users.Single(u => u.Email == admin2Email).Id;

        // Role change bundled with a password that fails Identity's policy (RequiredLength=12,
        // digit/upper/lower/non-alphanumeric required — appsettings-configured, Program.cs) — the
        // whole PATCH transaction must roll back BEFORE the security-stamp rotation step runs
        // (TenantUsersController.Update: NewPassword failure returns/rolls back before
        // mustRevokeCurrentToken is ever reached), so neither the role NOR the stamp actually changed.
        var patchResponse = await admin1Client.PatchAsJsonAsync(
            $"/api/tenant-users/{admin2Id}",
            new UpdateTenantUserRequest { Role = UserRole.SalesRep, NewPassword = "short" },
            TenantUsersTestSupport.ApiJsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, patchResponse.StatusCode);

        // admin2's token, captured before the failed PATCH, must still work — proving no partial
        // state (role unchanged, stamp unchanged) leaked out of the rolled-back transaction.
        var afterResponse = await admin2Client.GetAsync("/api/tenant-users");
        Assert.Equal(HttpStatusCode.OK, afterResponse.StatusCode);

        var stillAdmin = await TenantUsersTestSupport.GetUsersAsync(admin2Client);
        Assert.Equal(UserRole.Administrator, stillAdmin.Single(u => u.Email == admin2Email).Role);
    }
}
