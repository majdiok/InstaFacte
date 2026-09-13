using System.Text.Json;
using System.Text.Json.Serialization;
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

    /// <summary><paramref name="ExistingKey"/> non null = table existante réutilisée telle quelle
    /// (propriété omise du JSON quand null : les tables créées n'encombrent pas l'aperçu).</summary>
    public sealed record SummaryEntity(
        string DisplayName,
        int FieldCount,
        int RelationCount,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ExistingKey = null);

    /// <summary>
    /// Une relation plusieurs-à-plusieurs de l'aperçu (PR 2.2) : libellés d'affichage des deux tables
    /// (jamais de ref interne, l'aperçu ne montre que des noms lisibles) et nom de la jonction si fourni.
    /// </summary>
    public sealed record SummaryRelation(string FromDisplayName, string ToDisplayName, string Kind, string? JunctionName);

    /// <summary>
    /// <paramref name="Sample"/> : quelques VRAIES lignes déjà calculées, jointes à l'aperçu d'un état.
    /// L'utilisateur valide alors sur des chiffres, pas sur une promesse. Paramètre optionnel en fin de
    /// record : les aperçus existants (table, système, fenêtre) sont inchangés.
    /// <paramref name="Duplicates"/> : indices de doublons (table proposée ≈ table existante) — la clé
    /// <c>duplicates</c> est TOUJOURS émise dans le JSON (tableau vide par défaut, jamais null) pour
    /// que le bandeau d'aperçu ait une forme stable.
    /// <paramref name="Relations"/> : relations plusieurs-à-plusieurs déclarées (PR 2.2) ; comme
    /// <c>duplicates</c>, la clé <c>relations</c> est TOUJOURS émise en tableau (vide par défaut).
    /// </summary>
    public sealed record PlanSummary(
        string Kind,
        string Title,
        IReadOnlyList<SummaryStep> Steps,
        IReadOnlyList<SummaryEntity> Entities,
        IReadOnlyList<string> Warnings,
        Common.ReportResultDto? Sample = null,
        IReadOnlyList<DuplicateHint>? Duplicates = null,
        IReadOnlyList<SummaryRelation>? Relations = null);

    /// <summary>
    /// Un avertissement en clair par indice de doublon : l'utilisateur voit POURQUOI la table est
    /// signalée et comment la réutiliser (<c>existingKey</c>) au lieu de la recréer.
    /// </summary>
    internal static List<string> DuplicateWarnings(IReadOnlyList<DuplicateHint>? duplicates) =>
        duplicates?.Select(d =>
            $"Table « {d.SpecDisplayName} » : une table existante « {d.ExistingDisplayName} » (clé « {d.ExistingKey} ») semble équivalente"
            + " — réutilisez-la plutôt que de la recréer (\"existingKey\" dans la spec).").ToList()
        ?? new List<string>();

    public static string ForSystem(ParsedSystemSpec spec, IReadOnlyList<DuplicateHint>? duplicates = null)
    {
        var entities = spec.Entities
            .Select(e => new SummaryEntity(
                e.EntityDisplayName,
                e.Fields.Count,
                e.Fields.Count(f => f.FieldType is CustomFieldType.RelationCustom or CustomFieldType.RelationExisting),
                e.ExistingKey))
            .ToList();

        var created = entities.Where(e => e.ExistingKey is null).ToList();
        var totalFields = created.Sum(e => e.FieldCount);
        var totalRelations = created.Sum(e => e.RelationCount);
        var formCount = spec.Entities.Count(e => e.ExistingKey is null && e.Form is not null);
        var reportCount = spec.Entities.Count(e => e.ExistingKey is null && e.Report is not null);
        var seedCount = spec.Seed.Sum(s => s.Records.Count);

        var steps = new List<SummaryStep>
        {
            new("data_model", "Modèle de données",
                $"{created.Count} table(s) | {totalFields} champ(s)"
                + (totalRelations > 0 ? $" | {totalRelations} relation(s)" : string.Empty))
        };
        var reusedCount = entities.Count(e => e.ExistingKey is not null);
        if (reusedCount > 0)
            steps.Add(new SummaryStep("reuse", "Tables réutilisées",
                $"{reusedCount} table(s) existante(s) reprise(s) telle(s) quelle(s)"));
        var displayNameByRef = spec.Entities.ToDictionary(e => e.Ref, e => e.EntityDisplayName, StringComparer.Ordinal);
        string DisplayNameOf(string r) => displayNameByRef.TryGetValue(r, out var name) ? name : r;
        if (spec.Relations.Count > 0)
        {
            steps.Add(new SummaryStep("relations", "Relations plusieurs-à-plusieurs",
                $"{spec.Relations.Count} relation(s) — tables de liaison : " + string.Join(", ", spec.Relations.Select(r =>
                    string.IsNullOrWhiteSpace(r.JunctionName)
                        ? $"{DisplayNameOf(r.FromRef)} ↔ {DisplayNameOf(r.ToRef)}"
                        : r.JunctionName))));
        }
        if (formCount > 0)
            steps.Add(new SummaryStep("forms", "Formulaires", $"{formCount} formulaire(s) personnalisé(s)"));
        if (reportCount > 0)
            steps.Add(new SummaryStep("reports", "États", $"{reportCount} état(s)"));
        if (seedCount > 0)
            steps.Add(new SummaryStep("seed", "Données de référence", $"{seedCount} enregistrement(s)"));
        if (spec.OnboardingSteps is { Count: > 0 })
            steps.Add(new SummaryStep("onboarding", "Guide de démarrage", $"{spec.OnboardingSteps.Count} étape(s)"));

        var warnings = new List<string>(spec.Warnings ?? Array.Empty<string>());
        warnings.AddRange(DuplicateWarnings(duplicates));

        var summaryRelations = spec.Relations
            .Select(r => new SummaryRelation(DisplayNameOf(r.FromRef), DisplayNameOf(r.ToRef), r.Kind, r.JunctionName))
            .ToList();

        // Duplicates/Relations peuvent rester null ici : Serialize émet toujours un tableau (vide par défaut).
        return Serialize(new PlanSummary(
            StudioAiPlanKind.CreateSystem.ToString(), spec.SystemDisplayName, steps, entities, warnings,
            Duplicates: duplicates, Relations: summaryRelations));
    }

    public static string ForApp(ParsedAppSpec spec, IReadOnlyList<DuplicateHint>? duplicates = null)
    {
        var steps = new List<SummaryStep>
        {
            new("data_model", "Modèle de données", $"1 table | {spec.Fields.Count} champ(s)")
        };
        if (spec.Report is not null)
            steps.Add(new SummaryStep("reports", "États", $"1 état (« {spec.Report.DisplayName} »)"));

        var entities = new List<SummaryEntity> { new(spec.EntityDisplayName, spec.Fields.Count, 0) };
        return Serialize(new PlanSummary(
            StudioAiPlanKind.CreateApp.ToString(), spec.EntityDisplayName, steps, entities,
            DuplicateWarnings(duplicates), Duplicates: duplicates));
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

    /// <summary>
    /// Aperçu d'un ÉTAT : source, axes d'analyse, mesures, filtres — et un échantillon de lignes
    /// réellement calculées. Rien n'est enregistré tant que l'utilisateur n'a pas validé.
    /// </summary>
    public static string ForReport(
        string title,
        string sourceLabel,
        Common.ReportDefinition definition,
        Common.ReportResultDto sample,
        IReadOnlyList<string> warnings)
    {
        string LabelOf(string key) =>
            sample.Columns.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.Ordinal))?.Label ?? key;

        var steps = new List<SummaryStep> { new("report_source", "Source", sourceLabel) };

        if (definition.Grouping.Count > 0)
        {
            steps.Add(new SummaryStep("report_group", "Analysé par",
                string.Join(", ", definition.Grouping.Select(LabelOf))));
            steps.Add(new SummaryStep("report_measures", "Mesures",
                string.Join(", ", sample.Columns.Where(c => c.Kind == "measure").Select(c => c.Label))));
        }
        else
        {
            steps.Add(new SummaryStep("report_columns", "Colonnes",
                string.Join(", ", sample.Columns.Take(8).Select(c => c.Label))
                + (sample.Columns.Count > 8 ? $" (+{sample.Columns.Count - 8})" : string.Empty)));
        }

        if (definition.Filters.Count > 0)
            steps.Add(new SummaryStep("report_filters", "Filtres",
                string.Join(" ; ", definition.Filters.Select(f => $"{LabelOf(f.Field)} {f.Op}"))));

        if (definition.Sort.Count > 0)
            steps.Add(new SummaryStep("report_sort", "Tri",
                string.Join(", ", definition.Sort.Select(s => $"{LabelOf(s.Field)} {(s.Dir == "desc" ? "↓" : "↑")}"))));

        steps.Add(new SummaryStep("report_sample", "Aperçu des données",
            sample.TotalRows == 0
                ? "aucune donnée sur cette période"
                : $"{sample.Rows.Count} ligne(s) affichée(s) sur {sample.TotalRows}"));

        return Serialize(new PlanSummary(
            StudioAiPlanKind.Report.ToString(), title, steps,
            Array.Empty<SummaryEntity>(), warnings, sample));
    }

    /// <summary><c>duplicates</c> et <c>relations</c> sont TOUJOURS présents dans le JSON (tableau vide par défaut).</summary>
    private static string Serialize(PlanSummary summary) => JsonSerializer.Serialize(
        summary with
        {
            Duplicates = summary.Duplicates ?? Array.Empty<DuplicateHint>(),
            Relations = summary.Relations ?? Array.Empty<SummaryRelation>()
        },
        Options);
}
