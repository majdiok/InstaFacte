using System.Text.Json;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.AI.Json;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

/// <summary>
/// Sortie structurée Ollama : le schéma doit contraindre les types qui ont réellement cassé
/// en production, et sa présence ne doit rien changer pour les appelants qui ne l'utilisent pas.
/// </summary>
public sealed class LlmOutputSchemaTests
{
    private static JsonElement Property(JsonElement root, params string[] path)
    {
        var current = root;
        foreach (var segment in path)
        {
            Assert.True(
                current.TryGetProperty(segment, out current),
                $"Segment « {segment} » introuvable dans le schéma.");
        }
        return current;
    }

    [Fact]
    public void AccountingSchema_ConstrainsVatRateToInteger()
    {
        var type = Property(
            LlmOutputSchemas.AccountingDocument,
            "properties", "lines", "items", "properties", "vatRatePercent", "type");

        var allowed = type.EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("integer", allowed);
        Assert.DoesNotContain("number", allowed);
    }

    [Fact]
    public void AccountingSchema_ConstrainsVatBreakdownRateToInteger()
    {
        var type = Property(
            LlmOutputSchemas.AccountingDocument,
            "properties", "vatBreakdown", "items", "properties", "ratePercent", "type");

        Assert.Contains("integer", type.EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public void AccountingSchema_ConstrainsConfidenceToTheThreeStrings()
    {
        var confidence = Property(LlmOutputSchemas.AccountingDocument, "properties", "confidence");
        var allowed = confidence.GetProperty("enum").EnumerateArray()
            .Select(e => e.ValueKind == JsonValueKind.Null ? null : e.GetString())
            .ToList();

        Assert.Contains("high", allowed);
        Assert.Contains("medium", allowed);
        Assert.Contains("low", allowed);
    }

    [Fact]
    public void AccountingSchema_ConstrainsWarningsToStrings()
    {
        var itemType = Property(
            LlmOutputSchemas.AccountingDocument, "properties", "warnings", "items", "type");

        Assert.Equal("string", itemType.GetString());
    }

    [Fact]
    public void AccountingSchema_KeepsAmountsAsNumbers()
    {
        var type = Property(LlmOutputSchemas.AccountingDocument, "properties", "totalTtc", "type");
        Assert.Contains("number", type.EnumerateArray().Select(e => e.GetString()));
    }

    /// <summary>
    /// Le passage de <c>Format</c> à <c>object?</c> ne doit rien changer pour les appelants qui
    /// envoient la chaîne « json » : même charge utile qu'avant.
    /// </summary>
    [Fact]
    public void PlainJsonFormat_SerializesExactlyAsBefore()
    {
        var request = new OllamaChatRequest
        {
            Model = "gemma3:4b",
            Messages = [],
            Format = LlmOutputSchemas.PlainJson
        };

        var json = JsonSerializer.Serialize(request);

        Assert.Contains("\"format\":\"json\"", json);
    }

    [Fact]
    public void SchemaFormat_SerializesAsAnObject()
    {
        var request = new OllamaChatRequest
        {
            Model = "gemma3:4b",
            Messages = [],
            Format = LlmOutputSchemas.AccountingDocument
        };

        var json = JsonSerializer.Serialize(request);

        Assert.Contains("\"format\":{", json);
        Assert.Contains("vatRatePercent", json);
    }

    [Fact]
    public void NullFormat_IsOmitted()
    {
        var request = new OllamaChatRequest { Model = "gemma3:4b", Messages = [], Format = null };

        Assert.DoesNotContain("format", JsonSerializer.Serialize(request));
    }
}
