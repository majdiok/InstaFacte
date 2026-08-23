using System.Text.Json;

namespace FactuTrust.Application.Features.Studio.Ai;

public enum BareStudioSpecKind
{
    App,
    System
}

/// <summary>
/// Recovers a Studio generation tool call that the small CPU model (qwen2.5:3b) emitted as TEXT
/// inside <c>message.content</c> instead of a structured <c>tool_calls</c> entry. Pure / dependency-free
/// so it can be unit-tested. Used only in the focused StudioBuilder surface (see SendChatMessageCommand).
/// </summary>
public static class StudioTextToolCallRecovery
{
    public const string ReformulateMessage =
        "Je n'ai pas pu lancer la création du système. Reformulez votre demande (ex. : « Crée un système de gestion de congés avec plusieurs tables liées »).";

    /// <summary>
    /// Message adapté à une demande d'ÉTAT : dire « création du système » à quelqu'un qui demande un
    /// rapport l'envoie reformuler dans la mauvaise direction.
    /// </summary>
    public const string ReformulateReportMessage =
        "Je n'ai pas pu préparer cet état. Précisez la période, ce que vous voulez mesurer et par quoi regrouper (ex. : « Ventes par produit du 1er janvier au 31 mars »).";

    private static readonly HashSet<string> RecoverableTools = new(StringComparer.Ordinal)
    {
        "studio_generate_system",
        "studio_generate_app",
        "studio_plan_system",
        "studio_plan_app",
        "studio_run_report",
        "studio_plan_report"
    };

    /// <summary>Choisit le message de reformulation d'après l'outil visé (ou le texte, à défaut).</summary>
    public static string ResolveReformulateMessage(string? toolName, string? content = null)
    {
        if (toolName is not null && toolName.Contains("report", StringComparison.OrdinalIgnoreCase))
            return ReformulateReportMessage;
        return LooksLikeReportSpecText(content) ? ReformulateReportMessage : ReformulateMessage;
    }

    /// <summary>Signature d'une spécification d'état : une source/préréglage ET une mesure ou un regroupement.</summary>
    public static bool LooksLikeReportSpecText(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        var hasSource = content.Contains("\"preset\"", StringComparison.Ordinal)
            || content.Contains("\"source\"", StringComparison.Ordinal);
        var hasShape = content.Contains("\"measures\"", StringComparison.Ordinal)
            || content.Contains("\"groupBy\"", StringComparison.Ordinal)
            || content.Contains("\"aggregations\"", StringComparison.Ordinal);
        return hasSource && hasShape;
    }

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

    /// <summary>
    /// Recovers a bare Studio spec dumped as JSON (no <c>studio_*</c> envelope). Requires a complete,
    /// parseable object with <c>entity</c>+<c>fields</c> (app) or <c>system</c>+<c>entities</c> (system).
    /// Does not repair truncated JSON.
    /// </summary>
    public static bool TryExtractBareStudioSpec(string? content, out BareStudioSpecKind kind, out string specJson)
    {
        kind = default;
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
                if (TryReadBareSpec(doc.RootElement, out kind, out specJson))
                    return true;
            }
        }

        kind = default;
        specJson = string.Empty;
        return false;
    }

    public static string ResolveToolName(BareStudioSpecKind kind, bool planPreview) =>
        kind switch
        {
            BareStudioSpecKind.System => planPreview ? "studio_plan_system" : "studio_generate_system",
            _ => planPreview ? "studio_plan_app" : "studio_generate_app"
        };

    /// <summary>
    /// True when the text looks like a Studio spec leak (quoted keys), including truncated JSON.
    ///
    /// Resserré : une clôture ```json seule ne suffit plus, et « entity » seul non plus. Une prose
    /// qui cite un bloc de code ou le mot « entité » n'est plus prise pour une spécification tronquée
    /// — c'est ce qui faisait apparaître le message de reformulation à tort.
    /// </summary>
    public static bool LooksLikeStudioSpecText(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        var hasEntity = content.Contains("\"entity\"", StringComparison.Ordinal);
        var hasFields = content.Contains("\"fields\"", StringComparison.Ordinal);
        var hasSystem = content.Contains("\"system\"", StringComparison.Ordinal);
        var hasEntities = content.Contains("\"entities\"", StringComparison.Ordinal);
        return (hasEntity && hasFields)
            || (hasSystem && hasEntities)
            || LooksLikeReportSpecText(content);
    }

    /// <summary>
    /// Removes brace-balanced bare Studio specs and any unbalanced tail that still looks like one.
    /// </summary>
    public static string StripBareStudioJson(string content)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        var result = content;
        foreach (var candidate in EnumerateJsonObjects(content).Reverse())
        {
            if (!IsBareStudioSpecJson(candidate))
                continue;
            var idx = result.LastIndexOf(candidate, StringComparison.Ordinal);
            if (idx >= 0)
                result = result.Remove(idx, candidate.Length);
        }

        return StripUnbalancedStudioJsonTail(result);
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

    private static bool TryReadBareSpec(JsonElement root, out BareStudioSpecKind kind, out string specJson)
    {
        kind = default;
        specJson = string.Empty;
        if (root.ValueKind != JsonValueKind.Object)
            return false;

        if (root.TryGetProperty("name", out _) && root.TryGetProperty("arguments", out _))
            return false;
        if (root.TryGetProperty("function", out var fn) && fn.ValueKind == JsonValueKind.Object)
            return false;

        var hasEntity = root.TryGetProperty("entity", out var entity) && entity.ValueKind == JsonValueKind.Object;
        var hasFields = root.TryGetProperty("fields", out var fields)
            && fields.ValueKind == JsonValueKind.Array
            && fields.GetArrayLength() > 0;
        if (hasEntity && hasFields)
        {
            kind = BareStudioSpecKind.App;
            specJson = root.GetRawText();
            return true;
        }

        var hasSystem = root.TryGetProperty("system", out var system) && system.ValueKind == JsonValueKind.Object;
        var hasEntities = root.TryGetProperty("entities", out var entities)
            && entities.ValueKind == JsonValueKind.Array
            && entities.GetArrayLength() > 0;
        if (hasSystem && hasEntities)
        {
            kind = BareStudioSpecKind.System;
            specJson = root.GetRawText();
            return true;
        }

        return false;
    }

    private static bool IsBareStudioSpecJson(string candidate)
    {
        try
        {
            using var doc = JsonDocument.Parse(candidate);
            return TryReadBareSpec(doc.RootElement, out _, out _);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string StripUnbalancedStudioJsonTail(string content)
    {
        var start = content.IndexOf('{');
        while (start >= 0)
        {
            var slice = content[start..];
            if (LooksLikeStudioSpecText(slice) && !IsBalancedObject(slice))
                return content[..start].TrimEnd();
            start = content.IndexOf('{', start + 1);
        }

        return content;
    }

    private static bool IsBalancedObject(string text)
    {
        var depth = 0;
        var inString = false;
        var escaped = false;
        var sawOpen = false;
        foreach (var c in text)
        {
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
                    depth++;
                    sawOpen = true;
                    break;
                case '}':
                    if (depth > 0)
                        depth--;
                    break;
            }
        }

        return sawOpen && depth == 0 && !inString;
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
