using System.Text.Json.Nodes;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Studio;

namespace FactuTrust.Application.Features.Studio.Common;

/// <summary>
/// Fusion partielle d'un PATCH d'enregistrement (PR 2.3, R5) : applique les clés fournies sur le JSON
/// existant (<c>null</c> = effacement de la clé), refuse les clés inconnues, réservées
/// (<see cref="StudioKey.IsReservedFieldKey"/>) ou calculées (<c>Formula/Lookup/Rollup/AutoNumber</c>),
/// puis délègue à <see cref="CustomRecordValidator.ValidateAndCanonicalize"/> qui produit le JSON
/// canonique complet (champs requis, types, etc.). Les clés absentes du patch sont conservées.
/// </summary>
public static class CustomRecordPatchMerger
{
    /// <summary>
    /// Fusionne <paramref name="patch"/> dans <paramref name="existingDataJson"/> et valide le document
    /// résultant contre <paramref name="fields"/>. Retourne le JSON canonique ou une erreur
    /// <c>Validation.data</c> / <c>Validation.&lt;clé&gt;</c>.
    /// </summary>
    public static Result<string> MergePatch(
        string existingDataJson, IReadOnlyDictionary<string, JsonNode?> patch, IReadOnlyList<CustomFieldDefinition> fields)
    {
        var byKey = fields.Where(f => f.IsActive).ToDictionary(f => f.Key, StringComparer.Ordinal);

        // Refus des clés interdites AVANT toute fusion (message déterministe, code Validation.data).
        foreach (var key in patch.Keys)
        {
            if (StudioKey.IsReservedFieldKey(key))
                return Result.Failure<string>(Error.Validation("data", $"Clé réservée interdite : « {key} »."));
            if (!byKey.TryGetValue(key, out var field))
                return Result.Failure<string>(Error.Validation(key, $"Champ inconnu : « {key} »."));
            if (CustomRecordValidator.IsComputed(field.FieldType))
                return Result.Failure<string>(Error.Validation("data", $"Le champ calculé « {key} » n'est pas modifiable."));
        }

        // Point de départ : document existant (objet), sinon objet vide.
        JsonObject merged;
        try
        {
            merged = JsonNode.Parse(existingDataJson) as JsonObject ?? new JsonObject();
        }
        catch (System.Text.Json.JsonException)
        {
            merged = new JsonObject();
        }

        foreach (var (key, value) in patch)
        {
            if (value is null)
                merged.Remove(key); // null = effacement explicite de la clé
            else
                merged[key] = value.DeepClone();
        }

        // Le validateur produit le JSON canonique complet (tous les champs actifs), ce qui conserve les
        // clés absentes du patch (elles restent dans `merged`) et applique les règles de type/requis.
        var document = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var kv in merged)
            document[kv.Key] = kv.Value;

        return CustomRecordValidator.ValidateAndCanonicalize(fields, document);
    }
}
