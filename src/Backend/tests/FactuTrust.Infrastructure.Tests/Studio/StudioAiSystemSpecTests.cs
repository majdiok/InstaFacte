using FactuTrust.Application.Features.Studio.Ai;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

public sealed class StudioAiSystemSpecTests
{
    [Fact]
    public void Parses_multi_entity_system_with_relations_and_seed()
    {
        const string json = """
        {
          "system": { "displayName": "Gestion des conges", "onboarding": ["Ajoutez des employes", "Configurez les types"] },
          "entities": [
            { "ref": "employes", "displayName": "Employes", "fields": [ { "label": "Nom", "type": "text" } ] },
            { "ref": "types_conges", "displayName": "Types de conges", "fields": [ { "label": "Libelle", "type": "text" } ] },
            { "ref": "demandes", "displayName": "Demandes", "fields": [
              { "label": "Employe", "type": "relation", "relationTo": "employes" },
              { "label": "Type", "type": "relation", "relationTo": "types_conges" },
              { "label": "Statut", "type": "select", "options": [ {"value":"en_attente","label":"En attente"} ] }
            ] }
          ],
          "seed": [ { "entityRef": "types_conges", "records": [ { "libelle": "CP" }, { "libelle": "RTT" } ] } ]
        }
        """;

        Assert.True(StudioAiSystemSpec.TryParse(json, out var spec, out var error), error);
        Assert.NotNull(spec);
        Assert.Equal(3, spec!.Entities.Count);
        Assert.Equal(2, spec.Seed.Count > 0 ? spec.Seed[0].Records.Count : 0);
        var demandes = spec.Entities.First(e => e.Ref == "demandes");
        Assert.Equal(CustomFieldType.RelationCustom, demandes.Fields[0].FieldType);
        Assert.Equal("employes", demandes.Fields[0].RelationToRef);
    }

    [Fact]
    public void Unknown_relation_target_degrades_to_text_instead_of_failing()
    {
        // "connecté à l'ERP": the model points a relation at a non-spec/ERP table. The whole system must
        // still be created — the dangling relation degrades to a plain text field (never a hard failure).
        const string json = """
        { "system": { "displayName": "T" }, "entities": [
          { "ref": "a", "displayName": "A", "fields": [
            { "label": "Nom", "type": "text" },
            { "label": "Employe", "type": "relation", "relationTo": "employees" }
          ] }
        ] }
        """;
        Assert.True(StudioAiSystemSpec.TryParse(json, out var spec, out var error), error);
        var rel = spec!.Entities[0].Fields.First(f => f.Label == "Employe");
        Assert.Equal(CustomFieldType.Text, rel.FieldType);
        Assert.Null(rel.RelationToRef);
    }

    [Theory]
    [InlineData("clients", "clients")]
    [InlineData("Clients", "clients")]
    [InlineData("products", "products")]
    [InlineData("Produits", "products")]
    public void Relation_to_known_erp_source_becomes_existing(string relationTo, string expectedRef)
    {
        var json = $$"""
        { "system": { "displayName": "T" }, "entities": [
          { "ref": "commandes", "displayName": "Commandes", "fields": [
            { "label": "Client", "type": "relation", "relationTo": "{{relationTo}}" }
          ] }
        ] }
        """;
        Assert.True(StudioAiSystemSpec.TryParse(json, out var spec, out var error), error);
        var rel = spec!.Entities[0].Fields[0];
        Assert.Equal(CustomFieldType.RelationExisting, rel.FieldType);
        Assert.Equal(expectedRef, rel.RelationToRef);
    }

    [Fact]
    public void Select_with_options_and_stray_relationTo_stays_select()
    {
        // The small model often pastes a spurious relationTo onto a select that already has options.
        // The field must remain a Select (options kept), NOT become a broken relation.
        const string json = """
        { "system": { "displayName": "Conges" }, "entities": [
          { "ref": "demandes", "displayName": "Demandes", "fields": [
            { "label": "Nom", "type": "text" },
            { "label": "Type de conge", "type": "select", "relationTo": "demandes",
              "options": [ {"value":"vacances","label":"Vacances"}, {"value":"maladie","label":"Maladie"} ] }
          ] }
        ] }
        """;
        Assert.True(StudioAiSystemSpec.TryParse(json, out var spec, out var error), error);
        var typeField = spec!.Entities[0].Fields.First(f => f.Label == "Type de conge");
        Assert.Equal(CustomFieldType.Select, typeField.FieldType);
        Assert.NotNull(typeField.Options);
        Assert.Equal(2, typeField.Options!.Count);
        Assert.Null(typeField.RelationToRef);
    }

    [Fact]
    public void Field_with_relationTo_and_no_options_becomes_relation()
    {
        const string json = """
        { "system": { "displayName": "T" }, "entities": [
          { "ref": "employes", "displayName": "Employes", "fields": [ { "label": "Nom", "type": "text" } ] },
          { "ref": "demandes", "displayName": "Demandes", "fields": [
            { "label": "Employe", "type": "text", "relationTo": "employes" }
          ] }
        ] }
        """;
        Assert.True(StudioAiSystemSpec.TryParse(json, out var spec, out var error), error);
        var rel = spec!.Entities.First(e => e.Ref == "demandes").Fields[0];
        Assert.Equal(CustomFieldType.RelationCustom, rel.FieldType);
        Assert.Equal("employes", rel.RelationToRef);
    }
}