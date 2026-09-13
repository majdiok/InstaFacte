using System.Text.Json.Nodes;
using FactuTrust.Application.Features.Studio.Ai;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.RecordViews;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

/// <summary>
/// PR 2.4 — parsing tolérant et résolution contre le schéma réel des specs de vues enregistrées.
/// Schéma de référence : une table « interventions » (titre, client, statut Select, priorité Select,
/// date prévue Date, coût Money) inspirée du registre QA, cas 60–62.
/// </summary>
public class StudioAiRecordViewSpecTests
{
    private static readonly IReadOnlyList<CustomFieldDto> Interventions =
    [
        FieldDto("titre", CustomFieldType.Text, "Titre", order: 1),
        FieldDto("client", CustomFieldType.Text, "Client", order: 2),
        FieldDto("statut", CustomFieldType.Select, "Statut", order: 3,
            options: [new SelectOptionDto("planifiee", "Planifiée"), new SelectOptionDto("en_cours", "En cours"), new SelectOptionDto("terminee", "Terminée")]),
        FieldDto("priorite", CustomFieldType.Select, "Priorité", order: 4,
            options: [new SelectOptionDto("haute", "Haute"), new SelectOptionDto("basse", "Basse")]),
        FieldDto("date_prevue", CustomFieldType.Date, "Date prévue", order: 5),
        FieldDto("cout", CustomFieldType.Money, "Coût", order: 6),
    ];

    private static CustomFieldDto FieldDto(string key, CustomFieldType type, string? label = null,
        IReadOnlyList<SelectOptionDto>? options = null, int order = 1)
        => new(Guid.NewGuid(), key, label ?? key, type, false, false, order, null, options, null, true);

    // ---------- TryParse ----------

    [Fact]
    public void TryParse_rejects_invalid_mode()
    {
        var ok = StudioAiRecordViewSpec.TryParse(
            """{"entity":"interventions","name":"Vue","mode":"graphique"}""", out _, out var error);
        Assert.False(ok);
        Assert.Contains("Mode de vue inconnu", error);
    }

    [Fact]
    public void TryParse_rejects_missing_name()
    {
        var ok = StudioAiRecordViewSpec.TryParse(
            """{"entity":"interventions","mode":"list"}""", out _, out var error);
        Assert.False(ok);
        Assert.Contains("name est obligatoire", error);
    }

    [Fact]
    public void TryParse_rejects_invalid_json()
    {
        var ok = StudioAiRecordViewSpec.TryParse("{pas du json", out _, out var error);
        Assert.False(ok);
        Assert.Contains("JSON valide", error);
    }

    [Fact]
    public void TryParse_defaults_mode_to_list_and_slugs_entity_key()
    {
        var ok = StudioAiRecordViewSpec.TryParse(
            """{"entity":"Mes Interventions","name":"Toutes"}""", out var spec, out var error);
        Assert.True(ok, error);
        Assert.Equal("mes_interventions", spec!.EntityKey);
        Assert.Equal("list", spec.Mode);
        Assert.False(spec.IsDefault);
    }

    [Fact]
    public void TryParse_accepts_french_aliases()
    {
        var ok = StudioAiRecordViewSpec.TryParse(
            """{"table":"interventions","nom":"Kanban","mode":"tableau","colonnes":["titre"],"groupe":"statut","parDefaut":true}""",
            out var spec, out var error);
        Assert.True(ok, error);
        Assert.Equal("interventions", spec!.EntityKey);
        Assert.Equal("kanban", spec.Mode);
        Assert.Equal(new[] { "titre" }, spec.Columns);
        Assert.Equal("statut", spec.GroupByFieldKey);
        Assert.True(spec.IsDefault);
    }

    [Fact]
    public void TryParse_calendar_french_alias_reads_start_end()
    {
        var ok = StudioAiRecordViewSpec.TryParse(
            """{"entity":"interventions","name":"Planning","mode":"calendrier","debut":"date_prevue","titre":"titre"}""",
            out var spec, out var error);
        Assert.True(ok, error);
        Assert.Equal("calendar", spec!.Mode);
        Assert.Equal("date_prevue", spec.StartFieldKey);
        Assert.Equal("titre", spec.TitleFieldKey);
    }

    // ---------- ResolveAgainstSchema : tolérance et avertissements ----------

    [Fact]
    public void Resolve_keeps_tolerant_matching_columns_and_filters()
    {
        var ok = StudioAiRecordViewSpec.TryParse("""
            {"entity":"interventions","name":"Kanban par statut","mode":"kanban",
             "columns":["Titre","Client","statut","unknown_col"],
             "filters":[{"field":"statut","op":"in","value":["planifiee","en_cours"]},{"field":"bidon","op":"eq","value":1}],
             "sort":[{"field":"cout","desc":true}],
             "groupBy":"Statut","title":"titre"}
            """, out var spec, out var error);
        Assert.True(ok, error);

        var (mode, definition, warnings) = StudioAiRecordViewSpec.ResolveAgainstSchema(spec!, Interventions);

        Assert.Equal(CustomRecordViewMode.Kanban, mode);
        // Libellés résolus vers les clés réelles ; colonne inconnue retirée avec avertissement.
        Assert.Equal(new[] { "titre", "client", "statut" }, definition.Columns.Select(c => c.FieldKey));
        Assert.Single(definition.Filters);
        Assert.Equal("statut", definition.Filters[0].FieldKey);
        Assert.Single(definition.Sort);
        Assert.Equal("cout", definition.Sort[0].FieldKey);
        Assert.True(definition.Sort[0].Descending);
        Assert.Equal("statut", definition.Kanban!.GroupByFieldKey);
        Assert.Equal("titre", definition.Kanban.TitleFieldKey);
        Assert.Contains(warnings, w => w.Contains("unknown_col"));
        Assert.Contains(warnings, w => w.Contains("bidon"));
    }

    [Fact]
    public void Resolve_rejects_operator_incompatible_with_field_type()
    {
        var ok = StudioAiRecordViewSpec.TryParse("""
            {"entity":"interventions","name":"Filtrée","mode":"list",
             "columns":["titre"],"filters":[{"field":"titre","op":"gte","value":5}]}
            """, out var spec, out var error);
        Assert.True(ok, error);

        var (mode, definition, warnings) = StudioAiRecordViewSpec.ResolveAgainstSchema(spec!, Interventions);

        Assert.Equal(CustomRecordViewMode.List, mode);
        Assert.Empty(definition.Filters);
        Assert.Contains(warnings, w => w.Contains("incompatible"));
    }

    [Fact]
    public void Resolve_removes_malformed_filter_value()
    {
        // « between » exige exactement 2 bornes : une valeur simple est rejetée avec avertissement.
        var ok = StudioAiRecordViewSpec.TryParse("""
            {"entity":"interventions","name":"Filtrée","mode":"list",
             "columns":["titre"],"filters":[{"field":"cout","op":"between","value":12}]}
            """, out var spec, out var error);
        Assert.True(ok, error);

        var (_, definition, warnings) = StudioAiRecordViewSpec.ResolveAgainstSchema(spec!, Interventions);

        Assert.Empty(definition.Filters);
        Assert.Contains(warnings, w => w.Contains("cout"));
    }

    [Fact]
    public void Kanban_without_select_group_degrades_to_list()
    {
        var ok = StudioAiRecordViewSpec.TryParse(
            """{"entity":"interventions","name":"Kanban","mode":"kanban","columns":["titre"]}""",
            out var spec, out var error);
        Assert.True(ok, error);

        var (mode, definition, warnings) = StudioAiRecordViewSpec.ResolveAgainstSchema(spec!, Interventions);

        Assert.Equal(CustomRecordViewMode.List, mode);
        Assert.Null(definition.Kanban);
        Assert.Contains(warnings, w => w.Contains("Kanban impossible"));
    }

    [Fact]
    public void Kanban_grouping_on_text_field_degrades_to_list()
    {
        var ok = StudioAiRecordViewSpec.TryParse(
            """{"entity":"interventions","name":"Kanban","mode":"kanban","columns":["titre"],"groupBy":"client"}""",
            out var spec, out var error);
        Assert.True(ok, error);

        var (mode, _, warnings) = StudioAiRecordViewSpec.ResolveAgainstSchema(spec!, Interventions);

        Assert.Equal(CustomRecordViewMode.List, mode);
        Assert.Contains(warnings, w => w.Contains("Kanban impossible") && w.Contains("client"));
    }

    [Fact]
    public void Calendar_without_date_start_degrades_to_list()
    {
        var ok = StudioAiRecordViewSpec.TryParse(
            """{"entity":"interventions","name":"Agenda","mode":"calendar","columns":["titre"],"start":"client"}""",
            out var spec, out var error);
        Assert.True(ok, error);

        var (mode, definition, warnings) = StudioAiRecordViewSpec.ResolveAgainstSchema(spec!, Interventions);

        Assert.Equal(CustomRecordViewMode.List, mode);
        Assert.Null(definition.Calendar);
        Assert.Contains(warnings, w => w.Contains("Calendrier impossible"));
    }

    [Fact]
    public void Calendar_ignores_non_date_end_field()
    {
        var ok = StudioAiRecordViewSpec.TryParse(
            """{"entity":"interventions","name":"Agenda","mode":"calendar","start":"date_prevue","end":"client"}""",
            out var spec, out var error);
        Assert.True(ok, error);

        var (mode, definition, warnings) = StudioAiRecordViewSpec.ResolveAgainstSchema(spec!, Interventions);

        Assert.Equal(CustomRecordViewMode.Calendar, mode);
        Assert.Equal("date_prevue", definition.Calendar!.StartFieldKey);
        Assert.Null(definition.Calendar.EndFieldKey);
        Assert.Contains(warnings, w => w.Contains("Champ de fin"));
    }

    [Fact]
    public void Sort_on_computed_field_removed()
    {
        var fields = Interventions
            .Concat([FieldDto("score", CustomFieldType.Formula, "Score", order: 7)])
            .ToList();
        var ok = StudioAiRecordViewSpec.TryParse(
            """{"entity":"interventions","name":"Tri","mode":"list","columns":["titre"],"sort":[{"field":"score"}]}""",
            out var spec, out var error);
        Assert.True(ok, error);

        var (_, definition, warnings) = StudioAiRecordViewSpec.ResolveAgainstSchema(spec!, fields);

        Assert.Empty(definition.Sort);
        Assert.Contains(warnings, w => w.Contains("calculé"));
    }

    [Fact]
    public void Columns_are_deduplicated()
    {
        var ok = StudioAiRecordViewSpec.TryParse(
            """{"entity":"interventions","name":"Doublons","columns":["titre","Titre","client"]}""",
            out var spec, out var error);
        Assert.True(ok, error);

        var (_, definition, _) = StudioAiRecordViewSpec.ResolveAgainstSchema(spec!, Interventions);

        Assert.Equal(new[] { "titre", "client" }, definition.Columns.Select(c => c.FieldKey));
    }

    [Fact]
    public void Columns_beyond_25_are_truncated_with_warning()
    {
        var fields = Enumerable.Range(1, 30)
            .Select(i => FieldDto($"c{i:D2}", CustomFieldType.Text, order: i))
            .ToList();
        var ok = StudioAiRecordViewSpec.TryParse($$"""
            {"entity":"t","name":"Large","columns":[{{string.Join(",", Enumerable.Range(1, 30).Select(i => $"\"c{i:D2}\""))}}]}
            """, out var spec, out var error);
        Assert.True(ok, error);

        var (_, definition, warnings) = StudioAiRecordViewSpec.ResolveAgainstSchema(spec!, fields);

        Assert.Equal(25, definition.Columns.Count);
        Assert.Contains(warnings, w => w.Contains("25 colonnes"));
    }

    [Fact]
    public void Empty_columns_default_to_first_six_active_fields()
    {
        var ok = StudioAiRecordViewSpec.TryParse(
            """{"entity":"interventions","name":"Par défaut"}""", out var spec, out var error);
        Assert.True(ok, error);

        var (_, definition, _) = StudioAiRecordViewSpec.ResolveAgainstSchema(spec!, Interventions);

        Assert.Equal(
            new[] { "titre", "client", "statut", "priorite", "date_prevue", "cout" },
            definition.Columns.Select(c => c.FieldKey));
    }

    // ---------- SlugKey ----------

    [Fact]
    public void SlugKey_prefixes_and_strips_diacritics()
    {
        Assert.Equal("vue_mes_interventions", StudioAiRecordViewSpec.SlugKey("Mes interventions", new HashSet<string>()));
        Assert.Equal("vue_ete_2024", StudioAiRecordViewSpec.SlugKey("Été 2024", new HashSet<string>()));
    }

    [Fact]
    public void SlugKey_ensures_unique_with_suffix()
    {
        var used = new HashSet<string>(StringComparer.Ordinal) { "vue_mes_interventions" };
        Assert.Equal("vue_mes_interventions_2", StudioAiRecordViewSpec.SlugKey("Mes interventions", used));
    }

    [Fact]
    public void SlugKey_stays_within_studio_key_limits()
    {
        var key = StudioAiRecordViewSpec.SlugKey(new string('a', 200), new HashSet<string>());
        Assert.True(StudioKey.IsValidShape(key), $"clé invalide : {key}");
    }
}
