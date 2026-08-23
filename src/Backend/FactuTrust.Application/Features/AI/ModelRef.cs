namespace FactuTrust.Application.Features.AI;

/// <summary>
/// Canonical model identifiers: <c>ollama:modelname</c>, <c>openrouter:vendor/model</c>,
/// <c>modal:vendor/model</c>, or <c>cursor:id</c> / <c>cursor:id|param=value</c>.
/// Legacy values without prefix are treated as Ollama.
/// </summary>
public static class ModelRef
{
    public const string OllamaPrefix = "ollama:";
    public const string OpenRouterPrefix = "openrouter:";
    public const string ModalPrefix = "modal:";
    public const string CursorPrefix = "cursor:";

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

        if (s.StartsWith(ModalPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var id = s[ModalPrefix.Length..].Trim();
            return new ParsedModelRef(LlmProviderKind.Modal, id, s);
        }

        if (s.StartsWith(CursorPrefix, StringComparison.OrdinalIgnoreCase))
            return ParseCursor(s[CursorPrefix.Length..].Trim());

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

    public static string FormatCursor(string modelId, IReadOnlyList<CursorModelParam>? parameters = null)
    {
        var id = (modelId ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(id))
            return CursorPrefix.TrimEnd(':');

        var canonicalParams = NormalizeParams(parameters);
        if (canonicalParams.Count == 0)
            return $"{CursorPrefix}{id}";

        var encoded = string.Join(",", canonicalParams.Select(p => $"{p.Id}={p.Value}"));
        return $"{CursorPrefix}{id}|{encoded}";
    }

    private static ParsedModelRef ParseCursor(string remainder)
    {
        if (string.IsNullOrEmpty(remainder))
            return new ParsedModelRef(LlmProviderKind.Cursor, string.Empty, CursorPrefix.TrimEnd(':'));

        string id;
        IReadOnlyList<CursorModelParam> parameters;
        var pipe = remainder.IndexOf('|');
        if (pipe < 0)
        {
            id = remainder.Trim();
            parameters = Array.Empty<CursorModelParam>();
        }
        else
        {
            id = remainder[..pipe].Trim();
            parameters = ParseParams(remainder[(pipe + 1)..]);
        }

        var canonical = FormatCursor(id, parameters);
        return new ParsedModelRef(LlmProviderKind.Cursor, id, canonical) { Params = parameters };
    }

    private static IReadOnlyList<CursorModelParam> ParseParams(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<CursorModelParam>();

        var list = new List<CursorModelParam>();
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0)
                continue;
            var paramId = part[..eq].Trim();
            var value = part[(eq + 1)..].Trim();
            if (paramId.Length == 0)
                continue;
            list.Add(new CursorModelParam(paramId, value));
        }

        return NormalizeParams(list);
    }

    private static IReadOnlyList<CursorModelParam> NormalizeParams(IReadOnlyList<CursorModelParam>? parameters)
    {
        if (parameters is null || parameters.Count == 0)
            return Array.Empty<CursorModelParam>();

        return parameters
            .Where(p => !string.IsNullOrWhiteSpace(p.Id))
            .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Last())
            .OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

public enum LlmProviderKind
{
    Ollama = 0,
    OpenRouter = 1,
    Cursor = 2,
    Modal = 3
}

public readonly record struct CursorModelParam(string Id, string Value);

/// <param name="Kind">Backend to use.</param>
/// <param name="ProviderModelId">Ollama model name, OpenRouter/Modal model id, or Cursor model id.</param>
/// <param name="CanonicalModelRef">Stable value stored on conversations (e.g. ollama:qwen2.5:latest).</param>
public readonly record struct ParsedModelRef(LlmProviderKind Kind, string ProviderModelId, string CanonicalModelRef)
{
    public IReadOnlyList<CursorModelParam> Params { get; init; } = Array.Empty<CursorModelParam>();
}

/// <summary>Règles de sélection Cursor (Router <c>auto-smart</c>, etc.). Le parse reste permissif.</summary>
public static class CursorModelSelection
{
    public static readonly string[] AllowedOptimizeFor = ["cost", "balanced", "intelligence"];

    public static bool TryValidate(ParsedModelRef parsed, out string? error)
    {
        error = null;
        if (parsed.Kind != LlmProviderKind.Cursor)
            return true;

        if (string.IsNullOrWhiteSpace(parsed.ProviderModelId))
        {
            error = "Référence de modèle Cursor invalide.";
            return false;
        }

        if (string.Equals(parsed.ProviderModelId, "auto-smart", StringComparison.OrdinalIgnoreCase))
        {
            var optimize = parsed.Params.FirstOrDefault(p =>
                string.Equals(p.Id, "optimize_for", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(optimize.Value)
                || !AllowedOptimizeFor.Contains(optimize.Value, StringComparer.OrdinalIgnoreCase))
            {
                error = "Cursor Router (auto-smart) exige le paramètre optimize_for = cost, balanced ou intelligence.";
                return false;
            }
        }

        return true;
    }
}
