using System.Text.Json.Nodes;
using FactuTrust.Application.Features.Studio.Ai;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

/// <summary>
/// Canonicalisation des specs Studio IA (B-P0-02) : forme unique persistée/exposée à l'éditeur
/// d'aperçu. Invariants couverts : aller-retour parse → canonique → parse stable à l'octet près,
/// clé de champ explicite prioritaire sur la dérivation du libellé, clés réservées/dupliquées
/// rejetées, alias acceptés jamais émis, clés inconnues éliminées (filtre d'écriture massive).
/// </summary>
public sealed class StudioAiSpecCanonicalTests
{
    /// <summary>Spec telle que le LLM la produit aujourd'hui (alias, types FR, formes tolérées).</summary>
    private const string SystemSpecLegacy = """
    {
      "system": { "displayName": "Gestion des congés", "icon": "calendar", "description": "Suivi des congés.",
        "onboarding": ["Ajoutez des employés", "Configurez les types"] },
      "entities": [
        { "ref": "employes", "displayName": "Employés", "fields": [
          { "label": "Nom", "type": "text", "required": true },
          { "label": "Date d'embauche", "type": "date" }
        ] },
        { "ref": "types_conges", "displayName": "Types de congés", "fields": [
          { "label": "Libellé", "type": "text", "unique": true },
          { "label": "Nombre de jours", "type": "number" }
        ] },
        { "ref": "demandes", "displayName": "Demandes", "fields": [
          { "label": "Employé", "type": "relation", "relationTo": "employes" },
          { "label": "Type", "type": "relation", "relationTo": "types_conges" },
          { "label": "Statut", "type": "select", "options": [ {"value":"en_attente","label":"En attente"}, "approuvee" ] },
          { "label": "Commentaire", "type": "texte long" }
        ],
        "form": { "sections": [ { "title": "Demande", "fields": [ "employe", { "field": "statut", "width": "half" } ] } ] },
        "report": { "displayName": "État des congés", "fields": [ "employe", "statut" ],
          "filters": [ { "field": "statut", "op": "different", "value": "brouillon" } ],
          "sort": [ { "field": "statut", "dir": "desc" } ] } }
      ],
      "seed": [ { "entityRef": "types_conges", "records": [ { "libelle": "CP", "nombre_de_jours": 25 }, { "libelle": "RTT" } ] } ]
    }
    """;

    [Fact]
    public void Canonical_of_parse_is_reparsable_and_byte_stable()
    {
        Assert.True(StudioAiSystemSpec.TryParse(SystemSpecLegacy, out var spec, out var error), error);
        var canonical = StudioAiSpecCanonical.CanonicalSystem(spec!);

        Assert.True(StudioAiSystemSpec.TryParse(canonical, out var reparsed, out var reparseError), reparseError);
        var canonicalAgain = StudioAiSpecCanonical.CanonicalSystem(reparsed!);

        Assert.Equal(canonical, canonicalAgain);
    }

    [Theory]
    [InlineData(StudioAiPlanKind.CreateSystem, SystemSpecLegacy)]
    [InlineData(StudioAiPlanKind.CreateApp, """
        { "entity": { "displayName": "Note de frais" }, "fields": [
          { "label": "Montant", "type": "money" },
          { "label": "Justificatif", "type": "attachment" } ],
          "report": { "groupBy": ["montant"], "measures": [ { "field": "montant", "fn": "sum" } ] } }
        """)]
    [InlineData(StudioAiPlanKind.Amendment, """
        { "target": { "entityKey": "employes" }, "operations": [
          { "op": "add_field", "label": "Matricule", "type": "text", "required": true },
          { "op": "update_field", "field": "nom", "label": "Nom de famille" },
          { "op": "remove_field", "field": "ancien_champ" },
          { "op": "update_entity", "displayName": "Salariés" },
          { "op": "set_form", "form": { "sections": [ { "title": "Identité", "fields": ["nom"] } ] } },
          { "op": "set_report", "displayName": "Effectifs", "report": { "groupBy": ["service"] } } ] }
        """)]
    [InlineData(StudioAiPlanKind.View, """
        { "title": "Factures récentes", "table": "Invoices",
          "columns": [ "Number", { "name": "TotalAmount", "label": "Total", "format": "money" } ], "search": false }
        """)]
    [InlineData(StudioAiPlanKind.Report, """
        { "title": "Ventes T1", "source": "Invoices", "groupBy": ["CustomerId"],
          "measures": [ { "field": "TotalAmount", "fn": "sum" } ],
          "filters": [ { "field": "Status", "op": "=", "value": "Paid" } ],
          "sort": [ { "field": "TotalAmount", "dir": "desc" } ], "from": "2026-01-01", "to": "2026-03-31" }
        """)]
    public void CanonicalFor_round_trips_byte_stable_for_every_kind(StudioAiPlanKind kind, string specJson)
    {
        var canonical = StudioAiSpecCanonical.CanonicalFor(kind, specJson, out var error);
        Assert.NotNull(canonical);

        var canonicalAgain = StudioAiSpecCanonical.CanonicalFor(kind, canonical!, out var reparseError);

        Assert.Null(reparseError);
        Assert.Equal(canonical, canonicalAgain);
    }

    [Fact]
    public void CanonicalFor_of_an_invalid_spec_is_null_with_the_parser_error()
    {
        var canonical = StudioAiSpecCanonical.CanonicalFor(StudioAiPlanKind.CreateSystem, "{ pas du json", out var error);

        Assert.Null(canonical);
        Assert.NotNull(error);
    }

    [Fact]
    public void Field_with_explicit_key_keeps_it()
    {
        const string json = """
        { "system": { "displayName": "T" }, "entities": [
          { "ref": "demandes", "displayName": "Demandes", "fields": [
            { "key": "date_debut", "label": "Date de début", "type": "date" } ] } ] }
        """;

        Assert.True(StudioAiSystemSpec.TryParse(json, out var spec, out var error), error);
        var field = spec!.Entities[0].Fields[0];
        Assert.Equal("date_debut", field.Key); // et non « date_de_debut » dérivé du libellé

        var canonical = StudioAiSpecCanonical.CanonicalSystem(spec);
        Assert.Contains("\"key\": \"date_debut\"", canonical, StringComparison.Ordinal);
    }

    [Fact]
    public void Reused_entity_round_trips_as_ref_existingKey_displayName_only()
    {
        const string json = """
        { "system": { "displayName": "T" }, "entities": [
          { "ref": "employes", "existingKey": "employes" },
          { "ref": "demandes", "displayName": "Demandes", "fields": [
            { "label": "Employé", "type": "relation", "relationTo": "employes" } ] }
        ] }
        """;

        Assert.True(StudioAiSystemSpec.TryParse(json, out var spec, out var error), error);
        var canonical = StudioAiSpecCanonical.CanonicalSystem(spec!);

        // L'entité réutilisée n'émet QUE ref/existingKey/displayName : aucun champ, formulaire ou état.
        Assert.Contains("\"existingKey\": \"employes\"", canonical, StringComparison.Ordinal);
        var node = JsonNode.Parse(canonical)!;
        var reused = node["entities"]!.AsArray()
            .First(e => e!["ref"]!.GetValue<string>() == "employes")!;
        Assert.Equal(new[] { "ref", "existingKey", "displayName" },
            reused.AsObject().Select(p => p.Key).ToArray());
        Assert.Equal("employes", reused["displayName"]!.GetValue<string>()); // repli sur la clé

        // Stable à l'octet près au re-parse (l'éditeur d'aperçu ré-émet la même forme).
        var canonicalAgain = StudioAiSpecCanonical.CanonicalFor(StudioAiPlanKind.CreateSystem, canonical, out var reparseError);
        Assert.Null(reparseError);
        Assert.Equal(canonical, canonicalAgain);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("RowVersion")]
    [InlineData("created_at")]
    public void Field_with_reserved_key_is_rejected(string reservedKey)
    {
        var json = $$"""
        { "system": { "displayName": "T" }, "entities": [
          { "ref": "a", "displayName": "A", "fields": [
            { "key": "{{reservedKey}}", "label": "Identifiant", "type": "text" } ] } ] }
        """;

        var ok = StudioAiSystemSpec.TryParse(json, out _, out var error);

        Assert.False(ok);
        Assert.Contains("invalide ou réservée", error);
    }

    [Fact]
    public void Field_with_duplicate_key_is_rejected()
    {
        const string json = """
        { "system": { "displayName": "T" }, "entities": [
          { "ref": "a", "displayName": "A", "fields": [
            { "key": "nom", "label": "Nom", "type": "text" },
            { "key": "nom", "label": "Nom bis", "type": "text" } ] } ] }
        """;

        var ok = StudioAiSystemSpec.TryParse(json, out _, out var error);

        Assert.False(ok);
        Assert.Contains("dupliquée", error);
    }

    [Fact]
    public void App_field_with_explicit_key_keeps_it_and_duplicate_is_rejected()
    {
        const string json = """
        { "entity": { "displayName": "T" }, "fields": [
          { "key": "reference_interne", "label": "Référence", "type": "text" } ] }
        """;
        Assert.True(StudioAiAppSpec.TryParse(json, out var spec, out var error), error);
        Assert.Equal("reference_interne", spec!.Fields[0].Key);

        const string duplicate = """
        { "entity": { "displayName": "T" }, "fields": [
          { "key": "nom", "label": "A" }, { "key": "nom", "label": "B" } ] }
        """;
        Assert.False(StudioAiAppSpec.TryParse(duplicate, out _, out var dupError));
        Assert.Contains("dupliquée", dupError);
    }

    [Fact]
    public void Aliases_are_accepted_but_never_emitted()
    {
        // Alias historiques : « name » pour les libellés, « key » pour la ref d'entité, types FR.
        const string json = """
        { "name": "Système alias",
          "entities": [
            { "key": "employes", "name": "Employés", "fields": [
              { "name": "Nom", "type": "texte" },
              { "name": "Statut", "type": "liste", "options": ["actif", "inactif"] } ] } ] }
        """;

        Assert.True(StudioAiSystemSpec.TryParse(json, out var spec, out var error), error);
        var canonical = StudioAiSpecCanonical.CanonicalSystem(spec!);

        Assert.Contains("\"displayName\": \"Système alias\"", canonical, StringComparison.Ordinal);
        Assert.Contains("\"type\": \"text\"", canonical, StringComparison.Ordinal);
        Assert.Contains("\"type\": \"select\"", canonical, StringComparison.Ordinal);
        Assert.Contains("\"ref\": \"employes\"", canonical, StringComparison.Ordinal);
        Assert.DoesNotContain("\"texte\"", canonical, StringComparison.Ordinal);
        Assert.DoesNotContain("\"liste\"", canonical, StringComparison.Ordinal);
        Assert.DoesNotContain("\"name\":", canonical, StringComparison.Ordinal); // jamais l'alias « name »
        Assert.DoesNotContain("\"entity\":", canonical, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_keys_are_dropped()
    {
        const string json = """
        { "system": { "displayName": "T", "foo": "bar" },
          "inconnu": [1, 2, 3],
          "entities": [
            { "ref": "a", "displayName": "A", "surprise": true, "fields": [
              { "label": "Nom", "type": "text", "weird": { "nested": 1 } } ] } ] }
        """;

        Assert.True(StudioAiSystemSpec.TryParse(json, out var spec, out var error), error);
        var canonical = StudioAiSpecCanonical.CanonicalSystem(spec!);

        Assert.DoesNotContain("foo", canonical, StringComparison.Ordinal);
        Assert.DoesNotContain("inconnu", canonical, StringComparison.Ordinal);
        Assert.DoesNotContain("surprise", canonical, StringComparison.Ordinal);
        Assert.DoesNotContain("weird", canonical, StringComparison.Ordinal);
    }

    [Fact]
    public void Legacy_spec_canonical_form_is_stable()
    {
        // Fixture « produite par le LLM actuel » : la forme canonique doit être stable et relisible.
        var canonical = StudioAiSpecCanonical.CanonicalFor(StudioAiPlanKind.CreateSystem, SystemSpecLegacy, out var error);

        Assert.NotNull(canonical);
        Assert.Null(error);
        var node = JsonNode.Parse(canonical!)!.AsObject();
        Assert.True(node.ContainsKey("system"));
        Assert.True(node.ContainsKey("entities"));
        Assert.True(node.ContainsKey("seed"));

        // L'alias d'opérateur est traduit en opérateur canonique.
        Assert.Contains("\"op\": \"neq\"", canonical, StringComparison.Ordinal);
        Assert.DoesNotContain("different", canonical, StringComparison.Ordinal);
    }

    [Fact]
    public void Canonical_emits_options_as_value_label_objects_and_half_width_form_fields()
    {
        var canonical = StudioAiSpecCanonical.CanonicalFor(StudioAiPlanKind.CreateSystem, SystemSpecLegacy, out _);

        Assert.NotNull(canonical);
        Assert.Contains("\"value\": \"en_attente\"", canonical, StringComparison.Ordinal);
        Assert.Contains("\"label\": \"En attente\"", canonical, StringComparison.Ordinal);
        Assert.Contains("\"width\": \"half\"", canonical, StringComparison.Ordinal);
    }

    [Fact]
    public void Entity_with_more_than_max_fields_is_rejected_outright()
    {
        // 41 champs valides : rejet franc (revue P0) — aucune troncature silencieuse.
        var fields = string.Join(", ", Enumerable.Range(1, StudioAiAppSpec.MaxFields + 1)
            .Select(i => $"{{ \"label\": \"Champ {i}\", \"type\": \"text\" }}"));
        var json = $$"""
        { "system": { "displayName": "T" }, "entities": [
          { "ref": "a", "displayName": "A", "fields": [ {{fields}} ] } ] }
        """;

        var ok = StudioAiSystemSpec.TryParse(json, out var spec, out var error);

        Assert.False(ok);
        Assert.Null(spec);
        Assert.Contains("40", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Entity_with_exactly_max_fields_is_accepted()
    {
        var fields = string.Join(", ", Enumerable.Range(1, StudioAiAppSpec.MaxFields)
            .Select(i => $"{{ \"label\": \"Champ {i}\", \"type\": \"text\" }}"));
        var json = $$"""
        { "system": { "displayName": "T" }, "entities": [
          { "ref": "a", "displayName": "A", "fields": [ {{fields}} ] } ] }
        """;

        Assert.True(StudioAiSystemSpec.TryParse(json, out var spec, out var error), error);
        Assert.Equal(StudioAiAppSpec.MaxFields, spec!.Entities[0].Fields.Count);
    }

    [Fact]
    public void Barcode_field_keeps_its_format_config_in_system_spec()
    {
        const string json = """
        { "system": { "displayName": "T" }, "entities": [
          { "ref": "articles", "displayName": "Articles", "fields": [
            { "key": "code", "label": "Code", "type": "barcode", "config": { "format": "ean13" } } ] } ] }
        """;

        Assert.True(StudioAiSystemSpec.TryParse(json, out var spec, out var error), error);
        var config = spec!.Entities[0].Fields[0].Config;
        Assert.NotNull(config);
        Assert.Equal("ean13", config!["format"]!.GetValue<string>());

        var canonical = StudioAiSpecCanonical.CanonicalSystem(spec);
        Assert.Contains("\"format\": \"ean13\"", canonical, StringComparison.Ordinal);
    }

    [Fact]
    public void Relations_round_trip_byte_stable_and_omit_null_label_and_junction_name()
    {
        const string json = """
        { "system": { "displayName": "T" }, "entities": [
          { "ref": "employes", "displayName": "Employés", "fields": [ { "label": "Nom", "type": "text" } ] },
          { "ref": "formations", "displayName": "Formations", "fields": [ { "label": "Nom", "type": "text" } ] }
        ], "relations": [
          { "kind": "many_to_many", "from": "employes", "to": "formations", "label": "Participants", "junctionName": "Suivis" }
        ] }
        """;
        Assert.True(StudioAiSystemSpec.TryParse(json, out var spec, out var error), error);
        var canonical = StudioAiSpecCanonical.CanonicalSystem(spec!);

        Assert.Contains("\"relations\":", canonical, StringComparison.Ordinal);
        Assert.Contains("\"kind\": \"many_to_many\"", canonical, StringComparison.Ordinal);
        Assert.Contains("\"from\": \"employes\"", canonical, StringComparison.Ordinal);
        Assert.Contains("\"to\": \"formations\"", canonical, StringComparison.Ordinal);
        Assert.Contains("\"label\": \"Participants\"", canonical, StringComparison.Ordinal);
        Assert.Contains("\"junctionName\": \"Suivis\"", canonical, StringComparison.Ordinal);

        // Stable à l'octet près au re-parse.
        Assert.True(StudioAiSystemSpec.TryParse(canonical, out var reparsed, out var reparseError), reparseError);
        var canonicalAgain = StudioAiSpecCanonical.CanonicalSystem(reparsed!);
        Assert.Equal(canonical, canonicalAgain);

        // Sans label ni junctionName, les clés correspondantes sont omises (pas null).
        const string minimal = """
        { "system": { "displayName": "T" }, "entities": [
          { "ref": "employes", "displayName": "Employés", "fields": [ { "label": "Nom", "type": "text" } ] },
          { "ref": "formations", "displayName": "Formations", "fields": [ { "label": "Nom", "type": "text" } ] }
        ], "relations": [ { "kind": "many_to_many", "from": "employes", "to": "formations" } ] }
        """;
        Assert.True(StudioAiSystemSpec.TryParse(minimal, out var minimalSpec, out var minimalError), minimalError);
        var minimalCanonical = StudioAiSpecCanonical.CanonicalSystem(minimalSpec!);
        var relationNode = JsonNode.Parse(minimalCanonical)!["relations"]!.AsArray()[0]!.AsObject();
        Assert.Equal(new[] { "kind", "from", "to" }, relationNode.Select(p => p.Key).ToArray());
    }

    [Fact]
    public void Spec_without_relations_omits_the_relations_key_from_the_canonical_form()
    {
        const string json = """
        { "system": { "displayName": "T" }, "entities": [
          { "ref": "employes", "displayName": "Employés", "fields": [ { "label": "Nom", "type": "text" } ] } ] }
        """;
        Assert.True(StudioAiSystemSpec.TryParse(json, out var spec, out var error), error);
        var canonical = StudioAiSpecCanonical.CanonicalSystem(spec!);

        Assert.DoesNotContain("\"relations\"", canonical, StringComparison.Ordinal);
    }
}
