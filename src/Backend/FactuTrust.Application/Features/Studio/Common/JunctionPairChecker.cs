using System.Text.Json.Nodes;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.Studio.Common;

/// <summary>
/// PR 2.1 — unicité de la paire (source, cible) d'une table de jonction
/// (<see cref="CustomEntityKind.Junction"/>) au moment de l'écriture : un même couple d'identifiants
/// ne peut être lié qu'une fois. Les tables <see cref="CustomEntityKind.Standard"/> ne déclenchent
/// AUCUN appel au dépôt. Compare les valeurs canoniques (même représentation que <c>JSON_VALUE</c>).
/// </summary>
public static class JunctionPairChecker
{
    /// <summary>
    /// Renvoie <see cref="StudioErrorCodes.RecordDuplicateLink"/> si un autre enregistrement actif porte déjà
    /// la même paire ; <c>null</c> sinon (ou si l'entité n'est pas une jonction / les valeurs manquent —
    /// la validation « requis » est déjà assurée par <see cref="CustomRecordValidator"/>).
    /// </summary>
    public static async Task<Error?> CheckAsync(
        ICustomRecordRepository records,
        CustomEntityDefinition entity,
        IReadOnlyList<CustomFieldDefinition> fields,
        string canonicalJson,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        if (entity.Kind != CustomEntityKind.Junction)
            return null;

        var pair = ResolvePairFields(fields);
        if (pair is null)
            return null;

        if (JsonNode.Parse(canonicalJson) is not JsonObject obj)
            return null;

        var (fieldA, fieldB) = pair.Value;
        var valueA = Read(obj, fieldA.Key);
        var valueB = Read(obj, fieldB.Key);
        if (string.IsNullOrEmpty(valueA) || string.IsNullOrEmpty(valueB))
            return null;

        var exists = await records.ExistsWithFieldPairAsync(
            entity.TenantId, entity.Id, fieldA.Key, valueA, fieldB.Key, valueB, excludeId, cancellationToken);

        return exists
            ? new Error(StudioErrorCodes.RecordDuplicateLink, "Ce lien existe déjà entre ces deux enregistrements.")
            : null;
    }

    /// <summary>
    /// Les deux champs de liaison d'une jonction : les deux premiers champs <c>RelationCustom</c> actifs
    /// dans l'ordre d'affichage (source puis cible, tels que créés par <c>CreateManyToManyRelationCommand</c>).
    /// </summary>
    public static (CustomFieldDefinition Source, CustomFieldDefinition Target)? ResolvePairFields(
        IReadOnlyList<CustomFieldDefinition> fields)
    {
        var links = fields
            .Where(f => f.IsActive && f.FieldType == CustomFieldType.RelationCustom)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Key, StringComparer.Ordinal)
            .Take(2)
            .ToList();

        return links.Count == 2 ? (links[0], links[1]) : null;
    }

    private static string? Read(JsonObject obj, string key)
    {
        if (!obj.TryGetPropertyValue(key, out var node) || node is null)
            return null;

        if (node is JsonValue v)
        {
            if (v.TryGetValue<string>(out var s)) return s;
            return v.ToString();
        }

        return null; // arrays/objects are not link values
    }
}
