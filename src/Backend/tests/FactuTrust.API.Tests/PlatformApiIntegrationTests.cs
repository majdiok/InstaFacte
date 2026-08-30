using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using FactuTrust.Domain.Auth;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FactuTrust.API.Tests;

/// <summary>
/// Integration checks for /api/platform authorization (requires running API + SQL; may be skipped if DB unavailable).
/// Utilise <see cref="ChannelsDisabledWebApplicationFactory"/> : flags Channels OFF (pas de pont Node en test).
/// </summary>
[Collection("SqlServerIntegration")]
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
        // Depuis la remédiation Utilisateurs/rôles, OnTokenValidated vérifie l'existence et
        // l'état (IsActive/SecurityStamp) de l'utilisateur en base master (révocation immédiate,
        // §6 Phase 2.5) : un jeton forgé pour un Guid inexistant échoue désormais à
        // l'authentification (401) au lieu d'atteindre l'autorisation. Il faut donc un
        // utilisateur "Administrator" (tenant) réellement persisté pour continuer à vérifier
        // que ce rôle — non plateforme — reçoit bien 403 sur les endpoints /api/platform.
        var (userId, securityStamp) = await CreateTenantAdministratorAsync();

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var jwt = CreateSignedJwt(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, "Administrator"),
            new Claim("tenant_id", Guid.NewGuid().ToString()),
            new Claim(AuthClaimTypes.SecurityStamp, securityStamp)
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await client.GetAsync("/api/platform/migrations/tenants/migrations-status");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Crée directement (sans passer par /api/auth/register, coûteux et dépendant du
    /// provisioning tenant) un utilisateur actif avec le rôle tenant "Administrator" dans la
    /// base master, pour disposer d'un Guid réel + SecurityStamp réel utilisables dans un JWT
    /// forgé de test.
    /// </summary>
    private async Task<(Guid UserId, string SecurityStamp)> CreateTenantAdministratorAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var unique = Guid.NewGuid().ToString("N")[..12];
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"platform-403-{unique}@example.com",
            Email = $"platform-403-{unique}@example.com",
            EmailConfirmed = true,
            FirstName = "Test",
            LastName = "TenantAdmin",
            TenantId = Guid.NewGuid(),
            IsActive = true
        };

        var createResult = await userManager.CreateAsync(user, "SecurePass123!");
        Assert.True(createResult.Succeeded, string.Join(";", createResult.Errors.Select(e => e.Description)));

        var roleResult = await userManager.AddToRoleAsync(user, nameof(UserRole.Administrator));
        Assert.True(roleResult.Succeeded, string.Join(";", roleResult.Errors.Select(e => e.Description)));

        var stamp = await userManager.GetSecurityStampAsync(user);
        return (user.Id, stamp);
    }
}
