using System.Text.Json;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.RecordViews;
using FactuTrust.Application.Features.Studio.Systems;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

/// <summary>
/// Handler d'export d'un système Studio (PR 3.3, tranche 3.3c1) : gardes fail-closed (drapeau, tenant,
/// permission, forme de clé, système introuvable), chargement sans appel superflu (seed uniquement à la
/// demande et bornée), DTO miroir de l'exporteur, audit et logs sans contenu métier.
/// </summary>
public sealed class CustomSystemExportFeaturesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly Mock<ICustomSystemRepository> _systems = new(MockBehavior.Strict);
    private readonly Mock<ICustomEntityRepository> _entities = new(MockBehavior.Strict);
    private readonly Mock<ICustomFieldRepository> _fields = new(MockBehavior.Strict);
    private readonly Mock<ICustomFormRepository> _forms = new(MockBehavior.Strict);
    private readonly Mock<ICustomReportRepository> _reports = new(MockBehavior.Strict);
    private readonly Mock<ICustomRecordViewRepository> _views = new(MockBehavior.Strict);
    private readonly Mock<ICustomRecordRepository> _records = new(MockBehavior.Strict);
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IAuditService> _audit = new();
    private readonly Mock<ILogger<ExportCustomSystemQueryHandler>> _logger = new();

    private readonly OllamaSettings _settings = new() { EnableStudioSystemExport = true, StudioExportMaxSeedRows = 200 };

    public CustomSystemExportFeaturesTests()
    {
        _currentUser.Setup(x => x.TenantId).Returns(TenantId);
        _currentUser.Setup(x => x.UserId).Returns(UserId);
        _currentUser.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);
        _audit.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private ExportCustomSystemQueryHandler Handler() => new(
        _systems.Object, _entities.Object, _fields.Object, _forms.Object, _reports.Object, _views.Object, _records.Object,
        _currentUser.Object, _audit.Object, Options.Create(_settings), _logger.Object);

    private void VerifyNoRepositoryCalls()
    {
        _systems.VerifyNoOtherCalls();
        _entities.VerifyNoOtherCalls();
        _fields.VerifyNoOtherCalls();
        _forms.VerifyNoOtherCalls();
        _reports.VerifyNoOtherCalls();
        _views.VerifyNoOtherCalls();
        _records.VerifyNoOtherCalls();
    }

    // ---- Fixture : système « gestion_conges » (employes, conges + jonction) --------------------

    private sealed class Fixture
    {
        public CustomSystemDefinition System { get; } = CustomSystemDefinition.Create(
            TenantId, "gestion_conges", "Gestion des congés", "pi pi-calendar", "Suivi des congés.", null, null);

        public List<CustomEntityDefinition> Entities { get; } = new();
        public Dictionary<Guid, List<CustomFieldDefinition>> Fields { get; } = new();
        public Dictionary<Guid, List<CustomReportDefinition>> Reports { get; } = new();
        public Dictionary<Guid, List<CustomRecordViewDefinition>> Views { get; } = new();
        public Dictionary<Guid, List<CustomRecord>> Seed { get; } = new();

        public CustomEntityDefinition Employes { get; }
        public CustomEntityDefinition Conges { get; }
        public CustomEntityDefinition Junction { get; }

        public Fixture()
        {
            Employes = AddEntity("employes", "Employé", "Employés");
            AddField(Employes, "nom", "Nom", CustomFieldType.Text, required: true, sortOrder: 1);
            AddField(Employes, "service", "Service", CustomFieldType.RelationCustom, sortOrder: 2,
                optionsJson: StudioFieldJson.SerializeRelation(new RelationRefDto("custom", "services")));

            Conges = AddEntity("conges", "Congé", "Congés");
            AddField(Conges, "employe", "Employé", CustomFieldType.RelationCustom, required: true, sortOrder: 1,
                optionsJson: StudioFieldJson.SerializeRelation(new RelationRefDto("custom", "employes")));
            AddField(Conges, "debut", "Début", CustomFieldType.Date, sortOrder: 2);

            AddView(Conges, "Tous", CustomRecordViewMode.List, new RecordViewDefinition(
                new[] { new RecordViewColumn("employe"), new RecordViewColumn("debut") },
                Array.Empty<RecordViewFilter>(), Array.Empty<RecordViewSort>(), null, null), isDefault: true);

            Junction = AddEntity("employes_conges", "Participations", "Participations",
                description: "Participants — table de jonction employes ↔ conges.", kind: CustomEntityKind.Junction);
            AddField(Junction, "employe", "Employé", CustomFieldType.RelationCustom, required: true, sortOrder: 1,
                optionsJson: StudioFieldJson.SerializeRelation(new RelationRefDto("custom", "employes")));
            AddField(Junction, "conge", "Congé", CustomFieldType.RelationCustom, required: true, sortOrder: 2,
                optionsJson: StudioFieldJson.SerializeRelation(new RelationRefDto("custom", "conges")));

            Seed[Employes.Id] = new List<CustomRecord> { CustomRecord.Create(TenantId, Employes.Id, """{"nom":"Dupont"}""", null) };
            Seed[Conges.Id] = new List<CustomRecord>();
        }

        public CustomEntityDefinition AddEntity(string key, string name, string plural, string? description = null,
            CustomEntityKind kind = CustomEntityKind.Standard)
        {
            var entity = CustomEntityDefinition.Create(TenantId, key, name, plural, "pi pi-table", description, null, System.Id, kind);
            Entities.Add(entity);
            Fields[entity.Id] = new List<CustomFieldDefinition>();
            Reports[entity.Id] = new List<CustomReportDefinition>();
            Views[entity.Id] = new List<CustomRecordViewDefinition>();
            return entity;
        }

        public CustomFieldDefinition AddField(CustomEntityDefinition entity, string key, string label, CustomFieldType type,
            bool required = false, int sortOrder = 0, string? optionsJson = null)
        {
            var field = CustomFieldDefinition.Create(TenantId, entity.Id, key, label, type, required, false, sortOrder, null, optionsJson, null, null);
            Fields[entity.Id].Add(field);
            return field;
        }

        public CustomReportDefinition AddReport(CustomEntityDefinition entity, string key, string name, CustomReportDataSourceKind kind)
        {
            var report = CustomReportDefinition.Create(TenantId, key, name, kind, entity.Key,
                ReportDefinitionJson.Serialize(new ReportDefinition { Grouping = new[] { "debut" } }), null);
            Reports[entity.Id].Add(report);
            return report;
        }

        public void AddView(CustomEntityDefinition entity, string name, CustomRecordViewMode mode, RecordViewDefinition definition, bool isDefault = false)
        {
            Views[entity.Id].Add(CustomRecordViewDefinition.Create(TenantId, entity.Id, $"v_{Views[entity.Id].Count}", name, mode,
                RecordViewDefinitionJson.Serialize(definition), isDefault, null));
        }
    }

    /// <summary>Câble les dépôts Strict pour un export complet de la fixture (seed câblée séparément).</summary>
    private Fixture SetupLoadedSystem()
    {
        var f = new Fixture();
        _systems.Setup(s => s.GetByKeyAsync(TenantId, f.System.Key, It.IsAny<CancellationToken>())).ReturnsAsync(f.System);
        _entities.Setup(e => e.ListBySystemIdAsync(TenantId, f.System.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<CustomEntityDefinition>)f.Entities);

        foreach (var entity in f.Entities)
        {
            _fields.Setup(r => r.ListByEntityAsync(TenantId, entity.Id, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyList<CustomFieldDefinition>)f.Fields[entity.Id]);
            if (entity.Kind != CustomEntityKind.Standard) continue;

            _forms.Setup(r => r.GetDefaultByEntityAsync(TenantId, entity.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync((CustomFormDefinition?)null);
            _reports.Setup(r => r.ListAsync(TenantId, entity.Key, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => (IReadOnlyList<CustomReportDefinition>)f.Reports[entity.Id]);
            _views.Setup(r => r.ListByEntityAsync(TenantId, entity.Id, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyList<CustomRecordViewDefinition>)f.Views[entity.Id]);
        }
        return f;
    }

    private void SetupSeed(Fixture f, int expectedMax)
    {
        foreach (var entity in f.Entities.Where(e => e.Kind == CustomEntityKind.Standard))
            _records.Setup(r => r.GetAllForReportAsync(TenantId, entity.Id, expectedMax, It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyList<CustomRecord>)f.Seed[entity.Id]);
    }

    // ---- Gardes --------------------------------------------------------------------------------

    [Fact]
    public async Task Export_when_flag_off_returns_not_found_and_touches_no_repository()
    {
        _settings.EnableStudioSystemExport = false;

        var result = await Handler().Handle(new ExportCustomSystemQuery("gestion_conges"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
        VerifyNoRepositoryCalls();
    }

    [Fact]
    public async Task Export_without_tenant_returns_unauthorized()
    {
        _currentUser.Setup(x => x.TenantId).Returns((Guid?)null);

        var result = await Handler().Handle(new ExportCustomSystemQuery("gestion_conges"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Unauthorized", result.Error.Code);
        VerifyNoRepositoryCalls();
    }

    [Fact]
    public async Task Export_without_design_permission_returns_unauthorized()
    {
        _currentUser.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(false);

        var result = await Handler().Handle(new ExportCustomSystemQuery("gestion_conges"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Unauthorized", result.Error.Code);
        _currentUser.Verify(x => x.HasPermission(Permissions.Studio.DesignEntities), Times.Once);
        VerifyNoRepositoryCalls();
    }

    [Fact]
    public async Task Export_invalid_key_shape_returns_validation_error()
    {
        var result = await Handler().Handle(new ExportCustomSystemQuery("Bad Key!"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.key", result.Error.Code);
        VerifyNoRepositoryCalls();
    }

    [Fact]
    public async Task Export_unknown_key_returns_custom_system_not_found()
    {
        _systems.Setup(s => s.GetByKeyAsync(TenantId, "monsys", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomSystemDefinition?)null);

        var result = await Handler().Handle(new ExportCustomSystemQuery("  MonSys "), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("CustomSystem.NotFound", result.Error.Code);
        _systems.Verify(s => s.GetByKeyAsync(TenantId, "monsys", It.IsAny<CancellationToken>()), Times.Once);
        _entities.VerifyNoOtherCalls();
    }

    // ---- Chargement et seed --------------------------------------------------------------------

    [Fact]
    public async Task Export_without_seed_never_calls_record_repository()
    {
        var f = SetupLoadedSystem();

        var result = await Handler().Handle(new ExportCustomSystemQuery(f.System.Key), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IncludesSeed);
        Assert.Null(result.Value.Spec["seed"]);
        _records.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Export_with_seed_uses_clamped_max_rows_setting()
    {
        var f = SetupLoadedSystem();
        _settings.StudioExportMaxSeedRows = 5000;
        SetupSeed(f, expectedMax: 200);

        var result = await Handler().Handle(new ExportCustomSystemQuery(f.System.Key, IncludeSeed: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IncludesSeed);
        _records.Verify(r => r.GetAllForReportAsync(TenantId, f.Employes.Id, 200, It.IsAny<CancellationToken>()), Times.Once);
        _records.Verify(r => r.GetAllForReportAsync(TenantId, f.Conges.Id, 200, It.IsAny<CancellationToken>()), Times.Once);
        _records.Verify(r => r.GetAllForReportAsync(TenantId, f.Junction.Id, It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);

        // Borne à zéro : la seed est demandée mais aucun dépôt d'enregistrements n'est sollicité.
        _records.Reset();
        _settings.StudioExportMaxSeedRows = 0;

        var zero = await Handler().Handle(new ExportCustomSystemQuery(f.System.Key, IncludeSeed: true), CancellationToken.None);

        Assert.True(zero.IsSuccess);
        Assert.False(zero.Value.IncludesSeed);
        _records.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Export_dto_mirrors_exporter_counts_and_warnings()
    {
        var f = SetupLoadedSystem();

        var result = await Handler().Handle(new ExportCustomSystemQuery(f.System.Key), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value;
        Assert.Equal(StudioSystemSpecExporter.SpecVersion, dto.SpecVersion);
        Assert.Equal(1, dto.SpecVersion);
        Assert.Equal("gestion_conges", dto.SystemKey);
        Assert.Equal("Gestion des congés", dto.SystemDisplayName);
        Assert.Equal(2, dto.EntityCount);
        Assert.Equal(1, dto.RelationCount);
        Assert.Equal(1, dto.ViewCount);
        Assert.False(dto.IncludesSeed);
        // Relation « services » hors système ⇒ avertissement propagé par l'exporteur.
        Assert.Contains(dto.Warnings, w => w.Contains("services", StringComparison.Ordinal));
        Assert.Equal(1, dto.Spec["specVersion"]!.GetValue<int>());
        Assert.Equal("gestion_conges", dto.Spec["exportedFrom"]!["tenantSystemKey"]!.GetValue<string>());
        Assert.Equal(2, dto.Spec["entities"]!.AsArray().Count);
        Assert.Equal(1, dto.Spec["relations"]!.AsArray().Count);
    }

    [Fact]
    public async Task Export_filters_reports_to_custom_entity_kind()
    {
        var f = SetupLoadedSystem();
        f.AddReport(f.Conges, "r_existing", "Rapport ERP", CustomReportDataSourceKind.ExistingSource);
        f.AddReport(f.Conges, "r_custom", "Congés par date", CustomReportDataSourceKind.CustomEntity);

        var result = await Handler().Handle(new ExportCustomSystemQuery(f.System.Key), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var conges = result.Value.Spec["entities"]!.AsArray().Select(e => e!.AsObject())
            .Single(e => e["ref"]!.GetValue<string>() == "conges");
        Assert.Equal("Congés par date", conges["report"]!["displayName"]!.GetValue<string>());
        // Un seul rapport CustomEntity retenu ⇒ aucun avertissement « rapport(s) supplémentaire(s) ».
        Assert.DoesNotContain(result.Value.Warnings, w => w.Contains("supplémentaire", StringComparison.Ordinal));
    }

    // ---- Audit et logs -------------------------------------------------------------------------

    [Fact]
    public async Task Export_writes_audit_with_key_include_seed_and_entity_count_only()
    {
        var f = SetupLoadedSystem();
        object? captured = null;
        _audit.Setup(a => a.LogAsync("Studio.System.Exported", "CustomSystem", f.System.Id, null, It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Callback((string _, string _, Guid? _, object? _, object? newValues, CancellationToken _) => captured = newValues)
            .Returns(Task.CompletedTask);

        var result = await Handler().Handle(new ExportCustomSystemQuery(f.System.Key), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(captured);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(captured));
        var keys = doc.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(k => k, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "entityCount", "includeSeed", "systemKey" }, keys);
        Assert.Equal("gestion_conges", doc.RootElement.GetProperty("systemKey").GetString());
        Assert.False(doc.RootElement.GetProperty("includeSeed").GetBoolean());
        Assert.Equal(2, doc.RootElement.GetProperty("entityCount").GetInt32());
    }

    [Fact]
    public async Task Export_log_never_contains_spec_content()
    {
        var f = SetupLoadedSystem();
        SetupSeed(f, expectedMax: 200);
        var messages = new List<string>();
        _logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        _logger.Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(new InvocationAction(inv => messages.Add(inv.Arguments[2].ToString() ?? string.Empty)));

        var result = await Handler().Handle(new ExportCustomSystemQuery(f.System.Key, IncludeSeed: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IncludesSeed);
        Assert.NotEmpty(messages);
        Assert.All(messages, m => Assert.DoesNotContain("Dupont", m, StringComparison.Ordinal));
        Assert.All(messages, m => Assert.DoesNotContain("specVersion", m, StringComparison.Ordinal));
        Assert.Contains(messages, m => m.Contains("gestion_conges", StringComparison.Ordinal));
    }
}
