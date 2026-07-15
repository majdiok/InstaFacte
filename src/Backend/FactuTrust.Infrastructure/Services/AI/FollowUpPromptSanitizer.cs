using System.Text.Json;
using FactuTrust.Application.Features.AI.DTOs;

namespace FactuTrust.Infrastructure.Services.AI;

/// <summary>Validates LLM-proposed follow-up question chips for the chat client.</summary>
public static class FollowUpPromptSanitizer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private const int MaxPrompts = 5;
    private const int MaxPromptLength = 200;

    /// <summary>Returns JSON array string of prompts, or error.</summary>
    public static AiToolResult SanitizePromptsJson(string? promptsJson)
    {
        if (string.IsNullOrWhiteSpace(promptsJson))
            return AiToolResult.Error("prompts_json vide.");

        List<string> output;
        try
        {
            using var doc = JsonDocument.Parse(promptsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return AiToolResult.Error("prompts_json doit être un tableau JSON.");

            var arr = doc.RootElement;
            if (arr.GetArrayLength() > MaxPrompts)
                return AiToolResult.Error($"Maximum {MaxPrompts} suggestions.");

            output = new List<string>();
            var i = 0;
            foreach (var el in arr.EnumerateArray())
            {
                i++;
                if (el.ValueKind != JsonValueKind.String)
                    return AiToolResult.Error($"Suggestion #{i} : chaîne attendue.");
                var s = el.GetString()?.Trim() ?? "";
                if (s.Length == 0 || s.Length > MaxPromptLength)
                    return AiToolResult.Error($"Suggestion #{i} : longueur invalide (1–{MaxPromptLength}).");
                if (s.Contains('<', StringComparison.Ordinal) || s.Contains('>', StringComparison.Ordinal))
                    return AiToolResult.Error($"Suggestion #{i} : caractères non autorisés.");
                if (s.Contains('\\', StringComparison.Ordinal))
                    return AiToolResult.Error($"Suggestion #{i} : caractères non autorisés.");
                if (s.Any(static c => char.IsControl(c)))
                    return AiToolResult.Error($"Suggestion #{i} : caractères non autorisés.");
                output.Add(s);
            }

            if (output.Count == 0)
                return AiToolResult.Error("Aucune suggestion valide.");
        }
        catch (JsonException ex)
        {
            return AiToolResult.Error($"JSON invalide : {ex.Message}");
        }

        return AiToolResult.Ok(JsonSerializer.Serialize(output, JsonOptions));
    }
}
