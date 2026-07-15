using System.Text.Json;
using System.Text.Json.Nodes;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.Studio.Common;

/// <summary>A lookup field's config: read <c>Target</c> from the record referenced by relation field <c>Via</c>.</summary>
public sealed record LookupConfig(string Via, string Target);

/// <summary>
/// A rollup field's config: aggregate child records of custom entity <c>Entity</c> whose relation field
/// <c>RelationField</c> points back at this record. <c>Field</c> is the child field to aggregate (ignored for count).
/// </summary>
public sealed record RollupConfig(string Entity, string RelationField, string Agg, string? Field);

/// <summary>
/// Config (de)serialization + design-time validation for Lookup and Rollup fields. Both are
/// compute-on-read: the value is resolved fresh by the reader and never persisted.
/// </summary>
public static class StudioLookupRollup
{
    public static readonly IReadOnlySet<string> Aggregates =
        new HashSet<string>(StringComparer.Ordinal) { "count", "sum", "avg", "min", "max" };

    // ---- Lookup ----

    public static LookupConfig? ParseLookup(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson)) return null;
        try
        {
            var l = JsonNode.Parse(optionsJson)?["lookup"];
            var via = l?["via"]?.GetValue<string>();
            var target = l?["target"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(via) || string.IsNullOrWhiteSpace(target)) return null;
            return new LookupConfig(via, target);
        }
        catch (JsonException) { return null; }
        catch (InvalidOperationException) { return null; }
    }

    public static string SerializeLookup(string via, string target) =>
        new JsonObject { ["lookup"] = new JsonObject { ["via"] = via.Trim(), ["target"] = target.Trim() } }.ToJsonString();

    public static Error? ValidateLookup(string? via, string? target, IReadOnlyList<CustomFieldDefinition> siblings)
    {
        if (string.IsNullOrWhiteSpace(via) || string.IsNullOrWhiteSpace(target))
            return Error.Validation("lookup", "Le champ relation et le champ cible sont obligatoires.");

        var viaField = siblings.FirstOrDefault(f => string.Equals(f.Key, via, StringComparison.Ordinal));
        if (viaField is null)
            return Error.Validation("lookup", $"Champ relation introuvable : « {via} ».");
        if (viaField.FieldType is not (CustomFieldType.RelationCustom or CustomFieldType.RelationExisting))
            return Error.Validation("lookup", "Le champ « via » doit être un champ de type relation.");
        return null;
    }

    // ---- Rollup ----

    public static RollupConfig? ParseRollup(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson)) return null;
        try
        {
            var r = JsonNode.Parse(optionsJson)?["rollup"];
            var entity = r?["entity"]?.GetValue<string>();
            var relationField = r?["relationField"]?.GetValue<string>();
            var agg = r?["agg"]?.GetValue<string>();
            var field = r?["field"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(entity) || string.IsNullOrWhiteSpace(relationField) || string.IsNullOrWhiteSpace(agg))
                return null;
            return new RollupConfig(entity, relationField, agg, field);
        }
        catch (JsonException) { return null; }
        catch (InvalidOperationException) { return null; }
    }

    public static string SerializeRollup(string entity, string relationField, string agg, string? field)
    {
        var obj = new JsonObject
        {
            ["entity"] = entity.Trim(),
            ["relationField"] = relationField.Trim(),
            ["agg"] = agg.Trim().ToLowerInvariant()
        };
        if (!string.IsNullOrWhiteSpace(field)) obj["field"] = field.Trim();
        return new JsonObject { ["rollup"] = obj }.ToJsonString();
    }

    public static Error? ValidateRollup(RollupConfig? cfg)
    {
        if (cfg is null)
            return Error.Validation("rollup", "La configuration du rollup est incomplète.");
        if (!Aggregates.Contains(cfg.Agg))
            return Error.Validation("rollup", "Agrégat invalide (count, sum, avg, min, max).");
        if (!string.Equals(cfg.Agg, "count", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(cfg.Field))
            return Error.Validation("rollup", "Un champ à agréger est requis pour cet agrégat.");
        return null;
    }
}
