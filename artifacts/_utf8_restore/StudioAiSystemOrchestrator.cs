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

    public StudioAiSystemOrchestrator(IMediator mediator, ICurrentUser currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
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

        try
        {
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

            var entityKeyMap = new Dictionary<string, string>(StringComparer.Ordinal);
            var entityIdMap = new Dictionary<string, Guid>(StringComparer.Ordinal);

            foreach (var entitySpec in spec.Entities)
            {
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
                    return await RollbackAsync(systemResult.Error.Description, journal, Report, ct);

                var entity = entityResult.Value;
                journal.EntityIds.Add(entity.Id);
                entityKeyMap[entitySpec.Ref] = entity.Key;
                entityIdMap[entitySpec.Ref] = entity.Id;

                var simpleFields = entitySpec.Fields.Where(f => f.FieldType != CustomFieldType.RelationCustom).ToList();
                var relationFields = entitySpec.Fields.Where(f => f.FieldType == CustomFieldType.RelationCustom).ToList();

                Report("creating_fields", $"Champs de « {entitySpec.EntityDisplayName} »", "running", entitySpec.Ref);
                foreach (var f in simpleFields)
                {
                    var fr = await CreateFieldAsync(entity.Id, f, entityKeyMap, ct);
                    if (!fr.IsSuccess)
                        return await RollbackAsync(fr.Error.Description, journal, Report, ct);
                }
                Report("creating_fields", $"Champs de « {entitySpec.EntityDisplayName} »", "done", entitySpec.Ref);

                foreach (var f in relationFields)
                {
                    Report("creating_relations", $"Relation « {f.Label} »", "running", entitySpec.Ref);
                    var fr = await CreateFieldAsync(entity.Id, f, entityKeyMap, ct);
                    if (!fr.IsSuccess)
                        return await RollbackAsync(fr.Error.Description, journal, Report, ct);
                    Report("creating_relations", $"Relation « {f.Label} »", "done", entitySpec.Ref);
                }

                if (entitySpec.Form is not null && _currentUser.HasPermission(Permissions.Studio.DesignForms))
                {
                    Report("creating_form", $"Formulaire « {entitySpec.EntityDisplayName} »", "running", entitySpec.Ref);
                    var layout = BuildFormLayout(entitySpec.Form);
                    var formResult = await _mediator.Send(new UpsertDefaultFormCommand(entity.Id,
                        new SaveFormLayoutRequest(layout)), ct);
                    if (!formResult.IsSuccess)
                        return await RollbackAsync(formResult.Error.Description, journal, Report, ct);
                    Report("creating_form", $"Formulaire « {entitySpec.EntityDisplayName} »", "done", entitySpec.Ref);
                }

                if (entitySpec.Report is not null && _currentUser.HasPermission(Permissions.Studio.DesignReports))
                {
                    Report("creating_report", $"Rapport « {entitySpec.Report.DisplayName} »", "running", entitySpec.Ref);
                    var def = new ReportDefinition { Grouping = entitySpec.Report.Grouping, Aggregations = entitySpec.Report.Aggregations };
                    var rr = await _mediator.Send(new UpsertCustomReportCommand(null,
                        new SaveCustomReportRequest(null, entitySpec.Report.DisplayName,
                            CustomReportDataSourceKind.CustomEntity, entity.Key, def)), ct);
                    if (!rr.IsSuccess)
                        return await RollbackAsync(rr.Error.Description, journal, Report, ct);
                    Report("creating_report", $"Rapport « {entitySpec.Report.DisplayName} »", "done", entitySpec.Ref);
                }

                Report("creating_entity", $"Table « {entitySpec.EntityDisplayName} »", "done", entitySpec.Ref);
            }

            if (spec.Seed.Count > 0 && _currentUser.HasPermission(Permissions.CustomData.RecordsWrite))
            {
                Report("seeding_data", "Pré-remplissage des données de référence", "running");
                foreach (var batch in spec.Seed)
                {
                    if (!entityKeyMap.TryGetValue(batch.EntityRef, out var entityKey)) continue;
                    foreach (var rec in batch.Records)
                    {
                        var data = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
                        foreach (var kv in rec)
                            data[StudioAiAppSpec.SlugKey(kv.Key)] = kv.Value?.DeepClone();
                        var seedResult = await _mediator.Send(new CreateCustomRecordCommand(entityKey,
                            new SaveCustomRecordRequest(data)), ct);
                        if (!seedResult.IsSuccess)
                            return await RollbackAsync(seedResult.Error.Description, journal, Report, ct);
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
                openUrl = $"/studio/d/{entityKeyMap[e.Ref]}"
            }).ToList();

            var payload = new
            {
                success = true,
                systemKey = journal.SystemKey,
                systemUrl = $"/studio/systems/{journal.SystemKey}",
                displayName = spec.SystemDisplayName,
                entityCount = spec.Entities.Count,
                entities,
                buildSteps = journal.Steps,
                message = $"Système « {spec.SystemDisplayName} » créé avec {spec.Entities.Count} table(s)."
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

        return await _mediator.Send(new CreateCustomFieldCommand(entityId,
            new CreateCustomFieldRequest(f.Key, f.Label, f.FieldType, f.Required, f.Unique, null, f.Options, relation, f.Config)), ct);
    }

    private static FormLayout BuildFormLayout(ParsedFormSpec form)
    {
        var sections = form.Sections.Select(s => new FormSection
        {
            Title = s.Title,
            Fields = s.FieldKeys.Select(k => new FormFieldRef { Key = k, Width = "full" }).ToList()
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
        report("failed", "Échec — annulation", "error", detail: error);
        foreach (var entityId in journal.EntityIds.AsEnumerable().Reverse())
            await _mediator.Send(new DeleteCustomEntityCommand(entityId), ct);
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
