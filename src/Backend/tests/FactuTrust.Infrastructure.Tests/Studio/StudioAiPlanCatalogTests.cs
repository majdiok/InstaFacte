using System.Text.Json;
using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.AI.Tools;
using FactuTrust.Application.Features.Studio.Ai;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

/// <summary>
/// Garde anti-régression du flux « aperçu » : drapeau OFF ⇒ le catalogue StudioBuilder est
/// strictement celui d'avant ; drapeau ON ⇒ les outils de génération directe sont remplacés par
/// les outils de plan (jamais les deux à la fois, pour ne pas laisser au modèle un chemin
/// d'exécution sans validation).
/// </summary>
public sealed class StudioAiPlanCatalogTests
{
    [Fact]
    public void Flag_off_keeps_the_historical_studio_builder_catalogue()
    {
        var names = AiToolRegistry
            .GetDefinitionsForMode(AssistantMode.StudioBuilder, enableMutationTools: true)
            .Select(t => t.Name)
            .ToList();

        Assert.Contains("studio_generate_app", names);
        Assert.Contains("studio_generate_system", names);
        Assert.DoesNotContain("studio_plan_app", names);
        Assert.DoesNotContain("studio_plan_system", names);
    }

    [Fact]
    public void Flag_on_substitutes_plan_tools_for_direct_generation()
    {
        var historical = AiToolRegistry
            .GetDefinitionsForMode(AssistantMode.StudioBuilder, enableMutationTools: true)
            .Select(t => t.Name)
            .ToHashSet(StringComparer.Ordinal);
        var withPreview = AiToolRegistry
            .GetDefinitionsForMode(AssistantMode.StudioBuilder, enableMutationTools: true,
                agentScope: AssistantAgentScope.None, studioPlanPreview: true)
            .Select(t => t.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("studio_plan_app", withPreview);
        Assert.Contains("studio_plan_system", withPreview);
        Assert.DoesNotContain("studio_generate_app", withPreview);
        Assert.DoesNotContain("studio_generate_system", withPreview);

        // Seule la génération est substituée : tout le reste du catalogue StudioBuilder est identique.
        Assert.Equal(
            historical.Except(new[] { "studio_generate_app", "studio_generate_system" }).OrderBy(n => n),
            withPreview.Except(new[] { "studio_plan_app", "studio_plan_system" }).OrderBy(n => n));
    }

    [Fact]
    public void Plan_tools_are_absent_when_mutations_are_disabled()
    {
        var names = AiToolRegistry
            .GetDefinitionsForMode(AssistantMode.StudioBuilder, enableMutationTools: false,
                agentScope: AssistantAgentScope.None, studioPlanPreview: true)
            .Select(t => t.Name)
            .ToList();

        Assert.DoesNotContain("studio_plan_app", names);
        Assert.DoesNotContain("studio_plan_system", names);
    }

    [Fact]
    public void Plan_tools_have_a_french_label()
    {
        // Un nom d'outil interne ne doit jamais atteindre l'utilisateur : chaque outil a un libellé.
        Assert.True(AiToolFrenchLabels.Labels.ContainsKey("studio_plan_app"));
        Assert.True(AiToolFrenchLabels.Labels.ContainsKey("studio_plan_system"));
    }

    [Fact]
    public void Text_tool_call_recovery_handles_plan_tools()
    {
        const string content = """
        Voici ma proposition :
        {"name":"studio_plan_system","arguments":{"spec_json":"{\"system\":{\"displayName\":\"Congés\"}}"}}
        """;

        Assert.True(StudioTextToolCallRecovery.TryExtract(content, out var toolName, out var specJson));
        Assert.Equal("studio_plan_system", toolName);
        Assert.Contains("Congés", specJson);
    }

    [Fact]
    public void System_plan_summary_counts_tables_fields_relations_and_seed()
    {
        const string json = """
        { "system": { "displayName": "Gestion des congés", "onboarding": ["Ajoutez des employés"] },
          "entities": [
            { "ref": "employes", "displayName": "Employés", "fields": [ { "label": "Nom", "type": "text" } ] },
            { "ref": "demandes", "displayName": "Demandes", "fields": [
              { "label": "Employé", "type": "relation", "relationTo": "employes" },
              { "label": "Jours", "type": "number" }
            ],
            "form": { "sections": [ { "fields": [ "jours" ] } ] },
            "report": { "displayName": "Par employé", "groupBy": ["jours"] } }
          ],
          "seed": [ { "entityRef": "employes", "records": [ { "nom": "A" }, { "nom": "B" } ] } ] }
        """;
        Assert.True(StudioAiSystemSpec.TryParse(json, out var spec, out var err), err);

        using var doc = JsonDocument.Parse(StudioAiPlanSummary.ForSystem(spec!));
        var root = doc.RootElement;

        Assert.Equal("Gestion des congés", root.GetProperty("title").GetString());
        Assert.Equal(2, root.GetProperty("entities").GetArrayLength());

        var steps = root.GetProperty("steps").EnumerateArray()
            .ToDictionary(s => s.GetProperty("key").GetString()!, s => s.GetProperty("detail").GetString()!);
        Assert.Contains("2 table(s)", steps["data_model"]);
        Assert.Contains("3 champ(s)", steps["data_model"]);
        Assert.Contains("1 relation(s)", steps["data_model"]);
        Assert.Contains("1 formulaire", steps["forms"]);
        Assert.Contains("1 état", steps["reports"]);
        Assert.Contains("2 enregistrement", steps["seed"]);
        Assert.True(steps.ContainsKey("onboarding"));
    }

    [Fact]
    public void App_plan_summary_describes_a_single_table()
    {
        const string json = """
        { "entity": { "displayName": "Contrats" },
          "fields": [ { "label": "Nom", "type": "text" }, { "label": "Montant", "type": "money" } ],
          "report": { "displayName": "Par mois", "groupBy": ["nom"] } }
        """;
        Assert.True(StudioAiAppSpec.TryParse(json, out var spec, out var err), err);

        using var doc = JsonDocument.Parse(StudioAiPlanSummary.ForApp(spec!));
        var root = doc.RootElement;

        Assert.Equal("Contrats", root.GetProperty("title").GetString());
        Assert.Equal("CreateApp", root.GetProperty("kind").GetString());
        var steps = root.GetProperty("steps").EnumerateArray()
            .ToDictionary(s => s.GetProperty("key").GetString()!, s => s.GetProperty("detail").GetString()!);
        Assert.Contains("2 champ(s)", steps["data_model"]);
        Assert.Contains("Par mois", steps["reports"]);
    }
}
