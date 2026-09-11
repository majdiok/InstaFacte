using System.Text;
using System.Text.Json;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;
using Microsoft.Extensions.Caching.Memory;

namespace FactuTrust.Infrastructure.Services.Studio;

/// <summary>
/// Digests de contexte Studio injectés dans le prompt StudioBuilder (PR 1.2). Métadonnées de schéma
/// uniquement — jamais de valeurs d'enregistrements. Toutes les lectures sont SÉQUENTIELLES (les
/// dépôts partagent le même DbContext tenant scoped) et bornées : au plus <see cref="MaxEntities"/>
/// tables, une lecture de champs par table, le tout mis en cache <see cref="CacheTtl"/> par tenant.
/// </summary>
public sealed class StudioContextDigestService : IStudioContextDigestService
{
    /// <summary>Nombre maximal de tables résumées (au-delà : « … (+N tables) »).</summary>
    public const int MaxEntities = 50;

    /// <summary>Durée de vie du digest de schéma en cache. Courte : une table créée doit apparaître vite.</summary>
    public static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    /// <summary>Un plan terminé plus ancien n'est plus « le dernier plan » : l'utilisateur est passé à autre chose.</summary>
    public static readonly TimeSpan CompletedPlanWindow = TimeSpan.FromHours(24);

    private const string Ellipsis = "…";

    private readonly ICustomEntityRepository _entities;
    private readonly ICustomFieldRepository _fields;
    private readonly ICustomSystemRepository _systems;
    private readonly IStudioAiBuildPlanRepository _plans;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _timeProvider;

    public StudioContextDigestService(
        ICustomEntityRepository entities,
        ICustomFieldRepository fields,
        ICustomSystemRepository systems,
        IStudioAiBuildPlanRepository plans,
        IMemoryCache cache,
        TimeProvider? timeProvider = null)
    {
        _entities = entities;
        _fields = fields;
        _systems = systems;
        _plans = plans;
        _cache = cache;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public static string SchemaCacheKey(Guid tenantId) => $"studio:schema-digest:{tenantId}";

    public async Task<string> BuildSchemaDigestAsync(Guid tenantId, int maxChars, CancellationToken cancellationToken)
    {
        if (maxChars <= 0)
            return string.Empty;

        // Le cache porte les lignes NON tronquées : les budgets CPU / avancé partagent la même entrée.
        var cacheKey = SchemaCacheKey(tenantId);
        if (!_cache.TryGetValue(cacheKey, out SchemaLines? lines) || lines is null)
        {
            lines = await LoadSchemaLinesAsync(tenantId, cancellationToken);
            _cache.Set(cacheKey, lines, CacheTtl);
        }

        return Render(lines, maxChars);
    }

    public async Task<string?> BuildLastPlanDigestAsync(Guid tenantId, string userId, int maxChars, CancellationToken cancellationToken)
    {
        if (maxChars <= 0 || string.IsNullOrWhiteSpace(userId))
            return null;

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var entries = new List<string>();

        // Lectures séquentielles (même DbContext tenant).
        var pending = await _plans.ListPendingByOwnerAsync(tenantId, userId, cancellationToken);
        var latestPending = pending
            .Where(p => p.TenantId == tenantId)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefault();
        if (latestPending is not null)
            entries.Add(DescribePlan(latestPending, "En attente", latestPending.CreatedAt));

        var (completed, _) = await _plans.ListByOwnerAsync(
            tenantId, userId, StudioAiPlanStatus.Completed, kind: null, page: 1, pageSize: 1, cancellationToken);
        var latestCompleted = completed.FirstOrDefault(p => p.TenantId == tenantId);
        if (latestCompleted is not null)
        {
            var when = latestCompleted.ExecutedAt ?? latestCompleted.CreatedAt;
            if (now - when <= CompletedPlanWindow)
                entries.Add(DescribePlan(latestCompleted, "Terminé", when));
        }

        if (entries.Count == 0)
            return null;

        var text = string.Join("\n", entries);
        return text.Length <= maxChars ? text : Truncate(text, maxChars);
    }

    // ---------------------------------------------------------------- schéma

    private async Task<SchemaLines> LoadSchemaLinesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var entities = (await _entities.ListAsync(tenantId, includeInactive: false, cancellationToken))
            .Where(e => e.TenantId == tenantId && e.IsActive && !e.IsDeleted)
            .OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Key, StringComparer.Ordinal)
            .ToList();
        if (entities.Count == 0)
            return new SchemaLines([], 0);

        var systemKeys = new Dictionary<Guid, string>();
        if (entities.Any(e => e.SystemId.HasValue))
        {
            foreach (var system in await _systems.ListAsync(tenantId, includeInactive: true, cancellationToken))
                systemKeys.TryAdd(system.Id, system.Key);
        }

        var lines = new List<string>(Math.Min(entities.Count, MaxEntities));
        foreach (var entity in entities.Take(MaxEntities))
        {
            var fields = (await _fields.ListByEntityAsync(tenantId, entity.Id, includeInactive: false, cancellationToken))
                .Where(f => f.IsActive)
                .OrderBy(f => f.SortOrder)
                .ThenBy(f => f.Key, StringComparer.Ordinal)
                .ToList();
            lines.Add(FormatEntityLine(entity, fields, systemKeys));
        }

        return new SchemaLines(lines, Math.Max(0, entities.Count - lines.Count));
    }

    internal static string FormatEntityLine(
        CustomEntityDefinition entity,
        IReadOnlyList<CustomFieldDefinition> fields,
        IReadOnlyDictionary<Guid, string> systemKeys)
    {
        var sb = new StringBuilder();
        sb.Append("- ").Append(entity.Key).Append(" « ").Append(entity.DisplayName).Append(" »");
        if (entity.SystemId is { } systemId && systemKeys.TryGetValue(systemId, out var systemKey))
            sb.Append(" (systeme:").Append(systemKey).Append(')');
        sb.Append(" : ");
        sb.Append(fields.Count == 0
            ? "(aucun champ)"
            : string.Join(", ", fields.Select(f => $"{f.Key}:{FieldTypeToken(f.FieldType)}")));
        return sb.ToString();
    }

    /// <summary>Jeton de type court, aligné sur le vocabulaire des specs (<c>text</c>, <c>money</c>, <c>select</c>…).</summary>
    internal static string FieldTypeToken(CustomFieldType type) => type switch
    {
        CustomFieldType.Text => "text",
        CustomFieldType.MultilineText => "multiline",
        CustomFieldType.Number => "number",
        CustomFieldType.Decimal => "decimal",
        CustomFieldType.Boolean => "boolean",
        CustomFieldType.Date => "date",
        CustomFieldType.DateTime => "datetime",
        CustomFieldType.Select => "select",
        CustomFieldType.MultiSelect => "multiselect",
        CustomFieldType.RelationCustom => "relation",
        CustomFieldType.RelationExisting => "relation_erp",
        CustomFieldType.Money => "money",
        CustomFieldType.Percentage => "percentage",
        CustomFieldType.Rating => "rating",
        CustomFieldType.QrCode => "qrcode",
        CustomFieldType.Barcode => "barcode",
        CustomFieldType.AutoNumber => "autonumber",
        CustomFieldType.Formula => "formula",
        CustomFieldType.Lookup => "lookup",
        CustomFieldType.Rollup => "rollup",
        CustomFieldType.Attachment => "attachment",
        CustomFieldType.Signature => "signature",
        _ => type.ToString().ToLowerInvariant()
    };

    /// <summary>
    /// Assemble les lignes dans le budget. Une ligne qui ne tient pas est remplacée (avec les suivantes)
    /// par « … (+N tables) » : le modèle sait qu'il existe d'autres tables même s'il ne les voit pas.
    /// </summary>
    internal static string Render(SchemaLines lines, int maxChars)
    {
        if (lines.Lines.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        var included = 0;
        foreach (var line in lines.Lines)
        {
            var remainingAfter = lines.Lines.Count - included - 1 + lines.OmittedCount;
            var suffixReserve = remainingAfter > 0 ? OmittedSuffix(remainingAfter).Length + 1 : 0;
            var needed = (sb.Length > 0 ? 1 : 0) + line.Length;
            if (sb.Length + needed + suffixReserve > maxChars)
                break;
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(line);
            included++;
        }

        var omitted = lines.Lines.Count - included + lines.OmittedCount;
        if (omitted > 0)
        {
            var suffix = OmittedSuffix(omitted);
            if (sb.Length == 0)
                return suffix.Length <= maxChars ? suffix : Truncate(suffix, maxChars);
            sb.Append('\n').Append(suffix);
        }
        return sb.ToString();
    }

    private static string OmittedSuffix(int count) => $"{Ellipsis} (+{count} table{(count > 1 ? "s" : "")})";

    // ---------------------------------------------------------------- dernier plan

    private static string DescribePlan(StudioAiBuildPlan plan, string statusLabel, DateTime whenUtc)
    {
        var (title, names) = ReadPlanNames(plan);
        var sb = new StringBuilder();
        sb.Append("- [").Append(statusLabel).Append(' ').Append(whenUtc.ToString("HH:mm")).Append("] ")
          .Append(KindLabel(plan.Kind)).Append(" « ").Append(title).Append(" »");
        if (names.Count > 0)
        {
            sb.Append(" : ").Append(names.Count).Append(names.Count > 1 ? " tables (" : " table (")
              .Append(string.Join(", ", names)).Append(')');
        }
        return sb.ToString();
    }

    /// <summary>Libellé court du type de plan, dans la langue du prompt.</summary>
    internal static string KindLabel(StudioAiPlanKind kind) => kind switch
    {
        StudioAiPlanKind.CreateApp => "Table",
        StudioAiPlanKind.CreateSystem => "Système",
        StudioAiPlanKind.Amendment => "Modification",
        StudioAiPlanKind.View => "Fenêtre",
        StudioAiPlanKind.Report => "État",
        _ => kind.ToString()
    };

    /// <summary>
    /// Titre et tables du plan : clés RÉELLES depuis <c>ResultJson</c> quand le plan a été exécuté
    /// (<c>entityKey</c> / <c>entities[].entityKey</c>), sinon libellés de <c>SummaryJson</c>.
    /// Toute erreur de lecture JSON dégrade vers « (sans détail) » — jamais d'exception dans le prompt.
    /// </summary>
    internal static (string Title, IReadOnlyList<string> Names) ReadPlanNames(StudioAiBuildPlan plan)
    {
        var title = "(sans titre)";
        var names = new List<string>();
        try
        {
            using var summary = JsonDocument.Parse(plan.SummaryJson);
            if (summary.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (summary.RootElement.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(t.GetString()))
                    title = t.GetString()!.Trim();

                if (summary.RootElement.TryGetProperty("entities", out var ents) && ents.ValueKind == JsonValueKind.Array)
                {
                    foreach (var e in ents.EnumerateArray())
                    {
                        if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty("displayName", out var dn)
                            && dn.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(dn.GetString()))
                            names.Add(dn.GetString()!.Trim());
                    }
                }
            }
        }
        catch (JsonException)
        {
            // SummaryJson illisible : on garde le titre par défaut.
        }

        if (plan.Status == StudioAiPlanStatus.Completed && !string.IsNullOrWhiteSpace(plan.ResultJson))
        {
            try
            {
                using var result = JsonDocument.Parse(plan.ResultJson);
                var root = result.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    var keys = new List<string>();
                    if (root.TryGetProperty("entities", out var ents) && ents.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var e in ents.EnumerateArray())
                        {
                            if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty("entityKey", out var k)
                                && k.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(k.GetString()))
                                keys.Add(k.GetString()!);
                        }
                    }
                    else if (root.TryGetProperty("entityKey", out var single) && single.ValueKind == JsonValueKind.String
                             && !string.IsNullOrWhiteSpace(single.GetString()))
                    {
                        keys.Add(single.GetString()!);
                    }
                    if (keys.Count > 0)
                        names = keys;
                }
            }
            catch (JsonException)
            {
                // ResultJson illisible : les libellés de l'aperçu suffisent.
            }
        }

        return (title, names);
    }

    private static string Truncate(string text, int maxChars)
    {
        if (maxChars <= 0) return string.Empty;
        if (text.Length <= maxChars) return text;
        return maxChars == 1 ? Ellipsis : text[..(maxChars - 1)].TrimEnd() + Ellipsis;
    }

    /// <summary>Lignes non tronquées + nombre de tables au-delà de <see cref="MaxEntities"/> (entrée de cache).</summary>
    public sealed record SchemaLines(IReadOnlyList<string> Lines, int OmittedCount);
}
