using FactuTrust.Application.Features.Studio.Ai;
using FactuTrust.Domain.Entities.Studio;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

/// <summary>
/// Détection de doublons (PR 1.3) : une table proposée ≈ une table Studio existante du tenant est
/// signalée (indice d'aperçu, jamais un blocage). Rapprochements conservateurs : clé identique,
/// nom identique (accents pliés), variante singulier/pluriel — et JAMAIS de simple préfixe.
/// </summary>
public sealed class StudioAiDuplicateDetectorTests
{
    private static CustomEntityDefinition Existing(string key, string name, string? plural = null) =>
        CustomEntityDefinition.Create(Guid.NewGuid(), key, name, plural ?? name, null, null, null);

    private static ParsedSystemSpec ParseSpec(string entitiesJson)
    {
        var json = $$"""{ "system": { "displayName": "S" }, "entities": {{entitiesJson}} }""";
        Assert.True(StudioAiSystemSpec.TryParse(json, out var spec, out var error), error);
        return spec!;
    }

    [Fact]
    public void Same_key_is_flagged_with_the_strongest_reason()
    {
        var spec = ParseSpec("""
            [ { "ref": "employes", "displayName": "Collaborateurs", "fields": [ { "label": "Nom" } ] } ]
            """);
        var existing = new List<CustomEntityDefinition> { Existing("employes", "Salariés") };

        var hints = StudioAiDuplicateDetector.Detect(spec, existing);

        var hint = Assert.Single(hints);
        Assert.Equal("employes", hint.SpecRef);
        Assert.Equal("employes", hint.ExistingKey);
        Assert.Equal("Salariés", hint.ExistingDisplayName);
        Assert.Equal(StudioAiDuplicateDetector.ReasonSameKey, hint.Reason);
    }

    [Fact]
    public void Same_accented_label_matches_the_existing_key()
    {
        // « Employés » proposé vs clé existante « employes » : accents et casse pliés.
        var spec = ParseSpec("""
            [ { "ref": "rh_employes", "displayName": "Employés", "fields": [ { "label": "Nom" } ] } ]
            """);
        var existing = new List<CustomEntityDefinition> { Existing("employes", "Salariés") };

        var hints = StudioAiDuplicateDetector.Detect(spec, existing);

        var hint = Assert.Single(hints);
        Assert.Equal(StudioAiDuplicateDetector.ReasonSameName, hint.Reason);
        Assert.Equal("employes", hint.ExistingKey);
    }

    [Fact]
    public void Singular_plural_variant_is_flagged()
    {
        // « Demande de congé » ≡ « Demandes de congés » : mots vides écartés + singulier FR.
        var spec = ParseSpec("""
            [ { "ref": "suivi", "displayName": "Demande de congé", "fields": [ { "label": "Statut" } ] } ]
            """);
        var existing = new List<CustomEntityDefinition> { Existing("demandes_conges", "Demandes de congés") };

        var hints = StudioAiDuplicateDetector.Detect(spec, existing);

        var hint = Assert.Single(hints);
        Assert.Equal(StudioAiDuplicateDetector.ReasonSingularPlural, hint.Reason);
        Assert.Equal("demandes_conges", hint.ExistingKey);
    }

    [Fact]
    public void Prefix_overlap_alone_never_flags()
    {
        // « Contrat » n'est PAS « Contrats cadres » : aucun rapprochement par préfixe.
        var spec = ParseSpec("""
            [ { "ref": "contrat", "displayName": "Contrat", "fields": [ { "label": "Nom" } ] } ]
            """);
        var existing = new List<CustomEntityDefinition> { Existing("contrats_cadres", "Contrats cadres") };

        Assert.Empty(StudioAiDuplicateDetector.Detect(spec, existing));
    }

    [Fact]
    public void Empty_existing_list_yields_no_hint()
    {
        var spec = ParseSpec("""
            [ { "ref": "employes", "displayName": "Employés", "fields": [ { "label": "Nom" } ] } ]
            """);

        Assert.Empty(StudioAiDuplicateDetector.Detect(spec, Array.Empty<CustomEntityDefinition>()));
    }

    [Fact]
    public void Entity_reusing_an_existing_key_is_not_flagged()
    {
        // Choix explicite de réutilisation (existingKey) : pas d'indice, c'est déjà la bonne action.
        var spec = ParseSpec("""
            [ { "ref": "employes", "existingKey": "employes" } ]
            """);
        var existing = new List<CustomEntityDefinition> { Existing("employes", "Employés") };

        Assert.Empty(StudioAiDuplicateDetector.Detect(spec, existing));
    }

    [Fact]
    public void Inactive_existing_tables_are_ignored()
    {
        var inactive = Existing("employes", "Employés");
        inactive.Update("Employés", "Employés", null, null, isActive: false, updatedBy: null);
        var spec = ParseSpec("""
            [ { "ref": "employes", "displayName": "Employés", "fields": [ { "label": "Nom" } ] } ]
            """);

        Assert.Empty(StudioAiDuplicateDetector.Detect(spec, new List<CustomEntityDefinition> { inactive }));
    }

    [Fact]
    public void App_spec_is_checked_against_existing_tables()
    {
        const string json = """
            { "entity": { "displayName": "Registre" }, "fields": [ { "label": "Nom" } ] }
            """;
        Assert.True(StudioAiAppSpec.TryParse(json, out var spec, out var error), error);
        var existing = new List<CustomEntityDefinition> { Existing("registres", "Registres") };

        var hints = StudioAiDuplicateDetector.Detect(spec!, existing);

        var hint = Assert.Single(hints);
        Assert.Equal("registres", hint.ExistingKey);
        Assert.Equal(StudioAiDuplicateDetector.ReasonSingularPlural, hint.Reason);
    }

    [Theory]
    [InlineData("bureaux", "bureau")]
    [InlineData("Demandes de congés", "demande_conge")]
    [InlineData("demandes_conges", "demande_conge")]
    [InlineData("journaux", "journal")]
    public void NormalizeForMatch_folds_accents_stop_words_and_plurals(string input, string expected) =>
        Assert.Equal(expected, StudioAiDuplicateDetector.NormalizeForMatch(input));

    [Theory]
    [InlineData("adresse", "adresse")] // « ss » jamais retiré
    [InlineData("fils", "fils")]       // mot court inchangé
    [InlineData("Contrats cadres", "contrat_cadre")]
    public void NormalizeForMatch_stays_conservative(string input, string expected) =>
        Assert.Equal(expected, StudioAiDuplicateDetector.NormalizeForMatch(input));
}
