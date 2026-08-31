using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using FactuTrust.Domain.Auth;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FactuTrust.API.Tests;

/// <summary>
/// Phase 2 — backoffice admin CRUD over the sector-rule tables (plan §WP-B5): authorization
/// checks for <c>PlatformSectorRulesController</c>. Mirrors <see cref="PlatformApiIntegrationTests"/>'s
/// JWT-forging pattern. Requires a reachable SQL Server / LocalDB (same as other integration
/// tests) — set <c>RUN_SECTOR_RULES_ADMIN_SQL_TESTS=1</c> to execute; absent (the sandbox
/// default), every fact returns immediately without attempting to build the host.
/// </summary>
[Collection("SqlServerIntegration")]
public sealed class PlatformSectorRulesControllerTests : IClassFixture<ChannelsDisabledWebApplicationFactory>
{
    private static bool ShouldRun => Environment.GetEnvironmentVariable("RUN_SECTOR_RULES_ADMIN_SQL_TESTS") == "1";

    private readonly ChannelsDisabledWebApplicationFactory _factory;

    public PlatformSectorRulesControllerTests(ChannelsDisabledWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static string CreateSignedJwt(IEnumerable<Claim> claims)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            ChannelsDisabledWebApplicationFactory.TestJwtSecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "FactuTrust",
            audience: "FactuTrust-API",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Creates (idempotently) a real persisted platform-role user, mirroring the exact
    /// claim set <c>PlatformAuthController.GeneratePlatformTokensAsync</c> issues on real login —
    /// role claim(s) + one <see cref="AuthClaimTypes.Permission"/> claim per effective permission.</summary>
    private async Task<string> CreatePlatformUserJwtAsync(string role)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

        if (!await roleManager.RoleExistsAsync(role))
        {
            var roleCreate = await roleManager.CreateAsync(new ApplicationRole { Name = role, NormalizedName = role.ToUpperInvariant() });
            Assert.True(roleCreate.Succeeded, string.Join(";", roleCreate.Errors.Select(e => e.Description)));
        }

        var unique = Guid.NewGuid().ToString("N")[..12];
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"sector-rules-{unique}@example.com",
            Email = $"sector-rules-{unique}@example.com",
            EmailConfirmed = true,
            FirstName = "Test",
            LastName = "PlatformUser",
            TenantId = Guid.Empty,
            IsActive = true
        };

        var createResult = await userManager.CreateAsync(user, "SecurePass123!");
        Assert.True(createResult.Succeeded, string.Join(";", createResult.Errors.Select(e => e.Description)));

        var addRoleResult = await userManager.AddToRoleAsync(user, role);
        Assert.True(addRoleResult.Succeeded, string.Join(";", addRoleResult.Errors.Select(e => e.Description)));

        var stamp = await userManager.GetSecurityStampAsync(user);

        var permissions = RolePermissionMatrix.ComputeEffectivePermissions(new[] { role });
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Role, role),
            new(AuthClaimTypes.SecurityStamp, stamp)
        };
        claims.AddRange(permissions.Select(p => new Claim(AuthClaimTypes.Permission, p)));

        return CreateSignedJwt(claims);
    }

    [Fact]
    public async Task Endpoints_require_platform_entry_policy_401_anonymous()
    {
        if (!ShouldRun) return;

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/api/platform/sector-rules");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Read_endpoint_allows_readonly_auditor()
    {
        if (!ShouldRun) return;

        var jwt = await CreatePlatformUserJwtAsync(PlatformRoles.ReadOnlyAuditor);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await client.GetAsync("/api/platform/sector-rules/segments");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Write_endpoint_rejects_readonly_auditor_403()
    {
        if (!ShouldRun) return;

        var jwt = await CreatePlatformUserJwtAsync(PlatformRoles.ReadOnlyAuditor);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await client.PostAsync("/api/platform/sector-rules/segments", JsonContent(new
        {
            code = "seg-test",
            labelFr = "Test",
            descriptionFr = "Test",
            iconKey = "icon"
        }));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Seed_endpoint_rejects_non_super_admin_403()
    {
        if (!ShouldRun) return;

        var jwt = await CreatePlatformUserJwtAsync(PlatformRoles.BillingAdmin);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await client.PostAsync(
            "/api/platform/sector-rules/seed-from-catalog?force=false",
            new StringContent(string.Empty));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static System.Net.Http.Json.JsonContent JsonContent<T>(T value) => System.Net.Http.Json.JsonContent.Create(value);
}
