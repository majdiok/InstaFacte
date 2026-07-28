using System.Text.Json;

namespace FactuTrust.Application.Features.Studio.Ai;

/// <summary>
/// Recovers a Studio generation tool call that the small CPU model (qwen2.5:3b) emitted as TEXT
/// inside <c>message.content</c> instead of a structured <c>tool_calls</c> entry. Pure / dependency-free
/// so it can be unit-tested. Used only in the focused StudioBuilder surface (see SendChatMessageCommand).
/// </summary>
public static class StudioTextToolCallRecovery
{
    private static readonly HashSet<string> RecoverableTools = new(StringComparer.Ordinal)
    {
        "studio_generate_system",
        "studio_generate_app",
        "studio_plan_system",
        "studio_plan_app"
    };

    /// <summary>
    /// Scans <paramref name="content"/> for a tool-call envelope whose <c>name</c> is a Studio generation
    /// tool and extracts its <c>spec_json</c> argument. Handles a bare envelope, ```json fences, the
    /// <c>arguments</c> as an object (<c>{ "spec_json": "…" }</c>) or as a JSON string (OpenAI shape),
    /// and the OpenAI <c>{ "type":"function", "function": { … } }</c> wrapper.
    /// </summary>
    public static bool TryExtract(string? content, out string toolName, out string specJson)
    {
        toolName = string.Empty;
        specJson = string.Empty;
        if (string.IsNullOrWhiteSpace(content))
            return false;

        foreach (var candidate in EnumerateJsonObjects(content))
        {
            JsonDocument doc;
            try { doc = JsonDocument.Parse(candidate); }
            catch (JsonException) { continue; }

            using (doc)
            {
                if (TryReadCall(doc.RootElement, out toolName, out specJson))
                    return true;
            }
        }

        toolName = string.Empty;
        specJson = string.Empty;
        return false;
    }

    private static bool TryReadCall(JsonElement root, out string toolName, out string specJson)
    {
        toolName = string.Empty;
        specJson = string.Empty;
        if (root.ValueKind != JsonValueKind.Object)
            return false;

        // OpenAI wrapper: { "type": "function", "function": { "name": …, "arguments": … } }
        var nameEl = root;
        var argsHost = root;
        if (root.TryGetProperty("function", out var fn) && fn.ValueKind == JsonValueKind.Object)
        {
            nameEl = fn;
            argsHost = fn;
        }

        if (!nameEl.TryGetProperty("name", out var nameProp)
            || nameProp.ValueKind != JsonValueKind.String)
            return false;

        var name = nameProp.GetString();
        if (string.IsNullOrEmpty(name) || !RecoverableTools.Contains(name))
            return false;

        if (!argsHost.TryGetProperty("arguments", out var argsEl))
            return false;

        // arguments can be an object, or a JSON string holding the object (OpenAI streaming shape).
        if (argsEl.ValueKind == JsonValueKind.String)
        {
            var inner = argsEl.GetString();
            if (string.IsNullOrWhiteSpace(inner))
                return false;
            try
            {
                using var argsDoc = JsonDocument.Parse(inner);
                if (!TryReadSpec(argsDoc.RootElement, out specJson))
                    return false;
            }
            catch (JsonException) { return false; }
        }
        else if (argsEl.ValueKind == JsonValueKind.Object)
        {
            if (!TryReadSpec(argsEl, out specJson))
                return false;
        }
        else
        {
            return false;
        }

        toolName = name;
        return true;
    }

    private static bool TryReadSpec(JsonElement args, out string specJson)
    {
        specJson = string.Empty;
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty("spec_json", out var spec))
            return false;

        // spec_json is normally a stringified JSON; tolerate the model inlining it as an object.
        if (spec.ValueKind == JsonValueKind.String)
        {
            var s = spec.GetString();
            if (string.IsNullOrWhiteSpace(s))
                return false;
            specJson = s;
            return true;
        }
        if (spec.ValueKind == JsonValueKind.Object)
        {
            specJson = spec.GetRawText();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Yields each top-level <c>{ … }</c> substring (brace-balanced, string/escape aware) so callers can
    /// try-parse them. Captures the full envelope even when its nested <c>spec_json</c> string contains
    /// escaped braces. ```json fences and surrounding prose are simply ignored.
    /// </summary>
    private static IEnumerable<string> EnumerateJsonObjects(string content)
    {
        var depth = 0;
        var start = -1;
        var inString = false;
        var escaped = false;

        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];

            if (inString)
            {
                if (escaped) escaped = false;
                else if (c == '\\') escaped = true;
                else if (c == '"') inString = false;
                continue;
            }

            switch (c)
            {
                case '"':
                    inString = true;
                    break;
                case '{':
                    if (depth == 0) start = i;
                    depth++;
                    break;
                case '}':
                    if (depth > 0)
                    {
                        depth--;
                        if (depth == 0 && start >= 0)
                        {
                            yield return content.Substring(start, i - start + 1);
                            start = -1;
                        }
                    }
                    break;
            }
        }
    }
}
