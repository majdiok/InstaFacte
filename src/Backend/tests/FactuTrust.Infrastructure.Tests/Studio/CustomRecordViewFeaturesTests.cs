using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.RecordViews;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

/// <summary>
/// Studio IA — PR 2.3 : handlers des vues enregistrées (dépôts mockés). CRUD + audit, clé dupliquée
/// ⇒ 409, quota plan ⇒ Validation.Plan, SetDefault exclusif, Run kanban (groupes ordonnés,
/// « Sans valeur », truncated à la borne) et calendrier (fenêtre obligatoire, &gt; 92 jours ⇒ 400).
/// </summary>
public sealed class CustomRecordViewFeaturesTests
{
    private static readonly Guid Tid = Guid.NewGuid();
    private static readonly Guid EntityId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly Mock<ICustomEntityRepository> _entities = new();
    private readonly Mock<ICustomFieldRepository> _fields = new();
    private readonly Mock<ICustomRecordViewRepository> _views = new();
    private readonly Mock<ICustomRecordRepository> _records = new();
    private readonly Mock<IStudioQuotaService> _quota = new();
    private readonly Mock<IAuditService> _audit = new();
    private readonly Mock<IJsonIndexManager> _jsonIndex = new();
    private readonly Mock<ICurrentUser> _currentUser = new();

    public CustomRecordViewFeaturesTests()
    {
        _currentUser.Setup(u => u.TenantId).Returns(Tid);
        _currentUser.Setup(u => u.UserId).Returns(UserId);

        var entity = CustomEntityDefinition.Create(Tid, "chantiers", "Chantier", "Chantiers", null, null, UserId);
        SetId(entity, EntityId);
        _entities.Setup(e => e.GetByKeyAsync(Tid, "chantiers", It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        _fields.Setup(f => f.ListByEntityAsync(Tid, EntityId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Fields());

        _quota.Setup(q => q.EnsureUnderLimitAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _jsonIndex.Setup(j => j.IndexedColumnExistsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private static readonly string SelectOptions = """{"options":[{"value":"encours","label":"En cours"},{"value":"termine","label":"Terminé"}]}""";

    private static IReadOnlyList<CustomFieldDefinition> Fields() => new[]
    {
        Field("nom", CustomFieldType.Text),
        Field("statut", CustomFieldType.Select, SelectOptions),
        Field("debut", CustomFieldType.Date)
    };

    private static CustomFieldDefinition Field(string key, CustomFieldType type, string? optionsJson = null) =>
        CustomFieldDefinition.Create(Tid, EntityId, key, key, type, false, false, 0, null, optionsJson, null, null);

    private static RecordViewDefinition ListDef() =>
        new(new[] { new RecordViewColumn("nom") },
            new[] { new RecordViewFilter("statut", "eq", System.Text.Json.Nodes.JsonValue.Create("encours")) },
            new[] { new RecordViewSort("createdAt", Descending: true) },
            null, null, SearchEnabled: true, PageSize: 25);

    private static RecordViewDefinition KanbanDef() =>
        new(new[] { new RecordViewColumn("nom") }, Array.Empty<RecordViewFilter>(), Array.Empty<RecordViewSort>(),
            new RecordViewKanban("statut", "nom", new[] { "nom" }, null, ShowEmptyGroup: true), null);

    private static RecordViewDefinition CalendarDef() =>
        new(new[] { new RecordViewColumn("nom") }, Array.Empty<RecordViewFilter>(), Array.Empty<RecordViewSort>(),
            null, new RecordViewCalendar("debut", null, "nom", "statut"));

    private static void SetId(CustomEntityDefinition entity, Guid id) =>
        typeof(CustomEntityDefinition).GetProperty(nameof(CustomEntityDefinition.Id))!.SetValue(entity, id);

    private CustomRecordViewDefinition View(CustomRecordViewMode mode, RecordViewDefinition def, string key = "vue1", bool isDefault = false)
    {
        var v = CustomRecordViewDefinition.Create(Tid, EntityId, key, key, mode, RecordViewDefinitionJson.Serialize(def), isDefault, UserId);
        typeof(CustomRecordViewDefinition).GetProperty(nameof(CustomRecordViewDefinition.RowVersion))!.SetValue(v, new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 });
        return v;
    }

    private OllamaSettings Settings(int kanban = 500, int calendar = 1000) =>
        new() { EnableStudioRecordViews = true, StudioRecordViewMaxKanbanCards = kanban, StudioRecordViewMaxCalendarEvents = calendar };

    // ---- CRUD ----

    [Fact]
    public async Task Create_persists_the_view_makes_first_view_default_and_audits()
    {
        _views.Setup(v => v.KeyExistsAsync(Tid, EntityId, "encours", null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _views.Setup(v => v.CountByEntityAsync(Tid, EntityId, It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _views.Setup(v => v.AddAsync(It.IsAny<CustomRecordViewDefinition>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = new CreateCustomRecordViewCommandHandler(
            _entities.Object, _fields.Object, _views.Object, _quota.Object, _audit.Object, _currentUser.Object);
        var result = await handler.Handle(
            new CreateCustomRecordViewCommand("chantiers", new SaveCustomRecordViewRequest("encours", "En cours", CustomRecordViewMode.List, ListDef())),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("encours", result.Value.Key);
        Assert.True(result.Value.IsDefault); // première vue ⇒ par défaut
        _views.Verify(v => v.AddAsync(It.Is<CustomRecordViewDefinition>(x => x.IsDefault && x.Key == "encours"), It.IsAny<CancellationToken>()), Times.Once);
        _audit.Verify(a => a.LogAsync("Studio.RecordView.Created", "CustomRecordViewDefinition", It.IsAny<Guid?>(), null, It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_returns_409_when_the_key_is_taken()
    {
        _views.Setup(v => v.KeyExistsAsync(Tid, EntityId, "encours", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var handler = new CreateCustomRecordViewCommandHandler(
            _entities.Object, _fields.Object, _views.Object, _quota.Object, _audit.Object, _currentUser.Object);
        var result = await handler.Handle(
            new CreateCustomRecordViewCommand("chantiers", new SaveCustomRecordViewRequest("encours", "En cours", CustomRecordViewMode.List, ListDef())),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
        _views.Verify(v => v.AddAsync(It.IsAny<CustomRecordViewDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_returns_a_quota_error_when_the_plan_limit_is_reached()
    {
        _views.Setup(v => v.KeyExistsAsync(Tid, EntityId, "vue_quota", null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _views.Setup(v => v.CountByEntityAsync(Tid, EntityId, It.IsAny<CancellationToken>())).ReturnsAsync(20);
        _quota.Setup(q => q.EnsureUnderLimitAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.Validation("Plan", "Limite du plan atteinte : 20 vues par table maximum.")));

        var handler = new CreateCustomRecordViewCommandHandler(
            _entities.Object, _fields.Object, _views.Object, _quota.Object, _audit.Object, _currentUser.Object);
        var result = await handler.Handle(
            new CreateCustomRecordViewCommand("chantiers", new SaveCustomRecordViewRequest("vue_quota", "Vue quota", CustomRecordViewMode.List, ListDef())),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Plan", result.Error.Code);
    }

    [Fact]
    public async Task SetDefault_clears_then_sets_and_is_exclusive()
    {
        var view = View(CustomRecordViewMode.List, ListDef());
        _views.Setup(v => v.GetByIdAsync(Tid, EntityId, view.Id, It.IsAny<CancellationToken>())).ReturnsAsync(view);

        var handler = new SetDefaultCustomRecordViewCommandHandler(_entities.Object, _views.Object, _audit.Object, _currentUser.Object);
        var result = await handler.Handle(new SetDefaultCustomRecordViewCommand("chantiers", view.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _views.Verify(v => v.ClearDefaultAsync(Tid, EntityId, It.IsAny<CancellationToken>()), Times.Once);
        _views.Verify(v => v.UpdateAsync(It.Is<CustomRecordViewDefinition>(x => x.IsDefault), It.IsAny<CancellationToken>()), Times.Once);
        _audit.Verify(a => a.LogAsync("Studio.RecordView.DefaultSet", "CustomRecordViewDefinition", view.Id, null, It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- Run kanban ----

    [Fact]
    public async Task Run_kanban_groups_in_column_order_with_sans_valeur_and_truncates_at_the_bound()
    {
        var view = View(CustomRecordViewMode.Kanban, KanbanDef());
        _views.Setup(v => v.GetByIdAsync(Tid, EntityId, view.Id, It.IsAny<CancellationToken>())).ReturnsAsync(view);

        // 501 lignes au total mais borne 500 ⇒ truncated ; groupes dans l'ordre des options puis « Sans valeur ».
        var rows = new List<CustomRecord>
        {
            Rec("""{"nom":"t1","statut":"termine"}"""),
            Rec("""{"nom":"t2","statut":"encours"}"""),
            Rec("""{"nom":"t3"}""") // sans statut
        };
        _records.Setup(r => r.QueryAsync(It.IsAny<RecordQuerySpec>(), It.IsAny<IReadOnlyDictionary<string, CustomFieldType>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((rows, Total: 501));

        var handler = new RunCustomRecordViewQueryHandler(
            _entities.Object, _fields.Object, _views.Object, _records.Object, _jsonIndex.Object, _currentUser.Object,
            Options.Create(Settings(kanban: 500)));
        var result = await handler.Handle(
            new RunCustomRecordViewQuery("chantiers", view.Id, new RunRecordViewRequest()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var run = result.Value;
        Assert.Equal(CustomRecordViewMode.Kanban, run.Mode);
        Assert.True(run.Truncated); // 501 > 500
        Assert.NotNull(run.Groups);
        // Ordre des options : encours, termine, puis « Sans valeur ».
        Assert.Equal(new[] { "encours", "termine", null }, run.Groups!.Select(g => g.Value).ToArray());
        Assert.Equal("Sans valeur", run.Groups[^1].Label);
        Assert.Equal("t2", System.Text.Json.Nodes.JsonNode.Parse(run.Groups[0].Items.Single().Data!.ToJsonString())!["nom"]!.GetValue<string>());
        Assert.Equal("t3", run.Groups[^1].Items.Single().Data!["nom"]!.GetValue<string>());
    }

    // ---- Run calendrier ----

    [Fact]
    public async Task Run_calendar_requires_a_date_range_and_bounds_it_to_92_days()
    {
        var view = View(CustomRecordViewMode.Calendar, CalendarDef());
        _views.Setup(v => v.GetByIdAsync(Tid, EntityId, view.Id, It.IsAny<CancellationToken>())).ReturnsAsync(view);

        var handler = new RunCustomRecordViewQueryHandler(
            _entities.Object, _fields.Object, _views.Object, _records.Object, _jsonIndex.Object, _currentUser.Object,
            Options.Create(Settings()));

        // Fenêtre absente ⇒ 400 Validation.range
        var missing = await handler.Handle(
            new RunCustomRecordViewQuery("chantiers", view.Id, new RunRecordViewRequest()), CancellationToken.None);
        Assert.True(missing.IsFailure);
        Assert.Equal("Validation.range", missing.Error.Code);

        // Fenêtre > 92 jours ⇒ 400
        var tooWide = await handler.Handle(
            new RunCustomRecordViewQuery("chantiers", view.Id,
                new RunRecordViewRequest(RangeStart: new DateOnly(2026, 1, 1), RangeEnd: new DateOnly(2026, 6, 30))),
            CancellationToken.None);
        Assert.True(tooWide.IsFailure);
        Assert.Equal("Validation.range", tooWide.Error.Code);

        _records.Verify(r => r.QueryAsync(It.IsAny<RecordQuerySpec>(), It.IsAny<IReadOnlyDictionary<string, CustomFieldType>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Run_calendar_projects_events_within_the_range()
    {
        var view = View(CustomRecordViewMode.Calendar, CalendarDef());
        _views.Setup(v => v.GetByIdAsync(Tid, EntityId, view.Id, It.IsAny<CancellationToken>())).ReturnsAsync(view);

        var rows = new List<CustomRecord>
        {
            Rec("""{"nom":"Intervention A","debut":"2026-02-10","statut":"encours"}"""),
            Rec("""{"nom":"Intervention B","debut":"2026-02-12"}""")
        };
        _records.Setup(r => r.QueryAsync(It.IsAny<RecordQuerySpec>(), It.IsAny<IReadOnlyDictionary<string, CustomFieldType>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((rows, Total: 2));

        var handler = new RunCustomRecordViewQueryHandler(
            _entities.Object, _fields.Object, _views.Object, _records.Object, _jsonIndex.Object, _currentUser.Object,
            Options.Create(Settings()));
        var result = await handler.Handle(
            new RunCustomRecordViewQuery("chantiers", view.Id,
                new RunRecordViewRequest(RangeStart: new DateOnly(2026, 2, 1), RangeEnd: new DateOnly(2026, 2, 28))),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var run = result.Value;
        Assert.False(run.Truncated);
        Assert.NotNull(run.Events);
        Assert.Equal(2, run.Events!.Count);
        Assert.Equal("Intervention A", run.Events[0].Title);
        Assert.Equal("encours", run.Events[0].ColorValue);
        Assert.Null(run.Events[1].ColorValue);
    }

    private static CustomRecord Rec(string json) => CustomRecord.Create(Tid, EntityId, json, UserId);
}
