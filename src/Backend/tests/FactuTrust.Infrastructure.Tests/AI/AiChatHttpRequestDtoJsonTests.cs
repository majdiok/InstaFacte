using System.Text.Json;
using FactuTrust.Application.Features.AI.DTOs;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class AiChatHttpRequestDtoJsonTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void Deserialize_minimal_body_succeeds()
    {
        const string json = """{"message":"hello"}""";
        var dto = JsonSerializer.Deserialize<AiChatHttpRequestDto>(json, Options);
        Assert.NotNull(dto);
        Assert.Equal("hello", dto.Message);
        Assert.Null(dto.UiContext);
        Assert.Null(dto.Options);
    }

    [Fact]
    public void Deserialize_extended_body_succeeds()
    {
        const string json = """
            {
              "conversationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
              "message": "test",
              "model": "ollama/mistral",
              "uiContext": { "route": "/invoices/abc", "screenId": "invoice-detail" },
              "options": { "assistantMode": 1 }
            }
            """;
        var dto = JsonSerializer.Deserialize<AiChatHttpRequestDto>(json, Options);
        Assert.NotNull(dto);
        Assert.Equal("test", dto.Message);
        Assert.Equal("/invoices/abc", dto.UiContext?.Route);
        Assert.Equal("invoice-detail", dto.UiContext?.ScreenId);
        Assert.Equal(AssistantMode.Compliance, dto.Options?.AssistantMode);
    }

    [Fact]
    public void Deserialize_uiContext_with_analysisSummary_succeeds()
    {
        const string json = """
            {
              "message": "analyse",
              "uiContext": {
                "route": "/invoices",
                "screenId": "invoice-list",
                "analysisSummary": "{\"schemaVersion\":\"1\",\"payload\":{}}"
              }
            }
            """;
        var dto = JsonSerializer.Deserialize<AiChatHttpRequestDto>(json, Options);
        Assert.NotNull(dto);
        Assert.Equal("/invoices", dto.UiContext?.Route);
        Assert.Equal("invoice-list", dto.UiContext?.ScreenId);
        Assert.Contains("schemaVersion", dto.UiContext?.AnalysisSummary ?? "");
    }
}
