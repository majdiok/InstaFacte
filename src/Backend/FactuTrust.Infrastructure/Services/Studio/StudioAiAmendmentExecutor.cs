using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Features.Studio.Ai;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.Entities;
using FactuTrust.Application.Features.Studio.Fields;
using FactuTrust.Application.Features.Studio.Forms;
using FactuTrust.Application.Features.Studio.Reports;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Infrastructure.Services.Studio;

/// <summary>
/// Applique une modification approuvée sur une table Studio existante. Orchestration mince au-dessus
/// des commandes CQRS Studio : aucune logique métier ajoutée (validation, quotas, permissions et audit
/// restent dans les handlers).
///
/// Contrairement à la création, il n'y a PAS d'annulation globale : chaque opération est atomique et
/// déjà auditée individuellement, et l'utilisateur a validé l'aperçu. Une opération en échec est
/// rapportée puis on poursuit avec les suivantes ; le rapport final liste ce qui est passé et ce qui
/// ne l'est pas. Le schéma réel est RELU ici : ce que l'exécution résout est l'état courant, pas
/// l'instantané du moment de l'aperçu.
/// </summary>
public sealed class StudioAiAmendmentExecutor
{
    private readonly IMediator _mediator;
    private readonly ICurrentUser _currentUser;

    public StudioAiAmendmentExecutor(IMediator mediator, ICurrentUser currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    public async Task<(bool Success, string? Error, object? Payload)> ExecuteAsync(
        ParsedAmendmentSpec spec, IStudioBuildProgress? progress, CancellationToken ct)
    {
        void Report(string phase, string label, string status, string? detail = null) =>
            progress?.Report(new StudioBuildStep(phase, label, status, spec.TargetEntityRef, detail));

        Report("loading_schema", "Lecture de la table", "running");
        var schemaResult = await _mediator.Send(new GetCustomEntitySchemaQuery(spec.TargetEntityRef), ct);
        if (!schemaResult.IsSuccess)
        {
            Report("failed", "Table introuvable", "error", schemaResult.Error.Description);
            return (false, $"Table « {spec.TargetEntityRef} » introuvable : {schemaResult.Error.Description}", null);
        }

        var schema = schemaResult.Value;
        var entityId = schema.Entity.Id;
        var fields = schema.Fields.Where(f => f.IsActive).ToList();
        var usedKeys = schema.Fields.Select(f => f.Key).ToHashSet(StringComparer.Ordinal);
        Report("loading_schema", "Lecture de la table", "done");

        var applied = new List<string>();
        var warnings = new List<string>(spec.Warnings);

        foreach (var op in spec.Operations)
        {
            switch (op)
            {
                case AddFieldOp add:
                    await ApplyAddFieldAsync(add, entityId, usedKeys, applied, warnings, Report, ct);
                    break;
                case UpdateFieldOp upd:
                    await ApplyUpdateFieldAsync(upd, fields, applied, warnings, Report, ct);
                    break;
                case RemoveFieldOp rem:
                    await ApplyRemoveFieldAsync(rem, fields, applied, warnings, Report, ct);
                    break;
                case UpdateEntityOp ent:
                    await ApplyUpdateEntityAsync(ent, schema, applied, warnings, Report, ct);
                    break;
                case SetFormOp form:
                    await ApplySetFormAsync(form, entityId, fields, applied, warnings, Report, ct);
                    break;
                case SetReportOp report:
                    await ApplySetReportAsync(report, schema.Entity.Key, fields, applied, warnings, Report, ct);
                    break;
            }
        }

        if (applied.Count == 0)
        {
            var reason = warnings.Count > 0 ? string.Join(" ", warnings) : "Aucune modification applicable.";
            Report("failed", "Aucune modification appliquée", "error", reason);
            return (false, reason, null);
        }

        Report("completed", "Modifications appliquées", "done");

        var payload = new
        {
            success = true,
            entityKey = schema.Entity.Key,
            displayName = schema.Entity.DisplayName,
            appliedCount = applied.Count,
            applied,
            warnings,
            openUrl = $"/studio/d/{schema.Entity.Key}",
            message = $"{applied.Count} modification(s) appliquée(s) sur « {schema.Entity.DisplayName} »."
                + (warnings.Count > 0 ? $" {warnings.Count} élément(s) ignoré(s)." : string.Empty)
        };
        return (true, null, payload);
    }

    private async Task ApplyAddFieldAsync(
        AddFieldOp add, Guid entityId, HashSet<string> usedKeys, List<string> applied, List<string> warnings,
        Action<string, string, string, string?> report, CancellationToken ct)
    {
        var label = add.Field.Label;
        report("adding_field", $"Champ « {label} »", "running", null);

        var key = UniqueKey(add.Field.Key, usedKeys);
        var request = new CreateCustomFieldRequest(key, label, add.Field.FieldType,
            add.Field.Required, add.Field.Unique, null, add.Field.Options, null, add.Field.Config);

        var result = await _mediator.Send(new CreateCustomFieldCommand(entityId, request), ct);
        if (result.IsSuccess)
        {
            usedKeys.Add(key);
            applied.Add($"Champ « {label} » ajouté.");
            report("adding_field", $"Champ « {label} »", "done", null);
        }
        else
        {
            warnings.Add($"Champ « {label} » non ajouté : {result.Error.Description}");
            report("adding_field", $"Champ « {label} »", "error", result.Error.Description);
        }
    }

    private async Task ApplyUpdateFieldAsync(
        UpdateFieldOp op, IReadOnlyList<CustomFieldDto> fields, List<string> applied, List<string> warnings,
        Action<string, string, string, string?> report, CancellationToken ct)
    {
        var target = StudioAiAmendmentPlanner.ResolveField(op.FieldRef, fields);
        if (target is null)
        {
            warnings.Add($"Champ « {op.FieldRef} » introuvable : modification ignorée.");
            return;
        }

        report("updating_field", $"Champ « {target.Label} »", "running", null);

        // Les options ajoutées complètent la liste existante (jamais de remplacement destructif :
        // une valeur déjà saisie dans un enregistrement doit rester interprétable).
        var options = MergeOptions(target.Options, op.AddOptions);
        var request = new UpdateCustomFieldRequest(
            op.Label?.Trim() is { Length: > 0 } newLabel ? newLabel : target.Label,
            op.Required ?? target.IsRequired,
            op.Unique ?? target.IsUnique,
            target.Rules,
            options,
            target.Relation,
            IsActive: true,
            Config: null);

        var result = await _mediator.Send(new UpdateCustomFieldCommand(target.Id, request), ct);
        if (result.IsSuccess)
        {
            applied.Add($"Champ « {target.Label} » modifié.");
            report("updating_field", $"Champ « {target.Label} »", "done", null);
        }
        else
        {
            warnings.Add($"Champ « {target.Label} » non modifié : {result.Error.Description}");
            report("updating_field", $"Champ « {target.Label} »", "error", result.Error.Description);
        }
    }

    private async Task ApplyRemoveFieldAsync(
        RemoveFieldOp op, IReadOnlyList<CustomFieldDto> fields, List<string> applied, List<string> warnings,
        Action<string, string, string, string?> report, CancellationToken ct)
    {
        var target = StudioAiAmendmentPlanner.ResolveField(op.FieldRef, fields);
        if (target is null)
        {
            warnings.Add($"Champ « {op.FieldRef} » introuvable : retrait ignoré.");
            return;
        }

        report("removing_field", $"Champ « {target.Label} »", "running", null);

        // La commande désactive le champ sans toucher aux données déjà saisies.
        var result = await _mediator.Send(new DeleteCustomFieldCommand(target.Id), ct);
        if (result.IsSuccess)
        {
            applied.Add($"Champ « {target.Label} » retiré (données conservées).");
            report("removing_field", $"Champ « {target.Label} »", "done", null);
        }
        else
        {
            warnings.Add($"Champ « {target.Label} » non retiré : {result.Error.Description}");
            report("removing_field", $"Champ « {target.Label} »", "error", result.Error.Description);
        }
    }

    private async Task ApplyUpdateEntityAsync(
        UpdateEntityOp op, CustomEntitySchemaDto schema, List<string> applied, List<string> warnings,
        Action<string, string, string, string?> report, CancellationToken ct)
    {
        report("updating_entity", "Propriétés de la table", "running", null);

        var entity = schema.Entity;
        var request = new UpdateCustomEntityRequest(
            op.DisplayName?.Trim() is { Length: > 0 } name ? name : entity.DisplayName,
            op.DisplayNamePlural?.Trim() is { Length: > 0 } plural ? plural : entity.DisplayNamePlural,
            op.Icon?.Trim() is { Length: > 0 } icon ? icon : entity.Icon,
            op.Description?.Trim() is { Length: > 0 } desc ? desc : entity.Description,
            IsActive: true);

        var result = await _mediator.Send(new UpdateCustomEntityCommand(entity.Id, request), ct);
        if (result.IsSuccess)
        {
            applied.Add("Propriétés de la table mises à jour.");
            report("updating_entity", "Propriétés de la table", "done", null);
        }
        else
        {
            warnings.Add($"Table non modifiée : {result.Error.Description}");
            report("updating_entity", "Propriétés de la table", "error", result.Error.Description);
        }
    }

    private async Task ApplySetFormAsync(
        SetFormOp op, Guid entityId, IReadOnlyList<CustomFieldDto> fields, List<string> applied, List<string> warnings,
        Action<string, string, string, string?> report, CancellationToken ct)
    {
        var layout = StudioAiAmendmentPlanner.ResolveForm(op.FormNode, fields);
        if (layout is null)
        {
            warnings.Add("Formulaire non modifié : aucun champ reconnu dans la mise en page.");
            return;
        }

        report("updating_form", "Formulaire", "running", null);
        var result = await _mediator.Send(new UpsertDefaultFormCommand(entityId, new SaveFormLayoutRequest(layout, null)), ct);
        if (result.IsSuccess)
        {
            applied.Add("Formulaire réorganisé.");
            report("updating_form", "Formulaire", "done", null);
        }
        else
        {
            warnings.Add($"Formulaire non modifié : {result.Error.Description}");
            report("updating_form", "Formulaire", "error", result.Error.Description);
        }
    }

    private async Task ApplySetReportAsync(
        SetReportOp op, string entityKey, IReadOnlyList<CustomFieldDto> fields, List<string> applied, List<string> warnings,
        Action<string, string, string, string?> report, CancellationToken ct)
    {
        if (!_currentUser.HasPermission(Permissions.Studio.DesignReports))
        {
            warnings.Add("État ignoré : permission de conception des rapports absente.");
            return;
        }

        var definition = StudioAiAmendmentPlanner.ResolveReport(op.ReportNode, fields);
        if (definition is null)
        {
            warnings.Add("État ignoré : aucun champ reconnu dans la définition.");
            return;
        }

        var displayName = string.IsNullOrWhiteSpace(op.DisplayName) ? "Rapport" : op.DisplayName!.Trim();
        report("updating_report", $"État « {displayName} »", "running", null);

        var result = await _mediator.Send(new UpsertCustomReportCommand(null,
            new SaveCustomReportRequest(null, displayName, CustomReportDataSourceKind.CustomEntity, entityKey, definition)), ct);
        if (result.IsSuccess)
        {
            applied.Add($"État « {displayName} » créé.");
            report("updating_report", $"État « {displayName} »", "done", null);
        }
        else
        {
            warnings.Add($"État « {displayName} » non créé : {result.Error.Description}");
            report("updating_report", $"État « {displayName} »", "error", result.Error.Description);
        }
    }

    private static IReadOnlyList<SelectOptionDto>? MergeOptions(
        IReadOnlyList<SelectOptionDto>? existing, IReadOnlyList<SelectOptionDto>? added)
    {
        if (added is null || added.Count == 0) return existing;
        var merged = new List<SelectOptionDto>(existing ?? Array.Empty<SelectOptionDto>());
        var seen = merged.Select(o => o.Value).ToHashSet(StringComparer.Ordinal);
        foreach (var o in added)
            if (seen.Add(o.Value)) merged.Add(o);
        return merged;
    }

    private static string UniqueKey(string baseKey, HashSet<string> used)
    {
        if (string.IsNullOrEmpty(baseKey) || !StudioKey.IsValidShape(baseKey)) baseKey = "champ";
        var key = baseKey;
        var i = 1;
        while (used.Contains(key)) key = $"{baseKey}_{++i}";
        return key;
    }
}
