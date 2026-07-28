using FactuTrust.Application.Features.Studio.Ai;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

/// <summary>
/// Diff « avant → après » présenté à l'utilisateur avant qu'une modification ne s'applique. Le point
/// critique : une opération dont la cible n'existe pas doit DISPARAÎTRE de l'aperçu (avec un
/// avertissement) plutôt que d'être annoncée puis échouer silencieusement à l'exécution.
/// </summary>
public sealed class StudioAiAmendmentPlannerTests
{
    private static CustomEntitySchemaDto Schema() => new(
        new CustomEntityDto(Guid.NewGuid(), "contrats", "Contrat", "Contrats", null, null, true, 3, null,
            DateTime.UtcNow, DateTime.UtcNow),
        new[]
        {
            Field("nom", "Nom", CustomFieldType.Text),
            Field("statut", "Statut", CustomFieldType.Select, options: new[] { new SelectOptionDto("actif", "Actif") }),
            Field("montant", "Montant", CustomFieldType.Money, required: true)
        },
        new FormLayout
        {
            Sections = new[] { new FormSection { Fields = new[] { new FormFieldRef { Key = "nom", Width = "full" } } } }
        });

    private static CustomFieldDto Field(
        string key, string label, CustomFieldType type, bool required = false,
        IReadOnlyList<SelectOptionDto>? options = null) =>
        new(Guid.NewGuid(), key, label, type, required, false, 0, null, options, null, true);

    private static ParsedAmendmentSpec Parse(string json)
    {
        Assert.True(StudioAiAmendmentSpec.TryParse(json, out var spec, out var error), error);
        return spec!;
    }

    [Fact]
    public void Add_field_preview_describes_the_new_field()
    {
        var preview = StudioAiAmendmentPlanner.BuildPreview(Parse("""
        { "target": { "entityKey": "contrats" }, "operations": [
          { "op": "add_field", "label": "Motif de refus", "type": "multilinetext", "required": true } ] }
        """), Schema());

        var item = Assert.Single(preview.Items);
        Assert.Equal("add_field", item.Op);
        Assert.Equal("Motif de refus", item.Target);
        Assert.Null(item.Before);
        Assert.Contains("texte long", item.After);
        Assert.Contains("obligatoire", item.After);
    }

    [Fact]
    public void Remove_field_warns_that_data_is_preserved()
    {
        var preview = StudioAiAmendmentPlanner.BuildPreview(Parse("""
        { "target": { "entityKey": "contrats" }, "operations": [ { "op": "remove_field", "key": "statut" } ] }
        """), Schema());

        var item = Assert.Single(preview.Items);
        Assert.Equal("warning", item.Severity);
        Assert.Contains("CONSERVÉES", item.Warning);
        Assert.Equal("Statut", item.Target);
    }

    [Fact]
    public void Operation_on_an_unknown_field_is_dropped_with_a_warning()
    {
        var preview = StudioAiAmendmentPlanner.BuildPreview(Parse("""
        { "target": { "entityKey": "contrats" }, "operations": [
          { "op": "remove_field", "key": "champ_fantome" },
          { "op": "update_field", "key": "autre_fantome", "required": true } ] }
        """), Schema());

        Assert.Empty(preview.Items);
        Assert.Equal(2, preview.Warnings.Count);
        Assert.All(preview.Warnings, w => Assert.Contains("introuvable", w));
    }

    [Fact]
    public void Field_reference_resolves_by_label_as_well_as_key()
    {
        var preview = StudioAiAmendmentPlanner.BuildPreview(Parse("""
        { "target": { "entityKey": "contrats" }, "operations": [ { "op": "update_field", "label": "Montant" } ] }
        """), Schema());

        var item = Assert.Single(preview.Items);
        Assert.Equal("Montant", item.Target);
        Assert.Contains("montant", item.Before);      // type « montant » conservé
        Assert.Contains("obligatoire", item.Before);  // état actuel reflété
    }

    [Fact]
    public void Set_form_preview_flags_the_layout_replacement()
    {
        var preview = StudioAiAmendmentPlanner.BuildPreview(Parse("""
        { "target": { "entityKey": "contrats" }, "operations": [
          { "op": "set_form", "sections": [ { "title": "Identité", "fields": [ "nom", { "field": "montant", "width": "half" } ] } ] } ] }
        """), Schema());

        var item = Assert.Single(preview.Items);
        Assert.Equal("warning", item.Severity);
        Assert.Contains("Identité (2)", item.After);
        Assert.Contains("remplacée", item.Warning);
    }

    [Fact]
    public void Resolved_form_keeps_widths_and_drops_unknown_fields()
    {
        var spec = Parse("""
        { "target": { "entityKey": "contrats" }, "operations": [
          { "op": "set_form", "sections": [ { "fields": [
            "Nom",
            { "field": "montant", "width": "half", "label": "Montant TTC" },
            { "field": "inexistant", "width": "half" } ] } ] } ] }
        """);
        var op = Assert.IsType<SetFormOp>(Assert.Single(spec.Operations));

        var layout = StudioAiAmendmentPlanner.ResolveForm(op.FormNode, Schema().Fields);

        Assert.NotNull(layout);
        var fields = layout!.Sections[0].Fields;
        Assert.Equal(2, fields.Count);                     // champ inconnu écarté
        Assert.Equal("nom", fields[0].Key);                // résolu par libellé
        Assert.Equal("full", fields[0].Width);
        Assert.Equal("half", fields[1].Width);
        Assert.Equal("Montant TTC", fields[1].LabelOverride);
    }

    [Fact]
    public void Resolved_report_maps_labels_to_real_field_keys()
    {
        var spec = Parse("""
        { "target": { "entityKey": "contrats" }, "operations": [
          { "op": "set_report", "displayName": "Par statut", "definition": {
            "groupBy": ["Statut"], "measures": [ { "field": "Montant", "fn": "sum" } ],
            "filters": [ { "field": "Statut", "op": "eq", "value": "actif" } ],
            "sort": [ { "field": "Montant", "dir": "desc" } ] } } ] }
        """);
        var op = Assert.IsType<SetReportOp>(Assert.Single(spec.Operations));

        var def = StudioAiAmendmentPlanner.ResolveReport(op.ReportNode, Schema().Fields);

        Assert.NotNull(def);
        Assert.Equal(new[] { "statut" }, def!.Grouping);
        Assert.Equal("montant", def.Aggregations[0].Field);
        Assert.Equal("statut", def.Filters[0].Field);
        Assert.Equal("montant", def.Sort[0].Field);
        Assert.Equal("desc", def.Sort[0].Dir);
    }

    [Fact]
    public void Summary_json_carries_the_steps_and_the_destructive_warnings()
    {
        var preview = StudioAiAmendmentPlanner.BuildPreview(Parse("""
        { "target": { "entityKey": "contrats" }, "operations": [
          { "op": "add_field", "label": "Motif de refus", "type": "text" },
          { "op": "remove_field", "key": "statut" } ] }
        """), Schema());

        var json = StudioAiAmendmentPlanner.ToSummaryJson(preview);

        Assert.Contains("Amendment", json);
        Assert.Contains("Modification de", json);
        Assert.Contains("Ajouter", json);
        Assert.Contains("Retirer", json);
        Assert.Contains("CONSERV", json);
    }
}
