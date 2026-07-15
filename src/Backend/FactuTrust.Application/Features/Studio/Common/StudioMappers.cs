using System.Text.Json.Nodes;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.Studio.Common;

/// <summary>Maps Studio domain entities to their API DTOs (parsing the JSON columns).</summary>
public static class StudioMappers
{
    public static CustomEntityDto ToDto(CustomEntityDefinition e, int fieldCount) =>
        new(e.Id, e.Key, e.DisplayName, e.DisplayNamePlural, e.Icon, e.Description, e.IsActive,
            fieldCount, e.SystemId, e.CreatedAt, e.UpdatedAt);

    public static CustomSystemDto ToSystemDto(CustomSystemDefinition s, int entityCount, IReadOnlyList<string>? onboardingSteps) =>
        new(s.Id, s.Key, s.DisplayName, s.Icon, s.Description, onboardingSteps, s.IsActive, entityCount, s.CreatedAt, s.UpdatedAt);

    public static CustomFieldDto ToDto(CustomFieldDefinition f)
    {
        var isSelect = f.FieldType is CustomFieldType.Select or CustomFieldType.MultiSelect;
        var isRelation = f.FieldType is CustomFieldType.RelationCustom or CustomFieldType.RelationExisting;

        // Raw OptionsJson exposed as `Config` so the renderer can read per-type settings
        // (money.currency, rating.max, render.format/source, …). Bespoke types still use Options/Relation.
        JsonNode? config = null;
        if (!string.IsNullOrWhiteSpace(f.OptionsJson) && !isSelect && !isRelation)
        {
            try { config = JsonNode.Parse(f.OptionsJson); }
            catch (System.Text.Json.JsonException) { /* leave null on corrupt config */ }
        }

        return new CustomFieldDto(
            f.Id,
            f.Key,
            f.Label,
            f.FieldType,
            f.IsRequired,
            f.IsUnique,
            f.SortOrder,
            StudioFieldJson.ParseRules(f.ValidationRulesJson),
            isSelect ? StudioFieldJson.ParseOptions(f.OptionsJson) : null,
            isRelation ? StudioFieldJson.ParseRelation(f.OptionsJson) : null,
            f.IsActive,
            config);
    }

    public static CustomRecordDto ToDto(CustomRecord r)
    {
        JsonNode? data = null;
        try { data = JsonNode.Parse(r.DataJson); }
        catch (System.Text.Json.JsonException) { /* leave null on corrupt rows */ }
        var rowVersion = r.RowVersion is { Length: > 0 } ? System.Convert.ToBase64String(r.RowVersion) : null;
        return new CustomRecordDto(r.Id, data, r.CreatedAt, r.UpdatedAt, rowVersion);
    }
}
