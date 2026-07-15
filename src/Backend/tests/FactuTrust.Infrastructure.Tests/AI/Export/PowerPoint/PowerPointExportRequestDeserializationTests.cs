using System.Text.Json;
using System.Text.Json.Serialization;
using FactuTrust.Application.Features.AI.Export.PowerPoint.DTOs;
using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI.Export.PowerPoint;

public sealed class PowerPointExportRequestDeserializationTests
{
    private static readonly JsonSerializerOptions ApiJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void Deserialize_accepts_pascal_case_string_enums_from_frontend()
    {
        var conversationId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var json = $$"""
            {
              "title": "Synthèse",
              "template": "Vortex",
              "orientation": "Widescreen16x9",
              "includeCoverSlide": true,
              "includeAgenda": true,
              "includeSpeakerNotes": true,
              "includeSources": true,
              "responses": [
                {
                  "conversationId": "{{conversationId}}",
                  "messageId": "{{messageId}}",
                  "includeOnly": "Text, KpiCards"
                }
              ]
            }
            """;

        var dto = JsonSerializer.Deserialize<PowerPointExportRequestDto>(json, ApiJsonOptions);

        Assert.NotNull(dto);
        Assert.Equal(PowerPointTemplate.Vortex, dto!.Template);
        Assert.Equal(SlideOrientation.Widescreen16x9, dto.Orientation);
        Assert.Equal(SlideContentBlock.Text | SlideContentBlock.KpiCards, dto.Responses[0].IncludeOnly);
    }

    [Fact]
    public void Deserialize_accepts_numeric_enums_as_fallback()
    {
        var json = """
            {
              "title": "Synthèse",
              "template": 10,
              "orientation": 0,
              "responses": []
            }
            """;

        var dto = JsonSerializer.Deserialize<PowerPointExportRequestDto>(json, ApiJsonOptions);

        Assert.NotNull(dto);
        Assert.Equal(PowerPointTemplate.Vortex, dto!.Template);
        Assert.Equal(SlideOrientation.Widescreen16x9, dto.Orientation);
    }
}
