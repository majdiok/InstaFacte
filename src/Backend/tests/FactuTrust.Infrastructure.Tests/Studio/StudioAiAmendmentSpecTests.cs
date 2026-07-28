using FactuTrust.Application.Features.Studio.Ai;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

/// <summary>
/// Parsing des demandes de MODIFICATION. Même exigence que les autres specs Studio : tolérant aux
/// approximations du petit modèle, mais strict sur la liste blanche d'opérations (ni suppression de
/// table, ni suppression de système).
/// </summary>
public sealed class StudioAiAmendmentSpecTests
{
    [Fact]
    public void Parses_the_axelor_style_add_field_request()
    {
        const string json = """
        { "target": { "entityKey": "contrats" },
          "operations": [ { "op": "add_field", "label": "Motif de refus", "type": "multilinetext" } ] }
        """;

        Assert.True(StudioAiAmendmentSpec.TryParse(json, out var spec, out var error), error);
        Assert.Equal("contrats", spec!.TargetEntityRef);
        var add = Assert.IsType<AddFieldOp>(Assert.Single(spec.Operations));
        Assert.Equal("Motif de refus", add.Field.Label);
        Assert.Equal("motif_de_refus", add.Field.Key);
        Assert.Equal(CustomFieldType.MultilineText, add.Field.FieldType);
    }

    [Fact]
    public void Parses_every_whitelisted_operation()
    {
        const string json = """
        { "target": { "entityKey": "contrats" }, "operations": [
          { "op": "add_field", "label": "Note", "type": "rating", "max": 4 },
          { "op": "update_field", "key": "statut", "required": true, "addOptions": ["resilie"] },
          { "op": "remove_field", "key": "obsolete" },
          { "op": "update_entity", "displayName": "Contrats clients" },
          { "op": "set_form", "sections": [ { "title": "Général", "fields": ["statut"] } ] },
          { "op": "set_report", "displayName": "Par statut", "definition": { "groupBy": ["statut"] } }
        ] }
        """;

        Assert.True(StudioAiAmendmentSpec.TryParse(json, out var spec, out var error), error);
        Assert.Equal(6, spec!.Operations.Count);
        Assert.IsType<AddFieldOp>(spec.Operations[0]);
        var upd = Assert.IsType<UpdateFieldOp>(spec.Operations[1]);
        Assert.True(upd.Required);
        Assert.Single(upd.AddOptions!);
        Assert.IsType<RemoveFieldOp>(spec.Operations[2]);
        Assert.Equal("Contrats clients", Assert.IsType<UpdateEntityOp>(spec.Operations[3]).DisplayName);
        Assert.IsType<SetFormOp>(spec.Operations[4]);
        Assert.Equal("Par statut", Assert.IsType<SetReportOp>(spec.Operations[5]).DisplayName);
    }

    [Fact]
    public void Unknown_operations_are_dropped_with_a_warning_not_a_failure()
    {
        const string json = """
        { "target": { "entityKey": "contrats" }, "operations": [
          { "op": "drop_table" },
          { "op": "delete_system" },
          { "op": "add_field", "label": "Note", "type": "text" }
        ] }
        """;

        Assert.True(StudioAiAmendmentSpec.TryParse(json, out var spec, out var error), error);
        Assert.Single(spec!.Operations);
        Assert.Equal(2, spec.Warnings.Count);
        Assert.All(spec.Warnings, w => Assert.Contains("non prise en charge", w));
    }

    [Fact]
    public void Operations_are_capped()
    {
        var ops = string.Join(",", Enumerable.Range(0, StudioAiAmendmentSpec.MaxOperations + 5)
            .Select(i => $$"""{ "op": "add_field", "label": "Champ {{i}}", "type": "text" }"""));
        var json = $$"""{ "target": { "entityKey": "t" }, "operations": [ {{ops}} ] }""";

        Assert.True(StudioAiAmendmentSpec.TryParse(json, out var spec, out var error), error);
        Assert.Equal(StudioAiAmendmentSpec.MaxOperations, spec!.Operations.Count);
        Assert.Contains(spec.Warnings, w => w.Contains("ignorées"));
    }

    [Fact]
    public void Field_reference_accepts_key_or_label()
    {
        const string json = """
        { "target": { "entityKey": "t" }, "operations": [
          { "op": "remove_field", "label": "Date de début" },
          { "op": "update_field", "field": "montant", "label": "Montant TTC" }
        ] }
        """;

        Assert.True(StudioAiAmendmentSpec.TryParse(json, out var spec, out var error), error);
        Assert.Equal("Date de début", Assert.IsType<RemoveFieldOp>(spec!.Operations[0]).FieldRef);
        Assert.Equal("montant", Assert.IsType<UpdateFieldOp>(spec.Operations[1]).FieldRef);
    }

    [Fact]
    public void Report_definition_posed_inline_is_tolerated()
    {
        const string json = """
        { "target": { "entityKey": "t" }, "operations": [
          { "op": "set_report", "displayName": "Synthèse", "groupBy": ["statut"], "measures": [ { "field": "montant", "fn": "sum" } ] }
        ] }
        """;

        Assert.True(StudioAiAmendmentSpec.TryParse(json, out var spec, out var error), error);
        var report = Assert.IsType<SetReportOp>(Assert.Single(spec!.Operations));
        Assert.Equal("Synthèse", report.DisplayName);
        Assert.NotNull(report.ReportNode["groupBy"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{ \"operations\": [ { \"op\": \"add_field\", \"label\": \"X\" } ] }")]
    [InlineData("{ \"target\": { \"entityKey\": \"t\" }, \"operations\": [] }")]
    [InlineData("{ \"target\": { \"entityKey\": \"t\" }, \"operations\": [ { \"op\": \"drop_table\" } ] }")]
    public void Invalid_specs_fail(string json)
    {
        Assert.False(StudioAiAmendmentSpec.TryParse(json, out var spec, out var error));
        Assert.Null(spec);
        Assert.NotNull(error);
    }
}
