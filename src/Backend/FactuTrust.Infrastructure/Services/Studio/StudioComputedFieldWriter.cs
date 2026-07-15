using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Infrastructure.Services.Studio;

/// <summary>
/// Computes "write-time" custom field values and injects them into a record's canonical JSON:
///  - <b>AutoNumber</b>: reserved atomically via <see cref="ICustomSequenceAllocator"/>, immutable once set;
///  - <b>Formula</b>: re-evaluated on every write (topological order over inter-formula deps) with the
///    sandboxed <see cref="FormulaEngine"/>.
/// Neither value is ever taken from user input (the validator strips computed fields).
/// </summary>
public sealed class StudioComputedFieldWriter : IStudioComputedFieldWriter
{
    private readonly ICustomSequenceAllocator _allocator;

    public StudioComputedFieldWriter(ICustomSequenceAllocator allocator) => _allocator = allocator;

    public Task<string> ApplyOnCreateAsync(
        Guid tenantId, Guid entityDefinitionId, IReadOnlyList<CustomFieldDefinition> fields,
        string canonicalJson, CancellationToken cancellationToken = default)
        => ApplyAsync(tenantId, entityDefinitionId, fields, canonicalJson, existingJson: null, cancellationToken);

    public Task<string> ApplyOnUpdateAsync(
        Guid tenantId, Guid entityDefinitionId, IReadOnlyList<CustomFieldDefinition> fields,
        string canonicalJson, string existingJson, CancellationToken cancellationToken = default)
        => ApplyAsync(tenantId, entityDefinitionId, fields, canonicalJson, existingJson, cancellationToken);

    private async Task<string> ApplyAsync(
        Guid tenantId, Guid entityDefinitionId, IReadOnlyList<CustomFieldDefinition> fields,
        string canonicalJson, string? existingJson, CancellationToken cancellationToken)
    {
        var hasAuto = fields.Any(f => f.FieldType == CustomFieldType.AutoNumber);
        var hasFormula = fields.Any(f => f.FieldType == CustomFieldType.Formula);
        if (!hasAuto && !hasFormula)
            return canonicalJson;

        var obj = ParseObject(canonicalJson);
        var existing = existingJson is null ? null : ParseObject(existingJson);

        if (hasAuto)
            await ApplyAutoNumbersAsync(tenantId, entityDefinitionId, fields, obj, existing, cancellationToken);

        if (hasFormula)
            ApplyFormulas(fields, obj);

        return obj.ToJsonString();
    }

    // ---- AutoNumber ----

    private async Task ApplyAutoNumbersAsync(
        Guid tenantId, Guid entityDefinitionId, IReadOnlyList<CustomFieldDefinition> fields,
        JsonObject obj, JsonObject? existing, CancellationToken cancellationToken)
    {
        foreach (var field in fields.Where(f => f.FieldType == CustomFieldType.AutoNumber))
        {
            // Immutable: preserve a value the record already has (update path).
            var prior = existing?[field.Key];
            if (prior is not null)
            {
                obj[field.Key] = prior.DeepClone();
                continue;
            }

            if (obj[field.Key] is null)
            {
                var seq = await _allocator.ReserveNextAsync(tenantId, entityDefinitionId, field.Key, cancellationToken);
                obj[field.Key] = JsonValue.Create(StudioAutoNumber.Format(seq, field.OptionsJson));
            }
        }
    }

    // ---- Formula ----

    private static void ApplyFormulas(IReadOnlyList<CustomFieldDefinition> fields, JsonObject obj)
    {
        var formulaFields = fields.Where(f => f.FieldType == CustomFieldType.Formula).ToList();
        if (formulaFields.Count == 0) return;

        // Live value map the formulas read from; updated as each formula is computed so dependents see it.
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var f in fields)
            values[f.Key] = JsonToClr(obj[f.Key]);

        foreach (var field in TopoOrder(formulaFields))
        {
            var expr = StudioFormula.GetExpression(field.OptionsJson);
            if (FormulaEngine.TryEvaluate(expr, values, out var result) && result is not null)
            {
                var node = ClrToJson(result);
                obj[field.Key] = node;
                values[field.Key] = JsonToClr(node);
            }
            else
            {
                // Unevaluable (e.g. missing input) → leave the field absent rather than storing garbage.
                obj.Remove(field.Key);
                values[field.Key] = null;
            }
        }
    }

    /// <summary>Orders formula fields so each is computed after the formulas it references (graph is acyclic by design-time check).</summary>
    private static IReadOnlyList<CustomFieldDefinition> TopoOrder(IReadOnlyList<CustomFieldDefinition> formulaFields)
    {
        var byKey = formulaFields.ToDictionary(f => f.Key, f => f, StringComparer.Ordinal);
        var ordered = new List<CustomFieldDefinition>(formulaFields.Count);
        var state = new Dictionary<string, int>(StringComparer.Ordinal); // 0=unseen,1=visiting,2=done

        void Visit(CustomFieldDefinition f)
        {
            state.TryGetValue(f.Key, out var s);
            if (s == 2 || s == 1) return; // done, or guard against an unexpected cycle
            state[f.Key] = 1;
            foreach (var dep in FormulaEngine.GetReferences(StudioFormula.GetExpression(f.OptionsJson)))
                if (byKey.TryGetValue(dep, out var depField))
                    Visit(depField);
            state[f.Key] = 2;
            ordered.Add(f);
        }

        foreach (var f in formulaFields) Visit(f);
        return ordered;
    }

    // ---- JSON <-> CLR for the evaluator ----

    private static object? JsonToClr(JsonNode? node)
    {
        if (node is not JsonValue v) return node is null ? null : node.ToString();
        if (v.TryGetValue<bool>(out var b)) return b;
        if (v.TryGetValue<decimal>(out var d)) return d;
        if (v.TryGetValue<string>(out var s)) return s;
        return v.ToString();
    }

    private static JsonNode? ClrToJson(object? value) => value switch
    {
        null => null,
        bool b => JsonValue.Create(b),
        decimal d => JsonValue.Create(d),
        string s => JsonValue.Create(s),
        _ => JsonValue.Create(Convert.ToString(value, CultureInfo.InvariantCulture))
    };

    private static JsonObject ParseObject(string json)
    {
        try { return JsonNode.Parse(json) as JsonObject ?? new JsonObject(); }
        catch (JsonException) { return new JsonObject(); }
    }
}
