using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Auth;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FactuTrust.API.Tests;

/// <summary>
/// Phase 2 — backoffice tenant sector re-configuration controller (plan §WP-B7): authorization
/// checks for <c>PlatformTenantSectorConfigurationController</c> and the French 404 contract.
/// Mirrors <see cref="PlatformSectorRulesControllerTests"/>'s JWT-forging pattern. Requires a
/// reachable SQL Server / LocalDB (same as the other integration tests) — set
/// <c>RUN_SECTOR_RULES_ADMIN_SQL_TESTS=1</c> to execute; absent (the sandbox default), every fact
/// returns immediately without attempting to build the host.
/// </summary>
[Collection("SqlServerIntegration")]
public sealed class PlatformTenantSectorConfigurationControllerTests : IClassFixture<ChannelsDisabledWebApplicationFactory>
{
    private static bool ShouldRun => Environment.GetEnvironmentVariable("RUN_SECTOR_RULES_ADMIN_SQL_TESTS") == "1";

    private readonly ChannelsDisabledWebApplicationFactory _factory;

    public PlatformTenantSectorConfigurationControllerTests(ChannelsDisabledWebApplicationFactory factory)
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
            UserName = $"sector-reconfig-{unique}@example.com",
            Email = $"sector-reconfig-{unique}@example.com",
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

    private static System.Net.Http.Json.JsonContent RequestPayload() => System.Net.Http.Json.JsonContent.Create(new
    {
        companySegment = "commerce",
        recomputeModuleGrants = true,
        applyDataTemplates = false
    });

    [Fact]
    public async Task Preview_requires_sector_rules_read_permission()
    {
        if (!ShouldRun) return;

        // BillingAdmin passes the PlatformAdmin entry gate but lacks sector-rules:read → 403.
        var jwt = await CreatePlatformUserJwtAsync(PlatformRoles.BillingAdmin);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await client.PostAsync(
            $"/api/platform/tenants/{Guid.NewGuid()}/sector-configuration/preview",
            RequestPayload());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Apply_requires_sector_rules_apply_permission()
    {
        if (!ShouldRun) return;

        // ReadOnlyAuditor has sector-rules:read (so it passes the read gate on a GET) but NOT
        // sector-rules:apply → the apply action must still be 403, proving the apply permission is
        // gated independently of read.
        var jwt = await CreatePlatformUserJwtAsync(PlatformRoles.ReadOnlyAuditor);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await client.PostAsync(
            $"/api/platform/tenants/{Guid.NewGuid()}/sector-configuration/apply",
            RequestPayload());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Preview_returns_404_for_unknown_tenant_French()
    {
        if (!ShouldRun) return;

        // PlatformAdmin has every permission, so both gates pass and the request reaches the service,
        // which resolves the (random, non-existent) tenant to null → 404 with the French message.
        var jwt = await CreatePlatformUserJwtAsync(PlatformRoles.PlatformAdmin);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var unknownTenantId = Guid.NewGuid();
        var response = await client.PostAsync(
            $"/api/platform/tenants/{unknownTenantId}/sector-configuration/preview",
            RequestPayload());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<SectorReconfigurationPreviewDto>>();
        Assert.NotNull(body);
        Assert.False(body!.Success);
        Assert.Contains("introuvable", body.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Validation.TenantNotFound", body.Code);
    }
}
