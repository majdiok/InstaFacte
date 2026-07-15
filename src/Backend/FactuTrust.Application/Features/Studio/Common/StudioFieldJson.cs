using System.Text.Json;
using System.Text.Json.Nodes;

namespace FactuTrust.Application.Features.Studio.Common;

/// <summary>
/// (De)serialization helpers for the JSON columns on a custom field definition.
/// Select/MultiSelect choices and Relation targets both live in <c>OptionsJson</c>, disambiguated
/// by the wrapper key (<c>options</c> vs <c>relation</c>). Validation rules live in <c>ValidationRulesJson</c>.
/// </summary>
public static class StudioFieldJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static FieldValidationRules? ParseRules(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<FieldValidationRules>(json, Options); }
        catch (JsonException) { return null; }
    }

    public static string? SerializeRules(FieldValidationRules? rules) =>
        rules is null ? null : JsonSerializer.Serialize(rules, Options);

    public static IReadOnlyList<SelectOptionDto>? ParseOptions(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson)) return null;
        try
        {
            var node = JsonNode.Parse(optionsJson);
            var arr = node?["options"]?.AsArray();
            if (arr is null) return null;
            var result = new List<SelectOptionDto>();
            foreach (var item in arr)
            {
                var value = item?["value"]?.GetValue<string>();
                if (value is null) continue;
                var label = item?["label"]?.GetValue<string>() ?? value;
                result.Add(new SelectOptionDto(value, label));
            }
            return result;
        }
        catch (JsonException) { return null; }
        catch (InvalidOperationException) { return null; }
    }

    public static RelationRefDto? ParseRelation(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson)) return null;
        try
        {
            var rel = JsonNode.Parse(optionsJson)?["relation"];
            var kind = rel?["kind"]?.GetValue<string>();
            var refKey = rel?["ref"]?.GetValue<string>();
            if (kind is null || refKey is null) return null;
            return new RelationRefDto(kind, refKey);
        }
        catch (JsonException) { return null; }
        catch (InvalidOperationException) { return null; }
    }

    public static string? SerializeOptions(IReadOnlyList<SelectOptionDto>? options)
    {
        if (options is null || options.Count == 0) return null;
        var arr = new JsonArray();
        foreach (var o in options)
            arr.Add(new JsonObject { ["value"] = o.Value, ["label"] = o.Label });
        return new JsonObject { ["options"] = arr }.ToJsonString();
    }

    public static string? SerializeRelation(RelationRefDto? relation)
    {
        if (relation is null) return null;
        return new JsonObject
        {
            ["relation"] = new JsonObject { ["kind"] = relation.Kind, ["ref"] = relation.Ref }
        }.ToJsonString();
    }
}
