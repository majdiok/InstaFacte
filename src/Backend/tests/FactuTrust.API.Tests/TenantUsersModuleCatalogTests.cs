using System.Net;
using System.Net.Http.Json;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Authorization;
using FactuTrust.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FactuTrust.API.Tests;

/// <summary>
/// Integration coverage for the module-catalog endpoint and the 403 authorization boundary of
/// <c>TenantUsersController</c> — plan §6 Phase 2.1, §7.1.E. Requires a real SQL Server (company
/// registration provisions a real tenant DB); the admin session is registered ONCE per test class
/// (<see cref="IAsyncLifetime"/>) and reused across facts to keep the real-DB cost bounded — the
/// module-catalog endpoint itself never touches the DB (pure Domain calculation), so read-only
/// facts against the same session are independent of each other.
/// </summary>
public sealed class TenantUsersModuleCatalogTests : IClassFixture<ChannelsDisabledWebApplicationFactory>, IAsyncLifetime
{
    private readonly ChannelsDisabledWebApplicationFactory _factory;
    private HttpClient _adminClient = null!;
    private TenantUsersTestSupport.RegisteredCompany _company = null!;

    public TenantUsersModuleCatalogTests(ChannelsDisabledWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        var registerClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        _company = await TenantUsersTestSupport.RegisterCompanyAsync(registerClient);

        _adminClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false })
            .WithBearer(_company.AccessToken);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Module_catalog_without_token_returns_401()
    {
        var anonymousClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await anonymousClient.GetAsync("/api/tenant-users/module-catalog?role=Administrator");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/tenant-users")]
    [InlineData("GET", "/api/tenant-users/module-catalog?role=Administrator")]
    [InlineData("POST", "/api/tenant-users")]
    [InlineData("POST", "/api/tenant-users/batch")]
    [InlineData("PATCH", "/api/tenant-users/11111111-1111-1111-1111-111111111111")]
    public async Task Every_TenantUsersController_endpoint_returns_403_for_non_admin(string method, string path)
    {
        // Authorization ([Authorize(Roles=Administrator)]) runs before TenantMiddleware in the
        // pipeline (Program.cs: UseAuthorization precedes UseMiddleware<TenantMiddleware>), so a
        // persisted-but-non-admin user never needs a resolvable tenant DB to be rejected here.
        var jwt = await TenantUsersTestSupport.CreatePersistedUserJwtAsync(_factory, UserRole.Accountant);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false })
            .WithBearer(jwt);

        var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = method is "POST" or "PATCH" ? JsonContent.Create(new { }) : null
        };
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Module_catalog_invalid_role_returns_400()
    {
        var response = await _adminClient.GetAsync("/api/tenant-users/module-catalog?role=NotARealRole");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Module_catalog_administrator_matches_the_documented_shape()
    {
        var response = await _adminClient.GetAsync("/api/tenant-users/module-catalog?role=Administrator");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ModuleCatalogDto>>(TenantUsersTestSupport.ApiJsonOptions);
        Assert.NotNull(body?.Data);
        var catalog = body!.Data!;

        Assert.Equal(UserRole.Administrator, catalog.Role);
        Assert.NotEmpty(catalog.Modules);

        foreach (var module in catalog.Modules)
        {
            Assert.False(string.IsNullOrWhiteSpace(module.DisplayName));

            // Administrator can grant every module EXCEPT Honoraires: that module is exclusively
            // the accounting-firm "cabinet" fee-billing feature, driven entirely by
            // DelegatedPermissionCatalog for FirmManager/FirmAccountant — it is never part of
            // UserRole.Administrator's own base permission set (a Company-tenant role) and has no
            // RoleModuleGrantCeilingExtensions delta either, so its ceiling is legitimately empty.
            // This is architecture, not a regression (verified: every other of the 18 AppModule
            // values is grantable for Administrator).
            if (module.Module == AppModule.Honoraires)
            {
                Assert.False(module.Grantable, "Honoraires is cabinet-only and must stay non-grantable for Administrator");
                continue;
            }

            Assert.True(module.Grantable, $"{module.Module} should be grantable for Administrator");

            foreach (var feature in module.Features)
            {
                Assert.False(string.IsNullOrWhiteSpace(feature.Key));
                // Administrator's own base permission set is a superset of the whole ceiling
                // (plan §5.2/§5.3), so no feature should ever present as an extension for this role.
                Assert.False(feature.IsExtension, $"{module.Module}/{feature.Key} unexpectedly marked isExtension for Administrator");
            }
        }
    }

    [Fact]
    public async Task Module_catalog_salesrep_marks_treasury_entry_features_as_extension()
    {
        // Plan §7.1.E worked example: SalesRep gains Treasury entry features only via an explicit
        // module grant — RoleModuleGrantCeilingExtensions's delta table, never SalesRep's own base
        // permissions — so the catalog must flag them isExtension=true.
        var response = await _adminClient.GetAsync("/api/tenant-users/module-catalog?role=SalesRep");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ModuleCatalogDto>>(TenantUsersTestSupport.ApiJsonOptions);
        var catalog = body!.Data!;

        var treasury = catalog.Modules.Single(m => m.Module == AppModule.Treasury);
        Assert.True(treasury.Grantable);
        Assert.Contains(treasury.Features, f => f.IsExtension);
    }

    [Theory]
    [InlineData(UserRole.Client)]
    [InlineData(UserRole.FirmManager)]
    [InlineData(UserRole.FirmAccountant)]
    public async Task Module_catalog_excluded_roles_return_200_non_grantable_with_no_features(UserRole excludedRole)
    {
        // Plan §5.3/§6-2.1 unified behavior: excluded roles NEVER 400 on catalog read, but every
        // module is non-grantable and carries NO features at all (not just empty allowed lists).
        Assert.True(RoleModuleGrantCeilingExtensions.IsExcludedFromModuleGrants(excludedRole));

        var response = await _adminClient.GetAsync($"/api/tenant-users/module-catalog?role={excludedRole}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ModuleCatalogDto>>(TenantUsersTestSupport.ApiJsonOptions);
        var catalog = body!.Data!;

        Assert.Equal(excludedRole, catalog.Role);
        Assert.NotEmpty(catalog.Modules);
        foreach (var module in catalog.Modules)
        {
            Assert.False(module.Grantable, $"{module.Module} should be non-grantable for excluded role {excludedRole}");
            Assert.Empty(module.Features);
        }
    }

    [Fact]
    public async Task Create_user_with_module_not_grantable_for_role_returns_400()
    {
        // Write-time validation (plan §6 Phase 2.2): SalesRep cannot be granted the Administration
        // module (never in its ceiling) — rejected explicitly, never silently trimmed.
        var unique = Guid.NewGuid().ToString("N")[..10];
        var request = new
        {
            Email = $"reject-{unique}@example.com",
            FirstName = "Reject",
            LastName = "Test",
            Password = "SecurePass123!",
            Role = "SalesRep",
            ModuleAccess = new[] { new { Module = "Administration", Enabled = true, EnabledFeatureKeys = (string[]?)null } }
        };

        var response = await _adminClient.PostAsJsonAsync("/api/tenant-users", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_firm_only_role_on_company_tenant_returns_400()
    {
        // Tenant-kind/role compatibility (plan §6 Phase 2.2): this registered company is a
        // TenantKind.Company tenant, so FirmManager (a firm-only role) must be rejected.
        var unique = Guid.NewGuid().ToString("N")[..10];
        var request = new
        {
            Email = $"firmrole-{unique}@example.com",
            FirstName = "Firm",
            LastName = "Role",
            Password = "SecurePass123!",
            Role = "FirmManager"
        };

        var response = await _adminClient.PostAsJsonAsync("/api/tenant-users", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
