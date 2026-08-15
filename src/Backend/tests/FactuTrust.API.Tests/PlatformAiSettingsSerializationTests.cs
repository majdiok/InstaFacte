using System.Text.Json;
using System.Text.Json.Serialization;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.API.Tests;

public sealed class PlatformAiSettingsSerializationTests
{
    private static readonly JsonSerializerOptions ApiJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private static PlatformOpenRouterSettingsDto EmptyOpenRouter =>
        new(false, null, null, "https://openrouter.ai/api/v1", false, null);

    private static PlatformCursorSettingsDto EmptyCursor =>
        new(false, null, false, null);

    [Fact]
    public void InferenceDevice_serializes_as_pascal_case_string()
    {
        var dto = new PlatformAiSettingsDto(
            null,
            null,
            null,
            null,
            OllamaInferenceDevice.CpuOnly,
            true,
            Array.Empty<UnifiedAiModelInfo>(),
            null,
            EmptyOpenRouter,
            EmptyCursor);

        var json = JsonSerializer.Serialize(dto, ApiJsonOptions);

        Assert.Contains("\"inferenceDevice\":\"CpuOnly\"", json);
        Assert.Contains("\"studioAiModelRef\":null", json);
        Assert.Contains("\"openRouter\"", json);
        Assert.Contains("\"cursor\"", json);
        Assert.DoesNotContain("sk-", json);
        Assert.DoesNotContain("cursor_", json);
    }

    [Fact]
    public void InferenceDevice_deserializes_from_pascal_case_string()
    {
        const string json = """
            {
              "configuredModelRef": null,
              "invoiceImportModelRef": null,
              "studioAiModelRef": "ollama:qwen2.5:7b-instruct",
              "serverInvoiceImportVisionModel": null,
              "inferenceDevice": "Gpu",
              "isOllamaAssistantConfigured": true,
              "availableModels": [],
              "recommendation": null,
              "openRouter": {
                "isEnabled": true,
                "displayName": "OpenRouter",
                "baseUrl": null,
                "defaultBaseUrl": "https://openrouter.ai/api/v1",
                "isApiKeyConfigured": true,
                "apiKeyLast4": "ab12"
              }
            }
            """;

        var dto = JsonSerializer.Deserialize<PlatformAiSettingsDto>(json, ApiJsonOptions);

        Assert.NotNull(dto);
        Assert.Equal(OllamaInferenceDevice.Gpu, dto!.InferenceDevice);
        Assert.Equal("ollama:qwen2.5:7b-instruct", dto.StudioAiModelRef);
        Assert.True(dto.OpenRouter.IsEnabled);
        Assert.Equal("ab12", dto.OpenRouter.ApiKeyLast4);
    }

    [Fact]
    public void Cursor_deserializes_and_does_not_leak_api_key()
    {
        const string json = """
            {
              "configuredModelRef": "cursor:composer-2.5",
              "invoiceImportModelRef": null,
              "studioAiModelRef": null,
              "serverInvoiceImportVisionModel": null,
              "inferenceDevice": "Gpu",
              "isOllamaAssistantConfigured": false,
              "availableModels": [],
              "recommendation": null,
              "openRouter": {
                "isEnabled": false,
                "displayName": null,
                "baseUrl": null,
                "defaultBaseUrl": "https://openrouter.ai/api/v1",
                "isApiKeyConfigured": false,
                "apiKeyLast4": null
              },
              "cursor": {
                "isEnabled": true,
                "displayName": "Cursor",
                "isApiKeyConfigured": true,
                "apiKeyLast4": "xy89"
              }
            }
            """;

        var dto = JsonSerializer.Deserialize<PlatformAiSettingsDto>(json, ApiJsonOptions);

        Assert.NotNull(dto);
        Assert.NotNull(dto!.Cursor);
        Assert.True(dto.Cursor.IsEnabled);
        Assert.Equal("xy89", dto.Cursor.ApiKeyLast4);
        Assert.DoesNotContain("sk-", json);
        Assert.DoesNotContain("cursor_", json);
    }
}
