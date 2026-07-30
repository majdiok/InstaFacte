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
            null);

        var json = JsonSerializer.Serialize(dto, ApiJsonOptions);

        Assert.Contains("\"inferenceDevice\":\"CpuOnly\"", json);
        Assert.Contains("\"studioAiModelRef\":null", json);
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
              "recommendation": null
            }
            """;

        var dto = JsonSerializer.Deserialize<PlatformAiSettingsDto>(json, ApiJsonOptions);

        Assert.NotNull(dto);
        Assert.Equal(OllamaInferenceDevice.Gpu, dto!.InferenceDevice);
        Assert.Equal("ollama:qwen2.5:7b-instruct", dto.StudioAiModelRef);
    }
}
