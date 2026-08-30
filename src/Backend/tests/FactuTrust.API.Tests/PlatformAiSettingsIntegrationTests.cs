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
[Collection("SqlServerIntegration")]
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
        Assert.NotNull(getBody.Data!.Cursor);

        if (getBody.Data.InferenceDevice != OllamaInferenceDevice.Gpu)
        {
            var reset = await client.PutAsJsonAsync(
                "/api/platform/ai-settings",
                new UpdatePlatformAiSettingsRequest { InferenceDevice = OllamaInferenceDevice.Gpu });
            Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        }

        getResponse = await client.GetAsync("/api/platform/ai-settings");
        getBody = await getResponse.Content.ReadFromJsonAsync<ApiResponse<PlatformAiSettingsDto>>(ApiJsonOptions);
        Assert.NotNull(getBody?.Data);
        Assert.Equal(OllamaInferenceDevice.Gpu, getBody!.Data!.InferenceDevice);

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

    [Fact]
    public async Task Platform_ai_settings_get_and_put_studio_model_roundtrip()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var token = await LoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var getBefore = await client.GetAsync("/api/platform/ai-settings");
        Assert.Equal(HttpStatusCode.OK, getBefore.StatusCode);
        var beforeBody = await getBefore.Content.ReadFromJsonAsync<ApiResponse<PlatformAiSettingsDto>>(ApiJsonOptions);
        Assert.NotNull(beforeBody?.Data);
        var previousStudio = beforeBody!.Data!.StudioAiModelRef;

        const string studioRef = "ollama:qwen2.5:7b-instruct";
        var putResponse = await client.PutAsJsonAsync(
            "/api/platform/ai-settings",
            new UpdatePlatformAiSettingsRequest { StudioAiModelRef = studioRef });
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        var putBody = await putResponse.Content.ReadFromJsonAsync<ApiResponse<PlatformAiSettingsDto>>(ApiJsonOptions);
        Assert.NotNull(putBody);
        Assert.True(putBody!.Success);
        Assert.Equal(studioRef, putBody.Data!.StudioAiModelRef);

        var getAfter = await client.GetAsync("/api/platform/ai-settings");
        var afterBody = await getAfter.Content.ReadFromJsonAsync<ApiResponse<PlatformAiSettingsDto>>(ApiJsonOptions);
        Assert.Equal(studioRef, afterBody!.Data!.StudioAiModelRef);

        var clearResponse = await client.PutAsJsonAsync(
            "/api/platform/ai-settings",
            new UpdatePlatformAiSettingsRequest { StudioAiModelRef = "" });
        Assert.Equal(HttpStatusCode.OK, clearResponse.StatusCode);
        var clearBody = await clearResponse.Content.ReadFromJsonAsync<ApiResponse<PlatformAiSettingsDto>>(ApiJsonOptions);
        Assert.Null(clearBody!.Data!.StudioAiModelRef);

        // Restaurer la valeur précédente si le test a écrasé une config locale.
        if (!string.IsNullOrWhiteSpace(previousStudio))
        {
            await client.PutAsJsonAsync(
                "/api/platform/ai-settings",
                new UpdatePlatformAiSettingsRequest { StudioAiModelRef = previousStudio });
        }
    }

    [Fact]
    public async Task Platform_ai_settings_get_and_put_openrouter_roundtrip_keeps_secret_on_empty_key()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var token = await LoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var previous = await client.GetAsync("/api/platform/ai-settings");
        Assert.Equal(HttpStatusCode.OK, previous.StatusCode);
        var previousBody = await previous.Content.ReadFromJsonAsync<ApiResponse<PlatformAiSettingsDto>>(ApiJsonOptions);
        Assert.NotNull(previousBody?.Data?.OpenRouter);
        var previousOpenRouter = previousBody!.Data!.OpenRouter;

        var putResponse = await client.PutAsJsonAsync(
            "/api/platform/ai-settings",
            new UpdatePlatformAiSettingsRequest
            {
                OpenRouter = new UpdatePlatformOpenRouterRequest
                {
                    IsEnabled = true,
                    DisplayName = "OpenRouter Test",
                    BaseUrl = null,
                    ApiKey = "sk-or-test-key-abcd"
                }
            });
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        var putBody = await putResponse.Content.ReadFromJsonAsync<ApiResponse<PlatformAiSettingsDto>>(ApiJsonOptions);
        Assert.NotNull(putBody?.Data?.OpenRouter);
        Assert.True(putBody!.Data!.OpenRouter.IsEnabled);
        Assert.True(putBody.Data.OpenRouter.IsApiKeyConfigured);
        Assert.Equal("abcd", putBody.Data.OpenRouter.ApiKeyLast4);
        var putJson = await putResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("sk-or-test-key-abcd", putJson);

        var keepResponse = await client.PutAsJsonAsync(
            "/api/platform/ai-settings",
            new UpdatePlatformAiSettingsRequest
            {
                OpenRouter = new UpdatePlatformOpenRouterRequest
                {
                    IsEnabled = true,
                    DisplayName = "OpenRouter Kept",
                    ApiKey = null
                }
            });
        Assert.Equal(HttpStatusCode.OK, keepResponse.StatusCode);
        var keepBody = await keepResponse.Content.ReadFromJsonAsync<ApiResponse<PlatformAiSettingsDto>>(ApiJsonOptions);
        Assert.Equal("abcd", keepBody!.Data!.OpenRouter.ApiKeyLast4);
        Assert.Equal("OpenRouter Kept", keepBody.Data.OpenRouter.DisplayName);

        // Restaurer l'état précédent (désactivation si pas de clé précédente).
        await client.PutAsJsonAsync(
            "/api/platform/ai-settings",
            new UpdatePlatformAiSettingsRequest
            {
                OpenRouter = new UpdatePlatformOpenRouterRequest
                {
                    IsEnabled = previousOpenRouter.IsEnabled,
                    DisplayName = previousOpenRouter.DisplayName,
                    BaseUrl = previousOpenRouter.BaseUrl,
                    ApiKey = null
                }
            });
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