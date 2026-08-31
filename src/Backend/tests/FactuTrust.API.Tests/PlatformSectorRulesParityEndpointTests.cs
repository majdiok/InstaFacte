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
/// Phase 2 — parity endpoint (plan §WP-B9): authorization and response shape for
/// <c>GET /api/platform/sector-rules/parity</c>. Mirrors
/// <see cref="PlatformSectorRulesControllerTests"/>'s JWT-forging pattern. Requires a reachable SQL
/// Server / LocalDB — set <c>RUN_SECTOR_RULES_ADMIN_SQL_TESTS=1</c> to execute; absent (the sandbox
/// default), every fact returns immediately without attempting to build the host.
/// </summary>
[Collection("SqlServerIntegration")]
public sealed class PlatformSectorRulesParityEndpointTests : IClassFixture<ChannelsDisabledWebApplicationFactory>
{
    private static bool ShouldRun => Environment.GetEnvironmentVariable("RUN_SECTOR_RULES_ADMIN_SQL_TESTS") == "1";

    private readonly ChannelsDisabledWebApplicationFactory _factory;

    public PlatformSectorRulesParityEndpointTests(ChannelsDisabledWebApplicationFactory factory)
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
            UserName = $"parity-{unique}@example.com",
            Email = $"parity-{unique}@example.com",
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
    public async Task Parity_endpoint_requires_sector_rules_read_permission()
    {
        if (!ShouldRun) return;

        // BillingAdmin passes the PlatformAdmin entry gate but lacks sector-rules:read → 403.
        var jwt = await CreatePlatformUserJwtAsync(PlatformRoles.BillingAdmin);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await client.GetAsync("/api/platform/sector-rules/parity");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Parity_endpoint_returns_200_with_expected_shape()
    {
        if (!ShouldRun) return;

        // PlatformAdmin has sector-rules:read → the endpoint must return 200 with a parity DTO
        // carrying IsMatch, DbVersion, and a Differences list.
        var jwt = await CreatePlatformUserJwtAsync(PlatformRoles.PlatformAdmin);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await client.GetAsync("/api/platform/sector-rules/parity");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<SectorRuleParityDto>>();
        Assert.NotNull(body?.Data);
        // The test DB is seeded at startup via SeedIfEmptyAsync, so the parity result is deterministic
        // only in its shape — both IsMatch outcomes are valid, but the fields must be populated.
        Assert.True(body!.Data!.DbVersion >= 0);
        Assert.NotNull(body.Data.Differences);
    }
}
