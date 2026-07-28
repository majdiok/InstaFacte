using System.Text.Json;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Features.Studio.Ai;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.Entities;
using FactuTrust.Application.Features.Studio.Fields;
using FactuTrust.Application.Features.Studio.Forms;
using FactuTrust.Application.Features.Studio.Records;
using FactuTrust.Application.Features.Studio.Reports;
using FactuTrust.Application.Features.Studio.Systems;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Services.Studio;
using MediatR;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

public sealed class StudioAiSystemOrchestratorTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<ICurrentUser> _currentUser = new();

    public StudioAiSystemOrchestratorTests()
    {
        _currentUser.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);
    }

    [Fact]
    public async Task Seed_failure_yields_partial_success_without_rollback()
    {
        const string json = """
        { "system": { "displayName": "Sys" }, "entities": [
          { "ref": "tickets", "displayName": "Tickets", "fields": [ { "label": "Nom", "type": "text" } ] }
        ], "seed": [ { "entityRef": "tickets", "records": [ { "nom": "CP" } ] } ] }
        """;
        Assert.True(StudioAiSystemSpec.TryParse(json, out var spec, out var err), err);

        SetupHappyStructure();
        // The single seed record fails — must NOT nuke the whole system.
        _mediator.Setup(m => m.Send(It.IsAny<CreateCustomRecordCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<CustomRecordDto>(Error.Validation("seed", "boom")));

        var orchestrator = new StudioAiSystemOrchestrator(_mediator.Object, _currentUser.Object);
        var (success, error, payload) = await orchestrator.ExecuteAsync(spec!, null, CancellationToken.None);

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(payload);
        Assert.Contains("ignor", JsonSerializer.Serialize(payload), StringComparison.OrdinalIgnoreCase);
        _mediator.Verify(m => m.Send(It.IsAny<DeleteCustomEntityCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Non_guid_relation_seed_value_is_dropped_and_record_still_saves()
    {
        const string json = """
        { "system": { "displayName": "Sys" }, "entities": [
          { "ref": "employes", "displayName": "Employes", "fields": [ { "label": "Nom", "type": "text" } ] },
          { "ref": "demandes", "displayName": "Demandes", "fields": [
            { "label": "Employe", "type": "relation", "relationTo": "employes" },
            { "label": "Jours", "type": "number" }
          ] }
        ], "seed": [ { "entityRef": "demandes", "records": [ { "employe": "Jean Dupont", "jours": 3 } ] } ] }
        """;
        Assert.True(StudioAiSystemSpec.TryParse(json, out var spec, out var err), err);

        SetupHappyStructure();

        var orchestrator = new StudioAiSystemOrchestrator(_mediator.Object, _currentUser.Object);
        var (success, _, _) = await orchestrator.ExecuteAsync(spec!, null, CancellationToken.None);

        Assert.True(success);
        // The non-GUID relation value is dropped; the scalar field is kept so the record still saves.
        _mediator.Verify(m => m.Send(
            It.Is<CreateCustomRecordCommand>(c => !c.Request.Data.ContainsKey("employe") && c.Request.Data.ContainsKey("jours")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Entity_creation_failure_rolls_back_created_tables()
    {
        const string json = """
        { "system": { "displayName": "Sys" }, "entities": [
          { "ref": "a", "displayName": "A", "fields": [ { "label": "Nom", "type": "text" } ] },
          { "ref": "b", "displayName": "B", "fields": [ { "label": "Nom", "type": "text" } ] }
        ] }
        """;
        Assert.True(StudioAiSystemSpec.TryParse(json, out var spec, out var err), err);

        _mediator.Setup(m => m.Send(It.IsAny<ListCustomSystemsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<CustomSystemDto>>(new List<CustomSystemDto>()));
        _mediator.Setup(m => m.Send(It.IsAny<CreateCustomSystemCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(SystemDto()));
        _mediator.Setup(m => m.Send(It.IsAny<ListCustomEntitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<CustomEntityDto>>(new List<CustomEntityDto>()));
        _mediator.SetupSequence(m => m.Send(It.IsAny<CreateCustomEntityCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(EntityDto("a")))
            .ReturnsAsync(Result.Failure<CustomEntityDto>(Error.Validation("entity", "boom")));
        _mediator.Setup(m => m.Send(It.IsAny<CreateCustomFieldCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(FieldDto()));
        _mediator.Setup(m => m.Send(It.IsAny<DeleteCustomEntityCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _mediator.Setup(m => m.Send(It.IsAny<DeleteCustomSystemCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var orchestrator = new StudioAiSystemOrchestrator(_mediator.Object, _currentUser.Object);
        var (success, _, payload) = await orchestrator.ExecuteAsync(spec!, null, CancellationToken.None);

        Assert.False(success);
        Assert.Null(payload);
        _mediator.Verify(m => m.Send(It.IsAny<DeleteCustomEntityCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        // Le système orphelin est aussi supprimé (correctif rollback).
        _mediator.Verify(m => m.Send(It.IsAny<DeleteCustomSystemCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Preflight_quota_failure_creates_nothing()
    {
        const string json = """
        { "system": { "displayName": "Paie" }, "entities": [
          { "ref": "employes", "displayName": "Employes", "fields": [ { "label": "Nom", "type": "text" } ] },
          { "ref": "salaires", "displayName": "Salaires", "fields": [ { "label": "Montant", "type": "money" } ] }
        ] }
        """;
        Assert.True(StudioAiSystemSpec.TryParse(json, out var spec, out var err), err);

        _currentUser.Setup(x => x.TenantId).Returns(Guid.NewGuid());
        _mediator.Setup(m => m.Send(It.IsAny<ListCustomEntitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<CustomEntityDto>>(new List<CustomEntityDto>()));
        var quota = new Mock<IStudioQuotaService>();
        quota.Setup(q => q.EnsureUnderLimitAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.Validation("Plan", "Limite du plan atteinte : 50 tables maximum.")));

        var orchestrator = new StudioAiSystemOrchestrator(_mediator.Object, _currentUser.Object, quota.Object);
        var (success, error, payload) = await orchestrator.ExecuteAsync(spec!, null, CancellationToken.None);

        Assert.False(success);
        Assert.Null(payload);
        Assert.Contains("Limite du plan", error);
        // Rien n'est créé : pas de système ni de table (donc pas d'orphelin).
        _mediator.Verify(m => m.Send(It.IsAny<CreateCustomSystemCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        _mediator.Verify(m => m.Send(It.IsAny<CreateCustomEntityCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Form_widths_labels_and_report_filters_flow_into_commands()
    {
        const string json = """
        { "system": { "displayName": "Contrats" }, "entities": [
          { "ref": "contrats", "displayName": "Contrats", "fields": [
            { "label": "Nom", "type": "text" },
            { "label": "Date de debut", "type": "date" },
            { "label": "Montant", "type": "money" },
            { "label": "Statut", "type": "select", "options": [ {"value":"actif","label":"Actif"}, {"value":"expire","label":"Expiré"} ] }
          ],
          "form": { "sections": [ { "title": "Général", "fields": [
            "nom",
            { "field": "date_de_debut", "width": "half" },
            { "field": "montant", "width": "half", "label": "Montant TTC" },
            { "field": "statut", "width": "sideways" }
          ] } ] },
          "report": { "displayName": "Contrats actifs", "groupBy": ["statut"], "measures": [ {"field":"montant","fn":"sum"} ],
            "filters": [ { "field": "statut", "op": "eq", "value": "actif" } ],
            "sort": [ { "field": "sum_montant", "dir": "desc" } ] } }
        ] }
        """;
        Assert.True(StudioAiSystemSpec.TryParse(json, out var spec, out var err), err);

        SetupHappyStructure();
        FormLayout? savedLayout = null;
        _mediator.Setup(m => m.Send(It.IsAny<UpsertDefaultFormCommand>(), It.IsAny<CancellationToken>()))
            .Callback((IRequest<Result<CustomFormDto>> cmd, CancellationToken _) =>
                savedLayout = ((UpsertDefaultFormCommand)cmd).Request.Layout)
            .ReturnsAsync(Result.Success(new CustomFormDto(Guid.NewGuid(), "f", "F", true, new FormLayout())));
        ReportDefinition? savedDef = null;
        _mediator.Setup(m => m.Send(It.IsAny<UpsertCustomReportCommand>(), It.IsAny<CancellationToken>()))
            .Callback((IRequest<Result<CustomReportDto>> cmd, CancellationToken _) =>
                savedDef = ((UpsertCustomReportCommand)cmd).Request.Definition)
            .ReturnsAsync(Result.Success(new CustomReportDto(Guid.NewGuid(), "r", "R",
                CustomReportDataSourceKind.CustomEntity, "contrats", new ReportDefinition(), true)));

        var orchestrator = new StudioAiSystemOrchestrator(_mediator.Object, _currentUser.Object);
        var (success, error, _) = await orchestrator.ExecuteAsync(spec!, null, CancellationToken.None);

        Assert.True(success, error);
        Assert.NotNull(savedLayout);
        var fields = savedLayout!.Sections[0].Fields;
        Assert.Equal("full", fields[0].Width);                // entrée chaîne → pleine largeur
        Assert.Equal("half", fields[1].Width);
        Assert.Equal("half", fields[2].Width);
        Assert.Equal("Montant TTC", fields[2].LabelOverride);
        Assert.Equal("full", fields[3].Width);                // largeur invalide → full

        Assert.NotNull(savedDef);
        Assert.Single(savedDef!.Filters);
        Assert.Equal("statut", savedDef.Filters[0].Field);
        Assert.Equal("eq", savedDef.Filters[0].Op);
        Assert.Single(savedDef.Sort);
        Assert.Equal("sum_montant", savedDef.Sort[0].Field);
        Assert.Equal("desc", savedDef.Sort[0].Dir);
        Assert.Contains("statut", savedDef.Grouping);
    }

    private void SetupHappyStructure()
    {
        _mediator.Setup(m => m.Send(It.IsAny<ListCustomSystemsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<CustomSystemDto>>(new List<CustomSystemDto>()));
        _mediator.Setup(m => m.Send(It.IsAny<CreateCustomSystemCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(SystemDto()));
        _mediator.Setup(m => m.Send(It.IsAny<ListCustomEntitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<CustomEntityDto>>(new List<CustomEntityDto>()));
        var entitySeq = _mediator.SetupSequence(m => m.Send(It.IsAny<CreateCustomEntityCommand>(), It.IsAny<CancellationToken>()));
        for (var i = 0; i < 8; i++)
            entitySeq = entitySeq.ReturnsAsync(Result.Success(EntityDto($"e{i}")));
        _mediator.Setup(m => m.Send(It.IsAny<CreateCustomFieldCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(FieldDto()));
        _mediator.Setup(m => m.Send(It.IsAny<CreateCustomRecordCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(RecordDto()));
        _mediator.Setup(m => m.Send(It.IsAny<DeleteCustomEntityCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
    }

    private static CustomSystemDto SystemDto() =>
        new(Guid.NewGuid(), "sys", "Sys", null, null, null, true, 0, DateTime.UtcNow, DateTime.UtcNow);

    private static CustomEntityDto EntityDto(string key = "t") =>
        new(Guid.NewGuid(), key, "T", "T", null, null, true, 0, Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow);

    private static CustomFieldDto FieldDto() =>
        new(Guid.NewGuid(), "nom", "Nom", CustomFieldType.Text, false, false, 0, null, null, null, true, null);

    private static CustomRecordDto RecordDto() =>
        new(Guid.NewGuid(), null, DateTime.UtcNow, DateTime.UtcNow, null);
}
