using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services.Studio;

/// <summary>
/// Compute-on-read resolver for Lookup and Rollup fields. Batches the related/child loads (once per
/// relation/rollup, not per row) and injects the resolved values into the record DTOs. Fully
/// best-effort: any failure resolving a field is swallowed so the list/get is never broken by it.
/// </summary>
public sealed class StudioComputedFieldReader : IStudioComputedFieldReader
{
    private const int MaxRelated = 5000;
    private const int MaxChildren = 5000;

    private readonly ICustomEntityRepository _entities;
    private readonly ICustomRecordRepository _records;
    private readonly IExistingDataSourceProvider _existing;
    private readonly ILogger<StudioComputedFieldReader> _logger;

    public StudioComputedFieldReader(
        ICustomEntityRepository entities, ICustomRecordRepository records,
        IExistingDataSourceProvider existing, ILogger<StudioComputedFieldReader> logger)
    {
        _entities = entities;
        _records = records;
        _existing = existing;
        _logger = logger;
    }

    public async Task EnrichAsync(
        Guid tenantId, IReadOnlyList<CustomFieldDefinition> fields,
        IReadOnlyList<CustomRecordDto> records, CancellationToken cancellationToken = default)
    {
        var lookups = fields.Where(f => f.FieldType == CustomFieldType.Lookup).ToList();
        var rollups = fields.Where(f => f.FieldType == CustomFieldType.Rollup).ToList();
        if (lookups.Count == 0 && rollups.Count == 0) return;

        var targets = records
            .Where(r => r.Data is JsonObject)
            .Select(r => (Dto: r, Obj: (JsonObject)r.Data!))
            .ToList();
        if (targets.Count == 0) return;

        if (lookups.Count > 0)
            await ApplyLookupsAsync(tenantId, fields, lookups, targets, cancellationToken);

        if (rollups.Count > 0)
            await ApplyRollupsAsync(tenantId, rollups, targets, cancellationToken);
    }

    // ---- Lookups ----

    private async Task ApplyLookupsAsync(
        Guid tenantId, IReadOnlyList<CustomFieldDefinition> fields, IReadOnlyList<CustomFieldDefinition> lookups,
        List<(CustomRecordDto Dto, JsonObject Obj)> targets, CancellationToken ct)
    {
        var byKey = fields.ToDictionary(f => f.Key, f => f, StringComparer.Ordinal);

        // Group lookup fields by the relation field they traverse, so the related set loads once per relation.
        var groups = lookups
            .Select(l => (Field: l, Cfg: StudioLookupRollup.ParseLookup(l.OptionsJson)))
            .Where(x => x.Cfg is not null)
            .GroupBy(x => x.Cfg!.Via, StringComparer.Ordinal);

        foreach (var group in groups)
        {
            try
            {
                if (!byKey.TryGetValue(group.Key, out var viaField)) continue;
                var rel = StudioFieldJson.ParseRelation(viaField.OptionsJson);
                if (rel is null) continue;

                var ids = targets
                    .Select(t => AsString(t.Obj[group.Key]))
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Select(s => s!)
                    .ToHashSet(StringComparer.Ordinal);
                if (ids.Count == 0) continue;

                if (string.Equals(rel.Kind, "custom", StringComparison.Ordinal))
                {
                    var relEntity = await _entities.GetByKeyAsync(tenantId, rel.Ref, ct);
                    if (relEntity is null) continue;
                    var rows = await _records.GetAllForReportAsync(tenantId, relEntity.Id, MaxRelated, ct);
                    var map = rows.ToDictionary(r => r.Id.ToString(), r => ParseObject(r.DataJson), StringComparer.Ordinal);

                    foreach (var (_, obj) in targets)
                    {
                        var viaId = AsString(obj[group.Key]);
                        if (viaId is null || !map.TryGetValue(viaId, out var relObj)) continue;
                        foreach (var (lf, cfg) in group.Select(x => (x.Field, x.Cfg!)))
                        {
                            var val = relObj[cfg.Target];
                            if (val is not null) obj[lf.Key] = val.DeepClone();
                        }
                    }
                }
                else if (string.Equals(rel.Kind, "existing", StringComparison.Ordinal))
                {
                    var dict = await _existing.GetRelationRecordsByIdsAsync(tenantId, rel.Ref, ids, ct);
                    if (dict is null) continue;

                    foreach (var (_, obj) in targets)
                    {
                        var viaId = AsString(obj[group.Key]);
                        if (viaId is null || !dict.TryGetValue(viaId, out var src)) continue;
                        foreach (var (lf, cfg) in group.Select(x => (x.Field, x.Cfg!)))
                        {
                            if (src.TryGetValue(cfg.Target, out var v) && v is not null)
                                obj[lf.Key] = ClrToJson(v);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lookup resolution failed for relation field {Via} (tenant {TenantId})", group.Key, tenantId);
            }
        }
    }

    // ---- Rollups ----

    private async Task ApplyRollupsAsync(
        Guid tenantId, IReadOnlyList<CustomFieldDefinition> rollups,
        List<(CustomRecordDto Dto, JsonObject Obj)> targets, CancellationToken ct)
    {
        foreach (var rollupField in rollups)
        {
            try
            {
                var cfg = StudioLookupRollup.ParseRollup(rollupField.OptionsJson);
                if (cfg is null) continue;

                var childEntity = await _entities.GetByKeyAsync(tenantId, cfg.Entity, ct);
                if (childEntity is null) continue;

                var children = await _records.GetAllForReportAsync(tenantId, childEntity.Id, MaxChildren, ct);

                // Group children by the parent id they point at (their relation field's value).
                var groups = new Dictionary<string, List<JsonObject>>(StringComparer.Ordinal);
                foreach (var child in children)
                {
                    var obj = ParseObject(child.DataJson);
                    var parentId = AsString(obj[cfg.RelationField]);
                    if (parentId is null) continue;
                    if (!groups.TryGetValue(parentId, out var list))
                        groups[parentId] = list = new List<JsonObject>();
                    list.Add(obj);
                }

                foreach (var (dto, obj) in targets)
                {
                    groups.TryGetValue(dto.Id.ToString(), out var grp);
                    var value = Aggregate(cfg, grp);
                    if (value is not null) obj[rollupField.Key] = JsonValue.Create(value.Value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Rollup resolution failed for field {Field} (tenant {TenantId})", rollupField.Key, tenantId);
            }
        }
    }

    private static decimal? Aggregate(RollupConfig cfg, List<JsonObject>? group)
    {
        if (string.Equals(cfg.Agg, "count", StringComparison.Ordinal))
            return group?.Count ?? 0;

        var values = (group ?? new List<JsonObject>())
            .Select(o => cfg.Field is null ? (decimal?)null : ToDecimal(o[cfg.Field]))
            .Where(d => d is not null)
            .Select(d => d!.Value)
            .ToList();

        return cfg.Agg switch
        {
            "sum" => values.Sum(),
            "avg" => values.Count > 0 ? values.Average() : null,
            "min" => values.Count > 0 ? values.Min() : null,
            "max" => values.Count > 0 ? values.Max() : null,
            _ => null
        };
    }

    // ---- Helpers ----

    private static JsonObject ParseObject(string json)
    {
        try { return JsonNode.Parse(json) as JsonObject ?? new JsonObject(); }
        catch (JsonException) { return new JsonObject(); }
    }

    private static string? AsString(JsonNode? node)
    {
        if (node is not JsonValue v) return null;
        if (v.TryGetValue<string>(out var s)) return s;
        return v.ToString();
    }

    private static decimal? ToDecimal(JsonNode? node)
    {
        if (node is not JsonValue v) return null;
        if (v.TryGetValue<decimal>(out var d)) return d;
        if (v.TryGetValue<string>(out var s) &&
            decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var ds)) return ds;
        return null;
    }

    private static JsonNode? ClrToJson(object? value) => value switch
    {
        null => null,
        bool b => JsonValue.Create(b),
        decimal d => JsonValue.Create(d),
        string s => JsonValue.Create(s),
        int or long or short or byte => JsonValue.Create(Convert.ToDecimal(value, CultureInfo.InvariantCulture)),
        double or float => JsonValue.Create(Convert.ToDecimal(value, CultureInfo.InvariantCulture)),
        _ => JsonValue.Create(Convert.ToString(value, CultureInfo.InvariantCulture))
    };
}
