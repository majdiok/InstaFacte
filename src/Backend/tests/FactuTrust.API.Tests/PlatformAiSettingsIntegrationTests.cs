using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FactuTrust.API.Tests;

/// <summary>
/// Vérifie GET/PUT /api/platform/ai-settings incluant le moteur d'inférence Ollama.
/// </summary>
[Collection("PlatformBootstrapLogin")]
public sealed class PlatformAiSettingsIntegrationTests : IClassFixture<PlatformBootstrapWebApplicationFactory>
{
    private static readonly JsonSerializerOptions ApiJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly PlatformBootstrapWebApplicationFactory _factory;

    public PlatformAiSettingsIntegrationTests(PlatformBootstrapWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Platform_ai_settings_get_and_put_inference_device_roundtrip()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var token = await LoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var getResponse = await client.GetAsync("/api/platform/ai-settings");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var getBody = await getResponse.Content.ReadFromJsonAsync<ApiResponse<PlatformAiSettingsDto>>(ApiJsonOptions);
        Assert.NotNull(getBody);
        Assert.True(getBody!.Success);
        Assert.NotNull(getBody.Data);
        Assert.Equal(OllamaInferenceDevice.Gpu, getBody.Data!.InferenceDevice);

        var putResponse = await client.PutAsJsonAsync(
            "/api/platform/ai-settings",
            new UpdatePlatformAiSettingsRequest { InferenceDevice = OllamaInferenceDevice.CpuOnly });
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        var putBody = await putResponse.Content.ReadFromJsonAsync<ApiResponse<PlatformAiSettingsDto>>(ApiJsonOptions);
        Assert.NotNull(putBody);
        Assert.True(putBody!.Success);
        Assert.Equal(OllamaInferenceDevice.CpuOnly, putBody.Data!.InferenceDevice);

        // Restaurer le défaut pour ne pas impacter les autres tests / dev local.
        var restoreResponse = await client.PutAsJsonAsync(
            "/api/platform/ai-settings",
            new UpdatePlatformAiSettingsRequest { InferenceDevice = OllamaInferenceDevice.Gpu });
        Assert.Equal(HttpStatusCode.OK, restoreResponse.StatusCode);
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