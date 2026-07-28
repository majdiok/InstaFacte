using System.Text.Json;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.Studio.Ai;

/// <summary>
/// Construit l'aperçu structuré (checklist type « Modèle de données / Formulaires / États / Données »)
/// d'un plan Studio IA, rendu par le frontend avant confirmation. Pur (pas de DB), sérialisé en JSON
/// camelCase dans <c>StudioAiBuildPlan.SummaryJson</c>.
/// </summary>
public static class StudioAiPlanSummary
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public sealed record SummaryStep(string Key, string Label, string Detail);

    public sealed record SummaryEntity(string DisplayName, int FieldCount, int RelationCount);

    public sealed record PlanSummary(
        string Kind,
        string Title,
        IReadOnlyList<SummaryStep> Steps,
        IReadOnlyList<SummaryEntity> Entities,
        IReadOnlyList<string> Warnings);

    public static string ForSystem(ParsedSystemSpec spec)
    {
        var entities = spec.Entities
            .Select(e => new SummaryEntity(
                e.EntityDisplayName,
                e.Fields.Count,
                e.Fields.Count(f => f.FieldType is CustomFieldType.RelationCustom or CustomFieldType.RelationExisting)))
            .ToList();

        var totalFields = entities.Sum(e => e.FieldCount);
        var totalRelations = entities.Sum(e => e.RelationCount);
        var formCount = spec.Entities.Count(e => e.Form is not null);
        var reportCount = spec.Entities.Count(e => e.Report is not null);
        var seedCount = spec.Seed.Sum(s => s.Records.Count);

        var steps = new List<SummaryStep>
        {
            new("data_model", "Modèle de données",
                $"{spec.Entities.Count} table(s) | {totalFields} champ(s)"
                + (totalRelations > 0 ? $" | {totalRelations} relation(s)" : string.Empty))
        };
        if (formCount > 0)
            steps.Add(new SummaryStep("forms", "Formulaires", $"{formCount} formulaire(s) personnalisé(s)"));
        if (reportCount > 0)
            steps.Add(new SummaryStep("reports", "États", $"{reportCount} état(s)"));
        if (seedCount > 0)
            steps.Add(new SummaryStep("seed", "Données de référence", $"{seedCount} enregistrement(s)"));
        if (spec.OnboardingSteps is { Count: > 0 })
            steps.Add(new SummaryStep("onboarding", "Guide de démarrage", $"{spec.OnboardingSteps.Count} étape(s)"));

        return Serialize(new PlanSummary(
            StudioAiPlanKind.CreateSystem.ToString(), spec.SystemDisplayName, steps, entities, Array.Empty<string>()));
    }

    public static string ForApp(ParsedAppSpec spec)
    {
        var steps = new List<SummaryStep>
        {
            new("data_model", "Modèle de données", $"1 table | {spec.Fields.Count} champ(s)")
        };
        if (spec.Report is not null)
            steps.Add(new SummaryStep("reports", "États", $"1 état (« {spec.Report.DisplayName} »)"));

        var entities = new List<SummaryEntity> { new(spec.EntityDisplayName, spec.Fields.Count, 0) };
        return Serialize(new PlanSummary(
            StudioAiPlanKind.CreateApp.ToString(), spec.EntityDisplayName, steps, entities, Array.Empty<string>()));
    }

    public static string ForView(
        string title, string table, Common.ViewDefinition definition, IReadOnlyList<string> warnings)
    {
        var steps = new List<SummaryStep>
        {
            new("view_source", "Table source", table),
            new("view_columns", "Colonnes affichées",
                string.Join(", ", definition.Columns.Take(8).Select(c => c.Label ?? c.Name))
                + (definition.Columns.Count > 8 ? $" (+{definition.Columns.Count - 8})" : string.Empty))
        };
        if (definition.Search)
            steps.Add(new SummaryStep("view_search", "Recherche", "activée"));

        return Serialize(new PlanSummary(
            StudioAiPlanKind.View.ToString(), title, steps,
            Array.Empty<SummaryEntity>(), warnings));
    }

    private static string Serialize(PlanSummary summary) => JsonSerializer.Serialize(summary, Options);
}
