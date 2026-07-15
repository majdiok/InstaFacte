using System.Net;
using System.Net.Http.Json;
using FactuTrust.Application.DTOs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FactuTrust.API.Tests;

/// <summary>
/// Ensures optional bootstrap credentials create a platform admin that can call <c>/api/platform/auth/login</c>.
/// Requires a reachable SQL Server / LocalDB (same as other integration tests).
/// Hérite de <see cref="ChannelsDisabledWebApplicationFactory"/> : les flags Channels sont forcés
/// OFF (aucun pont Node lancé pendant les tests).
/// </summary>
public sealed class PlatformBootstrapWebApplicationFactory : ChannelsDisabledWebApplicationFactory
{
    public const string BootstrapTestEmail = "bootstrap-platform-test@factutrust.local";
    public const string BootstrapTestPassword = "BootstrapTest_Pw1!Xy";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Bootstrap:PlatformAdmin:Email"] = BootstrapTestEmail,
                ["Bootstrap:PlatformAdmin:Password"] = BootstrapTestPassword,
            });
        });
    }
}

/// <summary>
/// Les classes qui se connectent avec le compte bootstrap partagent cette collection : xUnit ne
/// les exécute jamais en parallèle (deux logins concurrents du même compte font courir l'écriture
/// RefreshToken/session ⇒ DbUpdateConcurrencyException ⇒ 409 flaky).
/// </summary>
[CollectionDefinition("PlatformBootstrapLogin")]
public sealed class PlatformBootstrapLoginCollection;

[Collection("PlatformBootstrapLogin")]
public sealed class PlatformBootstrapIntegrationTests : IClassFixture<PlatformBootstrapWebApplicationFactory>
{
    private readonly PlatformBootstrapWebApplicationFactory _factory;

    public PlatformBootstrapIntegrationTests(PlatformBootstrapWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Platform_login_succeeds_when_bootstrap_credentials_configured()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PostAsJsonAsync(
            "/api/platform/auth/login",
            new LoginDto
            {
                Email = PlatformBootstrapWebApplicationFactory.BootstrapTestEmail,
                Password = PlatformBootstrapWebApplicationFactory.BootstrapTestPassword
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
