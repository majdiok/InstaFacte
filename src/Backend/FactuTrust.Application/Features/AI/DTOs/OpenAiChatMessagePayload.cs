using System.Text.Json.Serialization;

namespace FactuTrust.Application.Features.AI.DTOs;

/// <summary>Subset of OpenAI chat message for /v1/chat/completions.</summary>
public sealed class OpenAiChatMessagePayload
{
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    /// <summary>
    /// Contenu du message. Peut être :
    /// - <c>string</c> (cas mono-textuel, comportement historique)
    /// - <c>List&lt;OpenAiContentPart&gt;</c> (cas multimodal : texte + image_url)
    /// System.Text.Json sérialise selon le type runtime.
    /// </summary>
    [JsonPropertyName("content")]
    public object? Content { get; init; }

    [JsonPropertyName("tool_calls")]
    public List<OpenAiToolCallPayload>? ToolCalls { get; init; }

    [JsonPropertyName("tool_call_id")]
    public string? ToolCallId { get; init; }
}

public sealed class OpenAiContentPart
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }   // "text" | "image_url"

    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; init; }

    [JsonPropertyName("image_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OpenAiImageUrl? ImageUrl { get; init; }
}

public sealed class OpenAiImageUrl
{
    [JsonPropertyName("url")]
    public required string Url { get; init; }
}

public sealed class OpenAiToolCallPayload
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; } = "function";

    [JsonPropertyName("function")]
    public required OpenAiToolFunctionPayload Function { get; init; }
}

public sealed class OpenAiToolFunctionPayload
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>JSON string of arguments object.</summary>
    [JsonPropertyName("arguments")]
    public required string Arguments { get; init; }
}

public sealed record OpenAiRemoteModelInfo(string Id, string? Name);
