namespace FactuTrust.Application.Features.AI;

/// <summary>
/// Canonical model identifiers: <c>ollama:modelname</c> or <c>openrouter:vendor/model</c>.
/// Legacy values without prefix are treated as Ollama.
/// </summary>
public static class ModelRef
{
    public const string OllamaPrefix = "ollama:";
    public const string OpenRouterPrefix = "openrouter:";

    public static ParsedModelRef Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new ParsedModelRef(LlmProviderKind.Ollama, string.Empty, string.Empty);

        var s = raw.Trim();
        if (s.StartsWith(OpenRouterPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var id = s[OpenRouterPrefix.Length..].Trim();
            return new ParsedModelRef(LlmProviderKind.OpenRouter, id, s);
        }

        if (s.StartsWith(OllamaPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var name = s[OllamaPrefix.Length..].Trim();
            return new ParsedModelRef(LlmProviderKind.Ollama, name, string.IsNullOrEmpty(name) ? s : $"{OllamaPrefix}{name}");
        }

        return new ParsedModelRef(LlmProviderKind.Ollama, s, $"{OllamaPrefix}{s}");
    }

    /// <summary>Normalize stored model string to canonical form when missing prefix.</summary>
    public static string NormalizeStored(string? raw)
    {
        var p = Parse(raw);
        return p.CanonicalModelRef;
    }
}

public enum LlmProviderKind
{
    Ollama = 0,
    OpenRouter = 1
}

/// <param name="Kind">Backend to use.</param>
/// <param name="ProviderModelId">Ollama model name or OpenRouter model id (e.g. anthropic/claude-3.5-sonnet).</param>
/// <param name="CanonicalModelRef">Stable value stored on conversations (e.g. ollama:qwen2.5:latest).</param>
public readonly record struct ParsedModelRef(LlmProviderKind Kind, string ProviderModelId, string CanonicalModelRef);
