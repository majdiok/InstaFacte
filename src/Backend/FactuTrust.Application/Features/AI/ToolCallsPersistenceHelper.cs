using System.Text.Json;
using FactuTrust.Application.Features.AI.DTOs;

namespace FactuTrust.Application.Features.AI;

/// <summary>
/// Persists assistant tool rounds for replay (Ollama + OpenAI-compatible history).
/// </summary>
public static class ToolCallsPersistenceHelper
{
    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed record StoredCall(string Id, string Name, string ArgumentsJson);

    public static string Serialize(IReadOnlyList<OllamaToolCall> calls)
    {
        var list = calls.Select((c, i) => new StoredCall(
            !string.IsNullOrEmpty(c.Id) ? c.Id! : $"gen_{i}",
            c.Function?.Name ?? "",
            JsonSerializer.Serialize(c.Function?.Arguments ?? new Dictionary<string, object?>()))).ToList();
        return JsonSerializer.Serialize(list);
    }

    public static List<OllamaToolCall> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<OllamaToolCall>();

        try
        {
            var items = JsonSerializer.Deserialize<List<StoredCall>>(json, DeserializeOptions);
            if (items is null)
                return new List<OllamaToolCall>();

            var result = new List<OllamaToolCall>();
            foreach (var item in items)
            {
                Dictionary<string, object?> args = new();
                try
                {
                    using var doc = JsonDocument.Parse(string.IsNullOrEmpty(item.ArgumentsJson) ? "{}" : item.ArgumentsJson);
                    foreach (var prop in doc.RootElement.EnumerateObject())
                        args[prop.Name] = JsonElementToObject(prop.Value);
                }
                catch
                {
                    args = new Dictionary<string, object?>();
                }

                result.Add(new OllamaToolCall
                {
                    Id = item.Id,
                    Function = new OllamaToolCallFunction
                    {
                        Name = item.Name,
                        Arguments = args
                    }
                });
            }

            return result;
        }
        catch
        {
            return new List<OllamaToolCall>();
        }
    }

    private static object? JsonElementToObject(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => el.GetRawText()
        };
    }
}
