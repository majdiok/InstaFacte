using System.Text.Json;
using System.Text.Json.Serialization;
using FactuTrust.Application.Features.AI.Commands;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.API.Tests.Studio;

/// <summary>
/// PR 1.2 — contrat HTTP de <c>POST /api/ai/chat</c> pour l'atelier Studio :
/// <c>options.useAdvancedModel</c> et <c>options.studioIntent</c> sont lus tels que l'Angular les envoie
/// (camelCase, options JSON de l'API) ; un corps « legacy » sans ces champs reste valide et donne
/// <c>false</c> / <c>null</c>. Côté flux SSE, l'événement <c>meta</c> transporte
/// <c>{ usedAdvancedModel, advancedModelFallbackReason, model }</c> dans <c>content</c>.
/// </summary>
public sealed class AiChatOptionsContractTests
{
    /// <summary>Mêmes options que <c>AddControllers().AddJsonOptions(...)</c> dans Program.cs (+ défauts Web d'ASP.NET Core).</summary>
    private static readonly JsonSerializerOptions ApiJson = BuildApiJsonOptions();

    private static JsonSerializerOptions BuildApiJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    [Fact]
    public void Studio_body_deserializes_useAdvancedModel_and_studioIntent()
    {
        const string body = """
            {
              "message": "Crée un système de gestion des congés",
              "options": {
                "assistantMode": "StudioBuilder",
                "useAdvancedModel": true,
                "studioIntent": "system"
              }
            }
            """;

        var request = JsonSerializer.Deserialize<AiChatHttpRequestDto>(body, ApiJson);

        Assert.NotNull(request);
        Assert.NotNull(request!.Options);
        Assert.Equal(AssistantMode.StudioBuilder, request.Options!.AssistantMode);
        Assert.True(request.Options.UseAdvancedModel);
        Assert.Equal("system", request.Options.StudioIntent);
    }

    [Fact]
    public void Legacy_body_without_the_new_fields_keeps_defaults_false_and_null()
    {
        const string body = """
            {
              "conversationId": null,
              "message": "Quel est le chiffre d'affaires d'avril ?",
              "options": { "assistantMode": "Default", "conversationalFollowUp": false }
            }
            """;

        var request = JsonSerializer.Deserialize<AiChatHttpRequestDto>(body, ApiJson);

        Assert.NotNull(request?.Options);
        Assert.False(request!.Options!.UseAdvancedModel);
        Assert.Null(request.Options.StudioIntent);
        Assert.Equal(AssistantMode.Default, request.Options.AssistantMode);
    }

    [Fact]
    public void Body_without_options_at_all_is_still_valid()
    {
        var request = JsonSerializer.Deserialize<AiChatHttpRequestDto>("""{ "message": "Bonjour" }""", ApiJson);

        Assert.NotNull(request);
        Assert.Null(request!.Options);
        // Le handler lit `command.Options?.UseAdvancedModel == true` : null ⇒ jamais « demandé ».
        var command = new SendChatMessageCommand(request.ConversationId, request.Message, request.Model, request.UiContext, request.Options, request.Attachments);
        Assert.Null(command.Options);
    }

    [Fact]
    public void Unknown_intent_is_carried_verbatim_the_backend_ignores_it_when_building_the_prompt()
    {
        const string body = """{ "message": "x", "options": { "studioIntent": "DROP TABLE" } }""";

        var request = JsonSerializer.Deserialize<AiChatHttpRequestDto>(body, ApiJson);

        Assert.Equal("DROP TABLE", request!.Options!.StudioIntent);
        var promptOptions = new StudioPromptOptions(false, request.Options.StudioIntent, Guid.NewGuid(), Guid.NewGuid().ToString());
        Assert.Null(promptOptions.NormalizedIntent);
    }

    [Fact]
    public void Default_ChatRequestOptionsDto_matches_legacy_behaviour()
    {
        var options = new ChatRequestOptionsDto();

        Assert.False(options.UseAdvancedModel);
        Assert.Null(options.StudioIntent);
    }

    [Fact]
    public void Sse_meta_event_serializes_with_type_meta_and_json_content()
    {
        var sseOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var ev = ChatStreamEvent.StudioMetaEvent(
            SendChatMessageHandler.BuildStudioMetaJson(false, SendChatMessageHandler.StudioAdvancedFallbackUnavailable,
                FactuTrust.Application.Features.AI.ModelRef.Parse("ollama:qwen2.5-coder:7b")));

        var frame = JsonSerializer.Serialize(ev, sseOptions);

        using var doc = JsonDocument.Parse(frame);
        Assert.Equal("meta", doc.RootElement.GetProperty("type").GetString());
        using var meta = JsonDocument.Parse(doc.RootElement.GetProperty("content").GetString()!);
        Assert.False(meta.RootElement.GetProperty("usedAdvancedModel").GetBoolean());
        Assert.Equal("unavailable", meta.RootElement.GetProperty("advancedModelFallbackReason").GetString());
        Assert.Equal("qwen2.5-coder:7b", meta.RootElement.GetProperty("model").GetString());
    }

    [Theory]
    [InlineData(SendChatMessageHandler.StudioAdvancedFallbackDisabled, "disabled")]
    [InlineData(SendChatMessageHandler.StudioAdvancedFallbackNotConfigured, "not_configured")]
    [InlineData(SendChatMessageHandler.StudioAdvancedFallbackUnavailable, "unavailable")]
    public void Fallback_reason_codes_are_frozen(string constant, string expected) =>
        Assert.Equal(expected, constant);
}
