using System.Text.Json;
using System.Text.Json.Nodes;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.Studio.Common;

/// <summary>
/// Config + design-time validation for Formula fields. The expression lives in OptionsJson
/// (<c>formula.expr</c>). Validation checks syntax/functions (via <see cref="FormulaEngine"/>),
/// that every referenced key is an existing sibling field, and that no formula→formula cycle is created.
/// </summary>
public static class StudioFormula
{
    /// <summary>Reads the stored expression from a Formula field's OptionsJson (null if absent/corrupt).</summary>
    public static string? GetExpression(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson)) return null;
        try
        {
            if (JsonNode.Parse(optionsJson)?["formula"]?["expr"] is JsonValue v && v.TryGetValue<string>(out var s))
                return s;
        }
        catch (JsonException) { }
        return null;
    }

    /// <summary>Serializes the expression into the canonical <c>{formula:{expr}}</c> OptionsJson.</summary>
    public static string Serialize(string expression) =>
        new JsonObject { ["formula"] = new JsonObject { ["expr"] = expression.Trim() } }.ToJsonString();

    /// <summary>
    /// Validates a Formula field about to be saved. <paramref name="siblings"/> is every field of the
    /// entity (active or not), including the field itself on update.
    /// </summary>
    public static Error? Validate(string fieldKey, string? expression, IReadOnlyList<CustomFieldDefinition> siblings)
    {
        var (ok, error, refs) = FormulaEngine.Validate(expression);
        if (!ok)
            return Error.Validation("formula", error ?? "Expression de formule invalide.");

        var keys = siblings.Select(f => f.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var r in refs)
        {
            if (string.Equals(r, fieldKey, StringComparison.Ordinal))
                return Error.Validation("formula", "Une formule ne peut pas se référencer elle-même.");
            if (!keys.Contains(r))
                return Error.Validation("formula", $"Référence inconnue dans la formule : « {r} ».");
        }

        // Build the formula-dependency graph (formula field key → referenced formula field keys),
        // overriding this field's edges with the new expression, then look for a path back to itself.
        var formulaFields = siblings
            .Where(f => f.FieldType == CustomFieldType.Formula)
            .Select(f => f.Key)
            .ToHashSet(StringComparer.Ordinal);
        formulaFields.Add(fieldKey);

        var graph = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var f in siblings.Where(f => f.FieldType == CustomFieldType.Formula))
        {
            var fRefs = string.Equals(f.Key, fieldKey, StringComparison.Ordinal)
                ? refs
                : FormulaEngine.GetReferences(GetExpression(f.OptionsJson));
            graph[f.Key] = fRefs.Where(formulaFields.Contains).ToHashSet(StringComparer.Ordinal);
        }
        graph[fieldKey] = refs.Where(formulaFields.Contains).ToHashSet(StringComparer.Ordinal);

        if (CanReach(graph, fieldKey, fieldKey))
            return Error.Validation("formula", "Cycle de dépendance détecté entre les formules.");

        return null;
    }

    /// <summary>True if <paramref name="target"/> is reachable from <paramref name="start"/> via ≥1 edge.</summary>
    private static bool CanReach(IReadOnlyDictionary<string, HashSet<string>> graph, string start, string target)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        if (graph.TryGetValue(start, out var first))
            foreach (var n in first) stack.Push(n);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (string.Equals(node, target, StringComparison.Ordinal)) return true;
            if (!visited.Add(node)) continue;
            if (graph.TryGetValue(node, out var next))
                foreach (var n in next) stack.Push(n);
        }
        return false;
    }
}
