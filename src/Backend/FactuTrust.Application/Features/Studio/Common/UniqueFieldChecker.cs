using System.Text.Json.Nodes;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Studio;

namespace FactuTrust.Application.Features.Studio.Common;

/// <summary>
/// Enforces the <c>IsUnique</c> flag on custom fields at write time: for each active unique field
/// with a value, checks no other active record holds the same value (compared against the canonical
/// stored representation so it matches how <c>JSON_VALUE</c> reads it back).
/// </summary>
public static class UniqueFieldChecker
{
    public static async Task<Error?> CheckAsync(
        ICustomRecordRepository records,
        Guid tenantId,
        Guid entityId,
        IReadOnlyList<CustomFieldDefinition> fields,
        string canonicalJson,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        if (JsonNode.Parse(canonicalJson) is not JsonObject obj)
            return null;

        foreach (var f in fields.Where(x => x.IsUnique && x.IsActive))
        {
            if (!obj.TryGetPropertyValue(f.Key, out var node) || node is null)
                continue;
            var value = ToComparable(node);
            if (string.IsNullOrEmpty(value))
                continue;

            if (await records.ExistsWithFieldValueAsync(tenantId, entityId, f.Key, value, excludeId, cancellationToken))
                return Error.Conflict($"« {f.Label} » doit être unique : cette valeur existe déjà.");
        }
        return null;
    }

    private static string? ToComparable(JsonNode node)
    {
        if (node is JsonValue v)
        {
            if (v.TryGetValue<string>(out var s)) return s;
            return v.ToString();
        }
        return null; // arrays/objects (e.g. MultiSelect) are not unique-checkable
    }
}
