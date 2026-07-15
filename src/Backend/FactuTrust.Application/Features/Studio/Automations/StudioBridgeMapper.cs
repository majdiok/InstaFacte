using System.Text.Json;
using System.Text.Json.Nodes;
using FactuTrust.Application.Features.AI.DTOs;

namespace FactuTrust.Application.Features.Studio.Automations;

/// <summary>One action-parameter binding: take the value from a record <c>field</c> or a literal <c>const</c>.</summary>
public sealed record BridgeParamMapping(string Param, string Source, string? Value);

/// <summary>
/// Pure (no DB) translation of a record's data + a parameter mapping into the argument dictionary an
/// ERP action tool expects, plus required-parameter validation. Relation fields already hold the ERP
/// GUID, so they map like any other field. Values are passed as strings; the tool handlers parse them.
/// </summary>
public static class StudioBridgeMapper
{
    private const string SourceConst = "const";

    public static IReadOnlyList<BridgeParamMapping> ParseMappings(string? mappingJson)
    {
        if (string.IsNullOrWhiteSpace(mappingJson)) return Array.Empty<BridgeParamMapping>();
        try
        {
            var arr = JsonNode.Parse(mappingJson) as JsonArray;
            if (arr is null) return Array.Empty<BridgeParamMapping>();
            var result = new List<BridgeParamMapping>();
            foreach (var item in arr)
            {
                if (item is not JsonObject o) continue;
                var param = Str(o["param"]);
                if (string.IsNullOrWhiteSpace(param)) continue;
                var source = Str(o["source"]) ?? "field";
                result.Add(new BridgeParamMapping(param!, source, Str(o["value"])));
            }
            return result;
        }
        catch (JsonException) { return Array.Empty<BridgeParamMapping>(); }
    }

    /// <summary>Builds the tool arguments. Returns an error message if a required parameter is missing/empty.</summary>
    public static (Dictionary<string, object?> Args, string? Error) Build(
        IReadOnlyList<BridgeParamMapping> mappings, JsonObject? recordData, AiToolDefinition tool)
    {
        var args = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var m in mappings)
        {
            if (string.IsNullOrWhiteSpace(m.Param)) continue;
            string? value = string.Equals(m.Source, SourceConst, StringComparison.Ordinal)
                ? m.Value
                : ReadFieldAsString(recordData, m.Value);
            if (!string.IsNullOrEmpty(value))
                args[m.Param] = value;
        }

        foreach (var req in tool.RequiredParameters)
        {
            if (!args.TryGetValue(req, out var v) || v is null || (v is string s && s.Length == 0))
                return (args, $"Paramètre requis manquant pour l'action : « {req} ».");
        }

        return (args, null);
    }

    private static string? ReadFieldAsString(JsonObject? data, string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || data is null) return null;
        var node = data[key];
        if (node is not JsonValue v) return null;
        if (v.TryGetValue<string>(out var s)) return s;
        return v.ToString();
    }

    private static string? Str(JsonNode? n)
    {
        if (n is not JsonValue v) return null;
        if (v.TryGetValue<string>(out var s)) return s;
        return v.ToString();
    }
}
