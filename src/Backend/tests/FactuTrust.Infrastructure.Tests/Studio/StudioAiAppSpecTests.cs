using FactuTrust.Application.Features.Studio.Ai;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

public sealed class StudioAiAppSpecTests
{
    [Fact]
    public void Parses_entity_fields_types_and_report()
    {
        const string json = """
        {
          "entity": { "displayName": "Contrats clients", "icon": "fa-solid fa-file-contract" },
          "fields": [
            { "label": "Date de début", "type": "date", "required": true },
            { "label": "Date de fin", "type": "date" },
            { "label": "Montant", "type": "money" },
            { "label": "Statut", "type": "select", "options": [ {"value":"actif","label":"Actif"}, {"value":"expire","label":"Expiré"} ] }
          ],
          "report": { "displayName": "Contrats par statut", "groupBy": ["Statut"], "measures": [ {"field":"Montant","fn":"sum"} ] }
        }
        """;

        Assert.True(StudioAiAppSpec.TryParse(json, out var spec, out var error));
        Assert.Null(error);
        Assert.NotNull(spec);
        Assert.Equal("Contrats clients", spec!.EntityDisplayName);
        Assert.Equal(4, spec.Fields.Count);

        Assert.Equal(CustomFieldType.Date, spec.Fields[0].FieldType);
        Assert.True(spec.Fields[0].Required);
        Assert.Equal(CustomFieldType.Money, spec.Fields[2].FieldType);

        var statut = spec.Fields[3];
        Assert.Equal(CustomFieldType.Select, statut.FieldType);
        Assert.NotNull(statut.Options);
        Assert.Equal(2, statut.Options!.Count);

        // Keys are slugified and unique.
        Assert.Equal("date_de_debut", spec.Fields[0].Key);
        Assert.Equal("statut", statut.Key);

        // Report references resolved by label → field key.
        Assert.NotNull(spec.Report);
        Assert.Contains("statut", spec.Report!.Grouping);
        Assert.Single(spec.Report.Aggregations);
        Assert.Equal("montant", spec.Report.Aggregations[0].Field);
        Assert.Equal("sum", spec.Report.Aggregations[0].Fn);
    }

    [Fact]
    public void Select_without_options_degrades_to_text()
    {
        const string json = """
        { "entity": { "displayName": "T" }, "fields": [ { "label": "Catégorie", "type": "select" } ] }
        """;
        Assert.True(StudioAiAppSpec.TryParse(json, out var spec, out _));
        Assert.Equal(CustomFieldType.Text, spec!.Fields[0].FieldType);
    }

    [Fact]
    public void Unknown_type_degrades_to_text_and_relation_types_are_not_accepted()
    {
        const string json = """
        { "entity": { "displayName": "T" }, "fields": [
          { "label": "Truc", "type": "wibble" },
          { "label": "Lien", "type": "RelationCustom" }
        ] }
        """;
        Assert.True(StudioAiAppSpec.TryParse(json, out var spec, out _));
        Assert.Equal(CustomFieldType.Text, spec!.Fields[0].FieldType);
        Assert.Equal(CustomFieldType.Text, spec.Fields[1].FieldType); // relation not allowed for AI generation
    }

    [Fact]
    public void Duplicate_labels_produce_unique_keys()
    {
        const string json = """
        { "entity": { "displayName": "T" }, "fields": [
          { "label": "Nom", "type": "text" },
          { "label": "Nom", "type": "text" }
        ] }
        """;
        Assert.True(StudioAiAppSpec.TryParse(json, out var spec, out _));
        Assert.Equal("nom", spec!.Fields[0].Key);
        Assert.Equal("nom_2", spec.Fields[1].Key);
    }

    [Fact]
    public void Money_and_rating_carry_config()
    {
        const string json = """
        { "entity": { "displayName": "T" }, "fields": [
          { "label": "Prix", "type": "money", "currency": "eur" },
          { "label": "Avis", "type": "rating", "max": 4 }
        ] }
        """;
        Assert.True(StudioAiAppSpec.TryParse(json, out var spec, out _));
        Assert.Equal("EUR", (string?)spec!.Fields[0].Config!["currency"]);
        Assert.Equal(4, (int)spec.Fields[1].Config!["max"]!);
    }

    [Fact]
    public void Report_parses_columns_filters_and_sort()
    {
        const string json = """
        {
          "entity": { "displayName": "Contrats" },
          "fields": [
            { "label": "Nom", "type": "text" },
            { "label": "Montant", "type": "money" },
            { "label": "Statut", "type": "select", "options": [ {"value":"actif"}, {"value":"expire"} ] },
            { "label": "Date de fin", "type": "date" }
          ],
          "report": {
            "displayName": "Contrats actifs",
            "columns": ["Nom", "Montant", "Inconnu"],
            "filters": [
              { "field": "Statut", "op": "eq", "value": "actif" },
              { "field": "Montant", "op": ">=", "value": 100 },
              { "field": "Inconnu", "op": "eq", "value": "x" },
              { "field": "Nom", "op": "wibble", "value": "x" }
            ],
            "sort": [ { "field": "Date de fin", "dir": "desc" }, "Nom" ]
          }
        }
        """;
        Assert.True(StudioAiAppSpec.TryParse(json, out var spec, out var error), error);
        var report = spec!.Report;
        Assert.NotNull(report);
        Assert.Equal(new[] { "nom", "montant" }, report!.Fields); // colonne inconnue ignorée
        Assert.Equal(2, report.Filters.Count);                    // champ inconnu + op hors liste ignorés
        Assert.Equal("eq", report.Filters[0].Op);
        Assert.Equal("gte", report.Filters[1].Op);                // alias « >= » normalisé
        Assert.Equal(2, report.Sort.Count);
        Assert.Equal("date_de_fin", report.Sort[0].Field);
        Assert.Equal("desc", report.Sort[0].Dir);
        Assert.Equal("nom", report.Sort[1].Field);                // entrée chaîne tolérée
        Assert.Equal("asc", report.Sort[1].Dir);
        Assert.Empty(report.Grouping);                            // rapport de détail : pas de count implicite
        Assert.Empty(report.Aggregations);
    }

    [Fact]
    public void Report_between_needs_two_values_and_scalar_in_becomes_eq()
    {
        const string json = """
        {
          "entity": { "displayName": "T" },
          "fields": [ { "label": "Montant", "type": "money" }, { "label": "Statut", "type": "text" } ],
          "report": {
            "filters": [
              { "field": "Montant", "op": "between", "value": 10 },
              { "field": "Montant", "op": "between", "value": 10, "value2": 20 },
              { "field": "Statut", "op": "in", "value": "actif" }
            ]
          }
        }
        """;
        Assert.True(StudioAiAppSpec.TryParse(json, out var spec, out var error), error);
        var filters = spec!.Report!.Filters;
        Assert.Equal(2, filters.Count);        // between sans value2 ignoré
        Assert.Equal("between", filters[0].Op);
        Assert.NotNull(filters[0].Value2);
        Assert.Equal("eq", filters[1].Op);     // `in` scalaire toléré → eq
    }

    [Fact]
    public void Report_sort_resolves_aggregate_key_with_string_direction()
    {
        const string json = """
        {
          "entity": { "displayName": "T" },
          "fields": [ { "label": "Montant", "type": "money" }, { "label": "Statut", "type": "text" } ],
          "report": { "groupBy": ["Statut"], "measures": [ { "field": "Montant", "fn": "sum" } ],
            "sort": [ "sum_montant desc" ] }
        }
        """;
        Assert.True(StudioAiAppSpec.TryParse(json, out var spec, out var error), error);
        var report = spec!.Report!;
        Assert.Single(report.Sort);
        Assert.Equal("sum_montant", report.Sort[0].Field); // colonne d'agrégat du rapport groupé
        Assert.Equal("desc", report.Sort[0].Dir);
    }

    [Fact]
    public void Legacy_report_without_new_attributes_is_unchanged()
    {
        const string json = """
        { "entity": { "displayName": "T" }, "fields": [ { "label": "Statut", "type": "text" } ],
          "report": { "groupBy": ["Statut"] } }
        """;
        Assert.True(StudioAiAppSpec.TryParse(json, out var spec, out var error), error);
        var report = spec!.Report!;
        Assert.Single(report.Aggregations);                 // count implicite conservé
        Assert.Equal("count", report.Aggregations[0].Fn);
        Assert.Empty(report.Fields);
        Assert.Empty(report.Filters);
        Assert.Empty(report.Sort);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{ \"fields\": [] }")]
    [InlineData("{ \"entity\": { \"displayName\": \"T\" }, \"fields\": [] }")]
    public void Invalid_specs_fail(string json)
    {
        Assert.False(StudioAiAppSpec.TryParse(json, out var spec, out var error));
        Assert.Null(spec);
        Assert.NotNull(error);
    }
}
