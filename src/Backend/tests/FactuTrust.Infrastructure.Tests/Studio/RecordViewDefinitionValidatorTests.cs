using System.Text.Json.Nodes;
using FactuTrust.Application.Features.Studio.RecordViews;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

/// <summary>
/// Studio IA — PR 2.3 : règles du validateur de définition de vue. Bornes 25/10/3, pageSize 1..200,
/// clés connues (champs actifs + createdAt/updatedAt), compatibilité opérateur/type, kanban ⇒ Select,
/// calendrier ⇒ Date/DateTime, champs calculés refusés en filtre/tri (autorisés en colonne).
/// </summary>
public sealed class RecordViewDefinitionValidatorTests
{
    private static readonly Guid Tid = Guid.NewGuid();
    private static readonly Guid Eid = Guid.NewGuid();

    private static CustomFieldDefinition Field(string key, CustomFieldType type, bool active = true, string? optionsJson = null)
    {
        var f = CustomFieldDefinition.Create(Tid, Eid, key, key, type, false, false, 0, null, optionsJson, null, null);
        if (!active) f.Update(key, false, false, null, optionsJson, null, isActive: false, null);
        return f;
    }

    private static readonly string SelectOptions = """{"options":[{"value":"encours","label":"En cours"},{"value":"termine","label":"Terminé"}]}""";

    private static IReadOnlyList<CustomFieldDefinition> Fields() => new[]
    {
        Field("nom", CustomFieldType.Text),
        Field("statut", CustomFieldType.Select, optionsJson: SelectOptions),
        Field("montant", CustomFieldType.Money),
        Field("debut", CustomFieldType.Date),
        Field("calcule", CustomFieldType.Formula),
        Field("inactif", CustomFieldType.Text, active: false)
    };

    private static RecordViewDefinition ListDef(
        IReadOnlyList<RecordViewColumn>? columns = null,
        IReadOnlyList<RecordViewFilter>? filters = null,
        IReadOnlyList<RecordViewSort>? sort = null) =>
        new(columns ?? new[] { new RecordViewColumn("nom") },
            filters ?? Array.Empty<RecordViewFilter>(),
            sort ?? Array.Empty<RecordViewSort>(),
            null, null);

    [Fact]
    public void A_simple_list_definition_is_valid()
    {
        var result = RecordViewDefinitionValidator.Validate(
            ListDef(sort: new[] { new RecordViewSort("createdAt", Descending: true) }),
            CustomRecordViewMode.List, Fields());

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Bounds_25_columns_10_filters_3_sorts_are_enforced()
    {
        var tooManyColumns = RecordViewDefinitionValidator.Validate(
            ListDef(columns: Enumerable.Range(0, 26).Select(i => new RecordViewColumn("nom")).ToList()),
            CustomRecordViewMode.List, Fields());
        Assert.True(tooManyColumns.IsFailure);
        Assert.Equal("Validation.columns", tooManyColumns.Error.Code);

        var tooManyFilters = RecordViewDefinitionValidator.Validate(
            ListDef(filters: Enumerable.Range(0, 11).Select(_ => new RecordViewFilter("nom", "eq", JsonValue.Create("x"))).ToList()),
            CustomRecordViewMode.List, Fields());
        Assert.True(tooManyFilters.IsFailure);
        Assert.Equal("Validation.filters", tooManyFilters.Error.Code);

        var tooManySorts = RecordViewDefinitionValidator.Validate(
            ListDef(sort: Enumerable.Range(0, 4).Select(_ => new RecordViewSort("nom")).ToList()),
            CustomRecordViewMode.List, Fields());
        Assert.True(tooManySorts.IsFailure);
        Assert.Equal("Validation.sort", tooManySorts.Error.Code);
    }

    [Fact]
    public void Unknown_or_inactive_field_is_rejected()
    {
        var unknown = RecordViewDefinitionValidator.Validate(
            ListDef(columns: new[] { new RecordViewColumn("nope") }), CustomRecordViewMode.List, Fields());
        Assert.True(unknown.IsFailure);
        Assert.Contains("nope", unknown.Error.Description);

        var inactive = RecordViewDefinitionValidator.Validate(
            ListDef(columns: new[] { new RecordViewColumn("inactif") }), CustomRecordViewMode.List, Fields());
        Assert.True(inactive.IsFailure);
    }

    [Fact]
    public void Incompatible_operator_is_rejected()
    {
        var result = RecordViewDefinitionValidator.Validate(
            ListDef(filters: new[] { new RecordViewFilter("nom", "gt", JsonValue.Create("x")) }),
            CustomRecordViewMode.List, Fields());

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.filters", result.Error.Code);
        Assert.Contains("gt", result.Error.Description);
    }

    [Fact]
    public void Computed_field_is_refused_in_filter_and_sort_but_allowed_as_column()
    {
        var inFilter = RecordViewDefinitionValidator.Validate(
            ListDef(filters: new[] { new RecordViewFilter("calcule", "eq", JsonValue.Create("x")) }),
            CustomRecordViewMode.List, Fields());
        Assert.True(inFilter.IsFailure);
        Assert.Contains("calculé", inFilter.Error.Description);

        var inSort = RecordViewDefinitionValidator.Validate(
            ListDef(sort: new[] { new RecordViewSort("calcule") }), CustomRecordViewMode.List, Fields());
        Assert.True(inSort.IsFailure);
        Assert.Equal("Validation.sort", inSort.Error.Code);

        var asColumn = RecordViewDefinitionValidator.Validate(
            ListDef(columns: new[] { new RecordViewColumn("calcule") }), CustomRecordViewMode.List, Fields());
        Assert.True(asColumn.IsSuccess);
    }

    [Fact]
    public void Kanban_requires_a_select_group_field()
    {
        var onSelect = RecordViewDefinitionValidator.Validate(
            new RecordViewDefinition(new[] { new RecordViewColumn("nom") }, Array.Empty<RecordViewFilter>(),
                Array.Empty<RecordViewSort>(),
                new RecordViewKanban("statut", "nom", new[] { "montant" }, null), null),
            CustomRecordViewMode.Kanban, Fields());
        Assert.True(onSelect.IsSuccess);

        var onText = RecordViewDefinitionValidator.Validate(
            new RecordViewDefinition(new[] { new RecordViewColumn("nom") }, Array.Empty<RecordViewFilter>(),
                Array.Empty<RecordViewSort>(),
                new RecordViewKanban("nom", null, null, null), null),
            CustomRecordViewMode.Kanban, Fields());
        Assert.True(onText.IsFailure);
        Assert.Equal("Validation.kanban", onText.Error.Code);

        var missingSection = RecordViewDefinitionValidator.Validate(
            ListDef(), CustomRecordViewMode.Kanban, Fields());
        Assert.True(missingSection.IsFailure);
        Assert.Equal("Validation.kanban", missingSection.Error.Code);
    }

    [Fact]
    public void Calendar_requires_date_fields()
    {
        var ok = RecordViewDefinitionValidator.Validate(
            new RecordViewDefinition(new[] { new RecordViewColumn("nom") }, Array.Empty<RecordViewFilter>(),
                Array.Empty<RecordViewSort>(), null,
                new RecordViewCalendar("debut", null, "nom", "statut")),
            CustomRecordViewMode.Calendar, Fields());
        Assert.True(ok.IsSuccess);

        var onText = RecordViewDefinitionValidator.Validate(
            new RecordViewDefinition(new[] { new RecordViewColumn("nom") }, Array.Empty<RecordViewFilter>(),
                Array.Empty<RecordViewSort>(), null,
                new RecordViewCalendar("nom", null, null, null)),
            CustomRecordViewMode.Calendar, Fields());
        Assert.True(onText.IsFailure);
        Assert.Equal("Validation.calendar", onText.Error.Code);
    }

    [Fact]
    public void Page_size_must_be_within_1_200()
    {
        var tooBig = RecordViewDefinitionValidator.Validate(
            ListDef() with { PageSize = 201 }, CustomRecordViewMode.List, Fields());
        Assert.True(tooBig.IsFailure);
        Assert.Equal("Validation.pageSize", tooBig.Error.Code);

        var zero = RecordViewDefinitionValidator.Validate(
            ListDef() with { PageSize = 0 }, CustomRecordViewMode.List, Fields());
        Assert.True(zero.IsFailure);
    }

    [Fact]
    public void Persisted_createdAt_updatedAt_are_allowed_as_columns_and_sort_but_not_filters()
    {
        var ok = RecordViewDefinitionValidator.Validate(
            ListDef(columns: new[] { new RecordViewColumn("createdAt") }, sort: new[] { new RecordViewSort("updatedAt") }),
            CustomRecordViewMode.List, Fields());
        Assert.True(ok.IsSuccess);

        var asFilter = RecordViewDefinitionValidator.Validate(
            ListDef(filters: new[] { new RecordViewFilter("createdAt", "eq", JsonValue.Create("x")) }),
            CustomRecordViewMode.List, Fields());
        Assert.True(asFilter.IsFailure);
    }

    // ---- forme des valeurs de filtres (sans elle : 500 au run au lieu de 400) ----

    [Fact]
    public void In_requires_a_bounded_array_value()
    {
        var notAnArray = RecordViewDefinitionValidator.Validate(
            ListDef(filters: new[] { new RecordViewFilter("statut", "in", JsonValue.Create("x")) }),
            CustomRecordViewMode.List, Fields());
        Assert.True(notAnArray.IsFailure);
        Assert.Equal("Validation.filters", notAnArray.Error.Code);

        var oversized = RecordViewDefinitionValidator.Validate(
            ListDef(filters: new[] { new RecordViewFilter("statut", "in",
                new JsonArray(Enumerable.Range(0, 101).Select(i => (JsonNode)JsonValue.Create($"v{i}")).ToArray())) }),
            CustomRecordViewMode.List, Fields());
        Assert.True(oversized.IsFailure);

        var ok = RecordViewDefinitionValidator.Validate(
            ListDef(filters: new[] { new RecordViewFilter("statut", "in",
                new JsonArray(JsonValue.Create("encours"), JsonValue.Create("termine"))) }),
            CustomRecordViewMode.List, Fields());
        Assert.True(ok.IsSuccess);
    }

    [Fact]
    public void Between_requires_two_convertible_bounds()
    {
        var notTwo = RecordViewDefinitionValidator.Validate(
            ListDef(filters: new[] { new RecordViewFilter("debut", "between",
                new JsonArray(JsonValue.Create("2026-01-01"))) }),
            CustomRecordViewMode.List, Fields());
        Assert.True(notTwo.IsFailure);
        Assert.Equal("Validation.filters", notTwo.Error.Code);

        var notConvertible = RecordViewDefinitionValidator.Validate(
            ListDef(filters: new[] { new RecordViewFilter("debut", "between",
                new JsonArray(JsonValue.Create("abc"), JsonValue.Create("2026-03-31"))) }),
            CustomRecordViewMode.List, Fields());
        Assert.True(notConvertible.IsFailure);
        Assert.Contains("abc", notConvertible.Error.Description);
    }

    [Fact]
    public void Typed_comparisons_reject_non_convertible_scalars()
    {
        var money = RecordViewDefinitionValidator.Validate(
            ListDef(filters: new[] { new RecordViewFilter("montant", "gt", JsonValue.Create("abc")) }),
            CustomRecordViewMode.List, Fields());
        Assert.True(money.IsFailure);
        Assert.Contains("montant", money.Error.Description);

        var date = RecordViewDefinitionValidator.Validate(
            ListDef(filters: new[] { new RecordViewFilter("debut", "lte", JsonValue.Create("pas-une-date")) }),
            CustomRecordViewMode.List, Fields());
        Assert.True(date.IsFailure);

        var objectValue = RecordViewDefinitionValidator.Validate(
            ListDef(filters: new[] { new RecordViewFilter("montant", "gte", new JsonObject()) }),
            CustomRecordViewMode.List, Fields());
        Assert.True(objectValue.IsFailure);

        var okMoney = RecordViewDefinitionValidator.Validate(
            ListDef(filters: new[] { new RecordViewFilter("montant", "gt", JsonValue.Create("100.5")) }),
            CustomRecordViewMode.List, Fields());
        Assert.True(okMoney.IsSuccess);

        var okDate = RecordViewDefinitionValidator.Validate(
            ListDef(filters: new[] { new RecordViewFilter("debut", "gte", JsonValue.Create("2026-01-15")) }),
            CustomRecordViewMode.List, Fields());
        Assert.True(okDate.IsSuccess);
    }
}
