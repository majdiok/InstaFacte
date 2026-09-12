using System.Text.Json.Nodes;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Features.Studio.Ai;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.Entities;
using FactuTrust.Application.Features.Studio.Fields;
using FactuTrust.Application.Features.Studio.Forms;
using FactuTrust.Application.Features.Studio.Records;
using FactuTrust.Application.Features.Studio.Reports;
using FactuTrust.Application.Features.Studio.Systems;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Infrastructure.Services.Studio;

public sealed class StudioAiSystemOrchestrator
{
    private readonly IMediator _mediator;
    private readonly ICurrentUser _currentUser;
    private readonly IStudioQuotaService? _quota;

    public StudioAiSystemOrchestrator(IMediator mediator, ICurrentUser currentUser, IStudioQuotaService? quota = null)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _quota = quota;
    }

    public async Task<(bool Success, string? Error, object? Payload)> ExecuteAsync(
        ParsedSystemSpec spec,
        IStudioBuildProgress? progress,
        CancellationToken ct)
    {
        var journal = new StudioBuildJournal();
        IStudioBuildProgress progressReporter = progress ?? journal;
        void Report(string phase, string label, string status, string? entityRef = null, string? detail = null) =>
            progressReporter.Report(new StudioBuildStep(phase, label, status, entityRef, detail));

        var warnings = new List<string>();
        try
        {
            // Pré-vérification de quota : refuser AVANT toute création si les N tables NOUVELLES du
            // système ne tiennent pas dans le quota restant. Une table réutilisée (existingKey) n'est
            // pas créée : elle ne consomme aucun quota.
            var newEntityCount = spec.Entities.Count(e => e.ExistingKey is null);
            if (_quota is not null && StudioContext.TryGet(_currentUser, out var quotaTenantId, out _, out _))
            {
                var existing = await _mediator.Send(new ListCustomEntitiesQuery(true), ct);
                var currentCount = existing.IsSuccess ? existing.Value.Count : 0;
                var quotaResult = await _quota.EnsureUnderLimitAsync(quotaTenantId, StudioQuotas.MaxEntitiesKey,
                    currentCount + newEntityCount - 1, StudioQuotas.MaxEntitiesFallback, "tables personnalisées", ct);
                if (!quotaResult.IsSuccess)
                {
                    Report("failed", "Échec – quota", "error", null, quotaResult.Error.Description);
                    return (false, quotaResult.Error.Description, null);
                }
            }

            // Résolution des tables réutilisées AVANT toute écriture : si une clé demandée est
            // introuvable ou inactive, on refuse SANS avoir créé le système (pas d'orphelin à
            // annuler). Le message ne cite que la clé demandée — jamais les clés du tenant.
            var entityKeyMap = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var reusedSpec in spec.Entities.Where(e => e.ExistingKey is not null))
            {
                Report("reusing_entity", $"Table existante « {reusedSpec.EntityDisplayName} »", "running", reusedSpec.Ref);
                var schema = await _mediator.Send(new GetCustomEntitySchemaQuery(reusedSpec.ExistingKey!), ct);
                if (!schema.IsSuccess || !schema.Value.Entity.IsActive)
                {
                    var reason = $"Table existante « {reusedSpec.ExistingKey} » introuvable ou inactive — la réutilisation demandée est impossible.";
                    Report("reusing_entity", $"Table existante « {reusedSpec.EntityDisplayName} »", "error", reusedSpec.Ref, reason);
                    return (false, reason, null);
                }
                entityKeyMap[reusedSpec.Ref] = reusedSpec.ExistingKey!;
                Report("reusing_entity", $"Table existante « {reusedSpec.EntityDisplayName} »", "done", reusedSpec.Ref);
            }

            Report("creating_system", "Création du système", "running");
            var systemKey = await UniqueSystemKeyAsync(spec.SystemDisplayName, ct);
            var systemResult = await _mediator.Send(new CreateCustomSystemCommand(
                new CreateCustomSystemRequest(systemKey, spec.SystemDisplayName, spec.SystemIcon,
                    spec.SystemDescription, spec.OnboardingSteps)), ct);
            if (!systemResult.IsSuccess)
                return Fail(systemResult.Error.Description, journal, ct);
            journal.SystemId = systemResult.Value.Id;
            journal.SystemKey = systemResult.Value.Key;
            Report("creating_system", "Création du système", "done");

            foreach (var entitySpec in spec.Entities)
            {
                // Table réutilisée : AUCUNE écriture (ni entité, ni champ, ni formulaire, ni état) et
                // surtout pas de journal — l'annulation ne doit jamais supprimer une table existante.
                if (entitySpec.ExistingKey is not null) continue;

                Report("creating_entity", $"Table « {entitySpec.EntityDisplayName} »", "running", entitySpec.Ref);
                var existing = await _mediator.Send(new ListCustomEntitiesQuery(true), ct);
                var usedKeys = existing.IsSuccess
                    ? existing.Value.Select(e => e.Key).ToHashSet(StringComparer.Ordinal)
                    : new HashSet<string>(StringComparer.Ordinal);
                var entityKey = UniqueEntityKey(entitySpec.EntityDisplayName, usedKeys);

                var entityResult = await _mediator.Send(new CreateCustomEntityCommand(
                    new CreateCustomEntityRequest(entityKey, entitySpec.EntityDisplayName,
                        entitySpec.EntityDisplayNamePlural, entitySpec.Icon, entitySpec.Description,
                        journal.SystemId)), ct);
                if (!entityResult.IsSuccess)
                    return await RollbackAsync(entityResult.Error.Description, journal, Report, ct);

                var entity = entityResult.Value;
                journal.EntityIds.Add(entity.Id);
                entityKeyMap[entitySpec.Ref] = entity.Key;

                var simpleFields = entitySpec.Fields.Where(f => f.FieldType != CustomFieldType.RelationCustom).ToList();
                var relationFields = entitySpec.Fields.Where(f => f.FieldType == CustomFieldType.RelationCustom).ToList();

                Report("creating_fields", $"Champs de « {entitySpec.EntityDisplayName} »", "running", entitySpec.Ref);
                foreach (var f in simpleFields)
                {
                    var fr = await CreateFieldAsync(entity.Id, f, entityKeyMap, ct);
                    if (!fr.IsSuccess)
                        warnings.Add($"Champ « {f.Label} » ignoré : {fr.Error.Description}");
                }
                Report("creating_fields", $"Champs de « {entitySpec.EntityDisplayName} »", "done", entitySpec.Ref);

                foreach (var f in relationFields)
                {
                    Report("creating_relations", $"Relation « {f.Label} »", "running", entitySpec.Ref);
                    var fr = await CreateFieldAsync(entity.Id, f, entityKeyMap, ct);
                    if (!fr.IsSuccess)
                    {
                        warnings.Add($"Relation « {f.Label} » ignorée : {fr.Error.Description}");
                        Report("creating_relations", $"Relation « {f.Label} »", "error", entitySpec.Ref, fr.Error.Description);
                        continue;
                    }
                    Report("creating_relations", $"Relation « {f.Label} »", "done", entitySpec.Ref);
                }

                if (entitySpec.Form is not null && _currentUser.HasPermission(Permissions.Studio.DesignForms))
                {
                    Report("creating_form", $"Formulaire « {entitySpec.EntityDisplayName} »", "running", entitySpec.Ref);
                    var layout = BuildFormLayout(entitySpec.Form);
                    var formResult = await _mediator.Send(new UpsertDefaultFormCommand(entity.Id,
                        new SaveFormLayoutRequest(layout, null)), ct);
                    if (!formResult.IsSuccess)
                    {
                        warnings.Add($"Formulaire « {entitySpec.EntityDisplayName} » ignoré : {formResult.Error.Description}");
                        Report("creating_form", $"Formulaire « {entitySpec.EntityDisplayName} »", "error", entitySpec.Ref, formResult.Error.Description);
                    }
                    else
                        Report("creating_form", $"Formulaire « {entitySpec.EntityDisplayName} »", "done", entitySpec.Ref);
                }

                if (entitySpec.Report is not null && _currentUser.HasPermission(Permissions.Studio.DesignReports))
                {
                    Report("creating_report", $"Rapport « {entitySpec.Report.DisplayName} »", "running", entitySpec.Ref);
                    var def = new ReportDefinition
                    {
                        Fields = entitySpec.Report.Fields,
                        Filters = entitySpec.Report.Filters,
                        Grouping = entitySpec.Report.Grouping,
                        Aggregations = entitySpec.Report.Aggregations,
                        Sort = entitySpec.Report.Sort
                    };
                    var rr = await _mediator.Send(new UpsertCustomReportCommand(null,
                        new SaveCustomReportRequest(null, entitySpec.Report.DisplayName,
                            CustomReportDataSourceKind.CustomEntity, entity.Key, def)), ct);
                    if (!rr.IsSuccess)
                    {
                        warnings.Add($"Rapport « {entitySpec.Report.DisplayName} » ignoré : {rr.Error.Description}");
                        Report("creating_report", $"Rapport « {entitySpec.Report.DisplayName} »", "error", entitySpec.Ref, rr.Error.Description);
                    }
                    else
                        Report("creating_report", $"Rapport « {entitySpec.Report.DisplayName} »", "done", entitySpec.Ref);
                }

                Report("creating_entity", $"Table « {entitySpec.EntityDisplayName} »", "done", entitySpec.Ref);
            }

            if (spec.Seed.Count > 0 && _currentUser.HasPermission(Permissions.CustomData.RecordsWrite))
            {
                Report("seeding_data", "Pré-remplissage des données de référence", "running");
                var entityByRef = spec.Entities.ToDictionary(e => e.Ref, StringComparer.Ordinal);
                foreach (var batch in spec.Seed)
                {
                    if (!entityKeyMap.TryGetValue(batch.EntityRef, out var entityKey)) continue;
                    if (!entityByRef.TryGetValue(batch.EntityRef, out var entitySpec)) continue;
                    // Jamais d'écriture dans une table réutilisée : le lot de seed est ignoré.
                    if (entitySpec.ExistingKey is not null)
                    {
                        warnings.Add($"Données de « {entitySpec.EntityDisplayName} » ignorées : table existante réutilisée, aucune écriture.");
                        continue;
                    }
                    var fieldByKey = entitySpec.Fields.ToDictionary(f => f.Key, StringComparer.Ordinal);

                    foreach (var rec in batch.Records)
                    {
                        // Map each seed key to a real field key (fuzzy: camelCase vs snake_case); drop unknown
                        // keys and relation values that aren't a GUID (the small model emits human labels, not
                        // record ids) so the record still saves — tolerant seeding, never a hard failure.
                        var data = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
                        foreach (var kv in rec)
                        {
                            var fieldKey = ResolveSeedFieldKey(entitySpec, kv.Key);
                            if (fieldKey is null || !fieldByKey.TryGetValue(fieldKey, out var field)) continue;
                            if (field.FieldType == CustomFieldType.RelationCustom
                                && !Guid.TryParse(AsRawString(kv.Value), out _)) continue;
                            data[fieldKey] = kv.Value?.DeepClone();
                        }
                        if (data.Count == 0) continue;
                        var seedResult = await _mediator.Send(new CreateCustomRecordCommand(entityKey,
                            new SaveCustomRecordRequest(data)), ct);
                        if (!seedResult.IsSuccess)
                            warnings.Add($"Donnée de « {entitySpec.EntityDisplayName} » ignorée : {seedResult.Error.Description}");
                    }
                }
                Report("seeding_data", "Pré-remplissage des données de référence", "done");
            }

            Report("completed", "Système créé", "done");

            var entities = spec.Entities.Select(e => new
            {
                refKey = e.Ref,
                entityKey = entityKeyMap[e.Ref],
                displayName = e.EntityDisplayName,
                reused = e.ExistingKey is not null,
                openUrl = $"/studio/d/{entityKeyMap[e.Ref]}"
            }).ToList();

            var reusedCount = spec.Entities.Count(e => e.ExistingKey is not null);
            var createdCount = spec.Entities.Count - reusedCount;
            var payload = new
            {
                success = true,
                systemKey = journal.SystemKey,
                systemUrl = $"/studio/systems/{journal.SystemKey}",
                displayName = spec.SystemDisplayName,
                entityCount = spec.Entities.Count,
                createdCount,
                reusedCount,
                entities,
                warnings,
                buildSteps = journal.Steps,
                message = $"Système « {spec.SystemDisplayName} » créé avec {createdCount} table(s)"
                    + (reusedCount > 0 ? $" et {reusedCount} table(s) existante(s) réutilisée(s)." : ".")
                    + (warnings.Count > 0 ? $" {warnings.Count} élément(s) ignoré(s)." : string.Empty)
            };
            return (true, null, payload);
        }
        catch (Exception ex)
        {
            return await RollbackAsync(ex.Message, journal, Report, ct);
        }
    }

    private async Task<Result<CustomFieldDto>> CreateFieldAsync(
        Guid entityId, ParsedSystemField f, Dictionary<string, string> entityKeyMap, CancellationToken ct)
    {
        RelationRefDto? relation = null;
        if (f.FieldType == CustomFieldType.RelationCustom)
        {
            if (f.RelationToRef is null || !entityKeyMap.TryGetValue(f.RelationToRef, out var targetKey))
                return Result.Failure<CustomFieldDto>(
                    Error.Validation("relation", $"Cible de relation « {f.RelationToRef} » introuvable."));
            relation = new RelationRefDto("custom", targetKey);
        }
        else if (f.FieldType == CustomFieldType.RelationExisting)
        {
            // Real ERP link (e.g. clients/products). The field-create command re-validates the source.
            relation = new RelationRefDto("existing", f.RelationToRef ?? string.Empty);
        }

        return await _mediator.Send(new CreateCustomFieldCommand(entityId,
            new CreateCustomFieldRequest(f.Key, f.Label, f.FieldType, f.Required, f.Unique, null, f.Options, relation, f.Config)), ct);
    }

    /// <summary>
    /// Resolves a seed key (as the model wrote it, e.g. camelCase "dateDuConge") to the entity's real
    /// field key (label-derived snake_case "date_du_conge"). Comparison ignores case and separators so
    /// the small model's loose key naming still lands on the right field.
    /// </summary>
    private static string? ResolveSeedFieldKey(ParsedSystemEntity entitySpec, string raw)
    {
        var nraw = Normalize(raw);
        if (string.IsNullOrEmpty(nraw)) return null;
        foreach (var f in entitySpec.Fields)
        {
            if (Normalize(f.Key) == nraw || Normalize(StudioAiAppSpec.SlugKey(f.Label)) == nraw)
                return f.Key;
        }
        return null;
    }

    private static string Normalize(string? s) =>
        new string((s ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static string? AsRawString(JsonNode? n)
    {
        if (n is not JsonValue v) return null;
        return v.TryGetValue<string>(out var s) ? s : v.ToString();
    }

    private static FormLayout BuildFormLayout(ParsedFormSpec form)
    {
        var sections = form.Sections.Select(s => new FormSection
        {
            Title = s.Title,
            Fields = s.Fields.Select(f => new FormFieldRef
            {
                Key = f.Key,
                Width = f.Width ?? "full",
                LabelOverride = f.LabelOverride
            }).ToList()
        }).ToList();
        return new FormLayout { Sections = sections };
    }

    private async Task<string> UniqueSystemKeyAsync(string displayName, CancellationToken ct)
    {
        var list = await _mediator.Send(new ListCustomSystemsQuery(true), ct);
        var used = list.IsSuccess
            ? list.Value.Select(s => s.Key).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        return UniqueEntityKey(displayName, used);
    }

    private static string UniqueEntityKey(string displayName, HashSet<string> used)
    {
        var baseKey = StudioAiAppSpec.SlugKey(displayName);
        if (string.IsNullOrEmpty(baseKey) || !StudioKey.IsValidShape(baseKey)) baseKey = "table";
        var key = baseKey;
        var i = 1;
        while (used.Contains(key)) key = $"{baseKey}_{++i}";
        return key;
    }

    private async Task<(bool Success, string? Error, object? Payload)> RollbackAsync(
        string error, StudioBuildJournal journal, Action<string, string, string, string?, string?> report, CancellationToken ct)
    {
        report("failed", "Échec – annulation", "error", null, error);
        foreach (var entityId in journal.EntityIds.AsEnumerable().Reverse())
            await _mediator.Send(new DeleteCustomEntityCommand(entityId), ct);
        if (journal.SystemId is Guid systemId)
            await _mediator.Send(new DeleteCustomSystemCommand(systemId), ct);
        return (false, error, null);
    }

    private static (bool Success, string? Error, object? Payload) Fail(string error, StudioBuildJournal journal, CancellationToken ct) =>
        (false, error, null);

    private sealed class StudioBuildJournal : IStudioBuildProgress
    {
        public Guid? SystemId { get; set; }
        public string? SystemKey { get; set; }
        public List<Guid> EntityIds { get; } = new();
        public List<StudioBuildStep> Steps { get; } = new();

        public void Report(StudioBuildStep step) => Steps.Add(step);
    }
}
