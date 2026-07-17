using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FactuTrust.API.Tests;

/// <summary>
/// Integration checks for /api/platform authorization (requires running API + SQL; may be skipped if DB unavailable).
/// Utilise <see cref="ChannelsDisabledWebApplicationFactory"/> : flags Channels OFF (pas de pont Node en test).
/// </summary>
public sealed class PlatformApiIntegrationTests : IClassFixture<ChannelsDisabledWebApplicationFactory>
{
    private readonly ChannelsDisabledWebApplicationFactory _factory;

    public PlatformApiIntegrationTests(ChannelsDisabledWebApplicationFactory factory)
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

    [Fact]
    public async Task Platform_migrations_without_token_returns_401()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/api/platform/migrations/tenants/migrations-status");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Platform_migrations_with_tenant_administrator_jwt_returns_403()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var jwt = CreateSignedJwt(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "Administrator"),
            new Claim("tenant_id", Guid.NewGuid().ToString())
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await client.GetAsync("/api/platform/migrations/tenants/migrations-status");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
