using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.AI.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FactuTrust.API.Tests;

[Collection("PlatformBootstrapLogin")]
public sealed class PlatformTenantModalSettingsIntegrationTests : IClassFixture<PlatformBootstrapWebApplicationFactory>
{
    private static readonly JsonSerializerOptions ApiJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly PlatformBootstrapWebApplicationFactory _factory;

    public PlatformTenantModalSettingsIntegrationTests(PlatformBootstrapWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Get_unknown_tenant_returns_404_without_token_leak()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var token = await LoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var unknownId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var response = await client.GetAsync($"/api/platform/tenants/{unknownId}/modal-settings");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("wk-", json);
        Assert.DoesNotContain("ws-", json);
    }

    [Fact]
    public async Task Put_unknown_tenant_returns_404()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var token = await LoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var unknownId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var response = await client.PutAsJsonAsync(
            $"/api/platform/tenants/{unknownId}/modal-settings",
            new UpdateTenantModalSettingsRequest
            {
                IsEnabled = false,
                DisplayName = "Modal"
            });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<string> LoginAsync(HttpClient client)
    {
        var loginResponse = await client.PostAsJsonAsync(
            "/api/platform/auth/login",
            new LoginDto
            {
                Email = PlatformBootstrapWebApplicationFactory.BootstrapTestEmail,
                Password = PlatformBootstrapWebApplicationFactory.BootstrapTestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var json = await loginResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var token = doc.RootElement.GetProperty("data").GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token!;
    }
}
