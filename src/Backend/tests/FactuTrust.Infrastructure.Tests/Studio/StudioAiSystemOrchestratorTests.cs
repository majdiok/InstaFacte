using System.Text.Json;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Studio.Ai;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.Entities;
using FactuTrust.Application.Features.Studio.Fields;
using FactuTrust.Application.Features.Studio.Forms;
using FactuTrust.Application.Features.Studio.Records;
using FactuTrust.Application.Features.Studio.Relations;
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

    // ---- Réutilisation de tables existantes (existingKey — PR 1.3) ----

    [Fact]
    public async Task Reused_entity_is_never_created_nor_written_but_relations_point_at_it()
    {
        const string json = """
        { "system": { "displayName": "Suivi" }, "entities": [
          { "ref": "employes", "existingKey": "employes" },
          { "ref": "demandes", "displayName": "Demandes", "fields": [
            { "label": "Employé", "type": "relation", "relationTo": "employes" },
            { "label": "Statut", "type": "text" }
          ] }
        ] }
        """;
        Assert.True(StudioAiSystemSpec.TryParse(json, out var spec, out var err), err);

        SetupHappyStructure();
        var reusedId = Guid.NewGuid();
        _mediator.Setup(m => m.Send(It.Is<GetCustomEntitySchemaQuery>(q => q.EntityKey == "employes"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new CustomEntitySchemaDto(
                new CustomEntityDto(reusedId, "employes", "Employés", "Employés", null, null, true, 2, Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow),
                new List<CustomFieldDto>(), new FormLayout())));

        var orchestrator = new StudioAiSystemOrchestrator(_mediator.Object, _currentUser.Object);
        var (success, error, payload) = await orchestrator.ExecuteAsync(spec!, null, CancellationToken.None);

        Assert.True(success, error);
        // UNE seule table créée (« demandes ») : jamais d'écriture pour la table réutilisée.
        _mediator.Verify(m => m.Send(It.IsAny<CreateCustomEntityCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(It.Is<CreateCustomFieldCommand>(c => c.EntityId == reusedId), It.IsAny<CancellationToken>()), Times.Never);
        // La relation de « demandes » vise la VRAIE clé de la table réutilisée.
        _mediator.Verify(m => m.Send(It.Is<CreateCustomFieldCommand>(c =>
                c.Request.FieldType == CustomFieldType.RelationCustom
                && c.Request.Relation != null && c.Request.Relation.Kind == "custom" && c.Request.Relation.Ref == "employes"),
            It.IsAny<CancellationToken>()), Times.Once);
        // Le payload distingue création et réutilisation.
        var json2 = JsonSerializer.Serialize(payload);
        Assert.Contains("\"reused\":true", json2, StringComparison.Ordinal);
        Assert.Contains("\"reusedCount\":1", json2, StringComparison.Ordinal);
        Assert.Contains("\"createdCount\":1", json2, StringComparison.Ordinal);
        // La table réutilisée n'est JAMAIS supprimée en cas de nettoyage.
        _mediator.Verify(m => m.Send(It.Is<DeleteCustomEntityCommand>(c => c.Id == reusedId), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Unknown_existing_key_fails_before_any_write()
    {
        const string json = """
        { "system": { "displayName": "Suivi" }, "entities": [
          { "ref": "employes", "existingKey": "employes" },
          { "ref": "demandes", "displayName": "Demandes", "fields": [ { "label": "Statut" } ] }
        ] }
        """;
        Assert.True(StudioAiSystemSpec.TryParse(json, out var spec, out var err), err);

        _mediator.Setup(m => m.Send(It.IsAny<GetCustomEntitySchemaQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<CustomEntitySchemaDto>(Error.Validation("entityKey", "introuvable")));

        var orchestrator = new StudioAiSystemOrchestrator(_mediator.Object, _currentUser.Object);
        var (success, error, payload) = await orchestrator.ExecuteAsync(spec!, null, CancellationToken.None);

        Assert.False(success);
        Assert.Null(payload);
        Assert.Contains("employes", error!);
        _mediator.Verify(m => m.Send(It.IsAny<CreateCustomSystemCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        _mediator.Verify(m => m.Send(It.IsAny<CreateCustomEntityCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Inactive_existing_key_fails_before_any_write()
    {
        const string json = """
        { "system": { "displayName": "Suivi" }, "entities": [
          { "ref": "employes", "existingKey": "employes" },
          { "ref": "demandes", "displayName": "Demandes", "fields": [ { "label": "Statut" } ] }
        ] }
        """;
        Assert.True(StudioAiSystemSpec.TryParse(json, out var spec, out var err), err);

        _mediator.Setup(m => m.Send(It.IsAny<GetCustomEntitySchemaQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new CustomEntitySchemaDto(
                new CustomEntityDto(Guid.NewGuid(), "employes", "Employés", "Employés", null, null, false, 0, Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow),
                new List<CustomFieldDto>(), new FormLayout())));

        var orchestrator = new StudioAiSystemOrchestrator(_mediator.Object, _currentUser.Object);
        var (success, _, _) = await orchestrator.ExecuteAsync(spec!, null, CancellationToken.None);

        Assert.False(success);
        _mediator.Verify(m => m.Send(It.IsAny<CreateCustomSystemCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Seed_targeting_a_reused_entity_is_skipped_with_a_warning()
    {
        const string json = """
        { "system": { "displayName": "Suivi" }, "entities": [
          { "ref": "employes", "existingKey": "employes" },
          { "ref": "demandes", "displayName": "Demandes", "fields": [ { "label": "Statut" } ] }
        ], "seed": [ { "entityRef": "employes", "records": [ { "nom": "Sami" } ] } ] }
        """;
        Assert.True(StudioAiSystemSpec.TryParse(json, out var spec, out var err), err);

        SetupHappyStructure();
        _mediator.Setup(m => m.Send(It.IsAny<GetCustomEntitySchemaQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new CustomEntitySchemaDto(
                EntityDto("employes"), new List<CustomFieldDto>(), new FormLayout())));

        var orchestrator = new StudioAiSystemOrchestrator(_mediator.Object, _currentUser.Object);
        var (success, error, payload) = await orchestrator.ExecuteAsync(spec!, null, CancellationToken.None);

        Assert.True(success, error);
        // AUCUN enregistrement écrit : le seul lot de seed visait la table réutilisée.
        _mediator.Verify(m => m.Send(It.IsAny<CreateCustomRecordCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Contains("aucune", JsonSerializer.Serialize(payload), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Preflight_quota_counts_only_new_entities()
    {
        const string json = """
        { "system": { "displayName": "Suivi" }, "entities": [
          { "ref": "employes", "existingKey": "employes" },
          { "ref": "demandes", "displayName": "Demandes", "fields": [ { "label": "Statut" } ] }
        ] }
        """;
        Assert.True(StudioAiSystemSpec.TryParse(json, out var spec, out var err), err);

        _currentUser.Setup(x => x.TenantId).Returns(Guid.NewGuid());
        _mediator.Setup(m => m.Send(It.IsAny<ListCustomEntitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<CustomEntityDto>>(new List<CustomEntityDto> { EntityDto("a"), EntityDto("b") }));
        var quota = new Mock<IStudioQuotaService>();
        int? requested = null;
        quota.Setup(q => q.EnsureUnderLimitAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((Guid _, string _, int count, int _, string _, CancellationToken _) => requested = count)
            .ReturnsAsync(Result.Failure(Error.Validation("Plan", "stop")));
        _mediator.Setup(m => m.Send(It.IsAny<GetCustomEntitySchemaQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new CustomEntitySchemaDto(
                EntityDto("employes"), new List<CustomFieldDto>(), new FormLayout())));

        var orchestrator = new StudioAiSystemOrchestrator(_mediator.Object, _currentUser.Object, quota.Object);
        var (success, _, _) = await orchestrator.ExecuteAsync(spec!, null, CancellationToken.None);

        Assert.False(success);
        // 2 tables existantes + 1 NOUVELLE (la réutilisée ne compte pas) - 1 = 2.
        Assert.Equal(2, requested);
    }

    // ---- Relations plusieurs-à-plusieurs et orchestration multi-passes (PR 2.2) ----

    [Fact]
    public async Task Relation_field_pointing_to_a_later_entity_resolves_without_degrading()
    {
        // La référence en avant ("contrats" pointe vers "clients" déclarée après lui) ne doit plus
        // dégrader la relation : entityKeyMap est complet dès la fin de la passe 1, avant la passe 2.
        const string json = """
        { "system": { "displayName": "Sys" }, "entities": [
          { "ref": "contrats", "displayName": "Contrats", "fields": [
            { "label": "Nom", "type": "text" },
            { "label": "Client", "type": "relation", "relationTo": "clients_perso" }
          ] },
          { "ref": "fournisseurs", "displayName": "Fournisseurs", "fields": [ { "label": "Nom", "type": "text" } ] },
          { "ref": "clients_perso", "displayName": "Clients perso", "fields": [ { "label": "Nom", "type": "text" } ] }
        ] }
        """;
        Assert.True(StudioAiSystemSpec.TryParse(json, out var spec, out var err), err);

        SetupHappyStructure();

        var orchestrator = new StudioAiSystemOrchestrator(_mediator.Object, _currentUser.Object);
        var (success, error, _) = await orchestrator.ExecuteAsync(spec!, null, CancellationToken.None);

        Assert.True(success, error);
        // "contrats" est le 1er créé (clé e0), "clients_perso" le 3ᵉ (clé e2, via SetupHappyStructure) :
        // la relation doit viser la VRAIE clé de "clients_perso", jamais un texte dégradé.
        _mediator.Verify(m => m.Send(
            It.Is<CreateCustomFieldCommand>(c =>
                c.Request.FieldType == CustomFieldType.RelationCustom
                && c.Request.Relation != null && c.Request.Relation.Kind == "custom" && c.Request.Relation.Ref == "e2"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Command_order_respects_the_five_passes()
    {
        const string json = """
        { "system": { "displayName": "Sys" }, "entities": [
          { "ref": "employes", "displayName": "Employes", "fields": [ { "label": "Nom", "type": "text" } ],
            "report": { "displayName": "Rapport", "columns": ["nom"] } },
          { "ref": "formations", "displayName": "Formations", "fields": [
            { "label": "Nom", "type": "text" },
            { "label": "Employe", "type": "relation", "relationTo": "employes" }
          ], "form": { "sections": [ { "title": "Général", "fields": [ "nom" ] } ] } }
        ], "relations": [ { "kind": "many_to_many", "from": "employes", "to": "formations" } ],
        "seed": [ { "entityRef": "employes", "records": [ { "nom": "Sami" } ] } ] }
        """;
        Assert.True(StudioAiSystemSpec.TryParse(json, out var spec, out var err), err);

        SetupHappyStructure();
        SetupJunctionSuccess();
        _mediator.Setup(m => m.Send(It.IsAny<UpsertDefaultFormCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new CustomFormDto(Guid.NewGuid(), "f", "F", true, new FormLayout())));
        _mediator.Setup(m => m.Send(It.IsAny<UpsertCustomReportCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new CustomReportDto(Guid.NewGuid(), "r", "R",
                CustomReportDataSourceKind.CustomEntity, "employes", new ReportDefinition(), true)));
        var settings = new OllamaSettings { EnableStudioManyToMany = true };

        var orchestrator = new StudioAiSystemOrchestrator(_mediator.Object, _currentUser.Object, null, settings);
        var (success, error, _) = await orchestrator.ExecuteAsync(spec!, null, CancellationToken.None);

        Assert.True(success, error);

        var invocations = _mediator.Invocations
            .Select((inv, idx) => (Idx: idx, Arg: inv.Arguments[0]))
            .ToList();
        int FirstIndexOf<T>(Func<T, bool>? predicate = null) => invocations
            .Where(x => x.Arg is T t && (predicate is null || predicate(t)))
            .Select(x => x.Idx).DefaultIfEmpty(int.MaxValue).Min();
        int LastIndexOf<T>(Func<T, bool>? predicate = null) => invocations
            .Where(x => x.Arg is T t && (predicate is null || predicate(t)))
            .Select(x => x.Idx).DefaultIfEmpty(-1).Max();

        var lastSimpleField = LastIndexOf<CreateCustomFieldCommand>(c => c.Request.FieldType != CustomFieldType.RelationCustom);
        var firstRelationField = FirstIndexOf<CreateCustomFieldCommand>(c => c.Request.FieldType == CustomFieldType.RelationCustom);
        var junctionIdx = FirstIndexOf<CreateManyToManyRelationCommand>();
        var formIdx = FirstIndexOf<UpsertDefaultFormCommand>();
        var reportIdx = FirstIndexOf<UpsertCustomReportCommand>();
        var seedIdx = FirstIndexOf<CreateCustomRecordCommand>();

        Assert.True(lastSimpleField < firstRelationField, "les champs simples doivent précéder les relations");
        Assert.True(firstRelationField < junctionIdx, "les relations simples doivent précéder les jonctions N-N");
        Assert.True(junctionIdx < formIdx, "les jonctions doivent précéder les formulaires");
        Assert.True(junctionIdx < reportIdx, "les jonctions doivent précéder les rapports");
        Assert.True(Math.Max(formIdx, reportIdx) < seedIdx, "le seed doit être la toute dernière passe");
    }

    [Fact]
    public async Task Junction_is_created_for_each_relation_when_the_flag_is_on()
    {
        const string json = """
        { "system": { "displayName": "Sys" }, "entities": [
          { "ref": "employes", "displayName": "Employes", "fields": [ { "label": "Nom", "type": "text" } ] },
          { "ref": "formations", "displayName": "Formations", "fields": [ { "label": "Nom", "type": "text" } ] }
        ], "relations": [ { "kind": "many_to_many", "from": "employes", "to": "formations", "label": "Participants" } ] }
        """;
        Assert.True(StudioAiSystemSpec.TryParse(json, out var spec, out var err), err);

        SetupHappyStructure();
        Guid? sourceId = null, targetId = null;
        CreateManyToManyRelationRequest? capturedRequest = null;
        _mediator.Setup(m => m.Send(It.IsAny<CreateManyToManyRelationCommand>(), It.IsAny<CancellationToken>()))
            .Callback((IRequest<Result<ManyToManyRelationDto>> cmd, CancellationToken _) =>
            {
                var c = (CreateManyToManyRelationCommand)cmd;
                sourceId = c.SourceEntityId;
                targetId = c.Request.TargetEntityId;
                capturedRequest = c.Request;
            })
            .ReturnsAsync(Result.Success(JunctionDto()));
        var settings = new OllamaSettings { EnableStudioManyToMany = true };

        var orchestrator = new StudioAiSystemOrchestrator(_mediator.Object, _currentUser.Object, null, settings);
        var (success, error, payload) = await orchestrator.ExecuteAsync(spec!, null, CancellationToken.None);

        Assert.True(success, error);
        _mediator.Verify(m => m.Send(It.IsAny<CreateManyToManyRelationCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(sourceId);
        Assert.NotNull(targetId);
        Assert.NotEqual(sourceId, targetId);
        Assert.Equal("Participants", capturedRequest!.Label);
        var json2 = JsonSerializer.Serialize(payload);
        Assert.Contains("junction", json2, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("openUrl", json2, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Flag_off_skips_junction_creation_and_warns()
    {
        const string json = """
        { "system": { "displayName": "Sys" }, "entities": [
          { "ref": "employes", "displayName": "Employes", "fields": [ { "label": "Nom", "type": "text" } ] },
          { "ref": "formations", "displayName": "Formations", "fields": [ { "label": "Nom", "type": "text" } ] }
        ], "relations": [ { "kind": "many_to_many", "from": "employes", "to": "formations" } ] }
        """;
        Assert.True(StudioAiSystemSpec.TryParse(json, out var spec, out var err), err);

        SetupHappyStructure();

        // Pas de settings (donc drapeau OFF) — comportement par défaut.
        var orchestrator = new StudioAiSystemOrchestrator(_mediator.Object, _currentUser.Object);
        var (success, error, payload) = await orchestrator.ExecuteAsync(spec!, null, CancellationToken.None);

        Assert.True(success, error);
        _mediator.Verify(m => m.Send(It.IsAny<CreateManyToManyRelationCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        var json2 = JsonSerializer.Serialize(payload);
        Assert.Contains("EnableStudioManyToMany", json2, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Junction_failure_does_not_abort_the_build_and_is_reported_as_a_warning()
    {
        const string json = """
        { "system": { "displayName": "Sys" }, "entities": [
          { "ref": "employes", "displayName": "Employes", "fields": [ { "label": "Nom", "type": "text" } ] },
          { "ref": "formations", "displayName": "Formations", "fields": [ { "label": "Nom", "type": "text" } ] }
        ], "relations": [ { "kind": "many_to_many", "from": "employes", "to": "formations" } ] }
        """;
        Assert.True(StudioAiSystemSpec.TryParse(json, out var spec, out var err), err);

        SetupHappyStructure();
        _mediator.Setup(m => m.Send(It.IsAny<CreateManyToManyRelationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<ManyToManyRelationDto>(Error.Conflict("clé déjà utilisée")));
        var settings = new OllamaSettings { EnableStudioManyToMany = true };

        var orchestrator = new StudioAiSystemOrchestrator(_mediator.Object, _currentUser.Object, null, settings);
        var (success, error, payload) = await orchestrator.ExecuteAsync(spec!, null, CancellationToken.None);

        Assert.True(success, error);
        // Pas de rollback global pour un échec de jonction, seulement un avertissement.
        _mediator.Verify(m => m.Send(It.IsAny<DeleteCustomEntityCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        var warningsProp = payload!.GetType().GetProperty("warnings");
        var payloadWarnings = (System.Collections.Generic.IEnumerable<string>)warningsProp!.GetValue(payload)!;
        Assert.Contains(payloadWarnings, w => w.Contains("clé déjà utilisée", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Quota_precheck_includes_relations_when_the_flag_is_on()
    {
        const string json = """
        { "system": { "displayName": "Sys" }, "entities": [
          { "ref": "employes", "displayName": "Employes", "fields": [ { "label": "Nom", "type": "text" } ] },
          { "ref": "formations", "displayName": "Formations", "fields": [ { "label": "Nom", "type": "text" } ] }
        ], "relations": [ { "kind": "many_to_many", "from": "employes", "to": "formations" } ] }
        """;
        Assert.True(StudioAiSystemSpec.TryParse(json, out var spec, out var err), err);

        _currentUser.Setup(x => x.TenantId).Returns(Guid.NewGuid());
        _mediator.Setup(m => m.Send(It.IsAny<ListCustomEntitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<CustomEntityDto>>(new List<CustomEntityDto>()));
        var quota = new Mock<IStudioQuotaService>();
        int? requested = null;
        quota.Setup(q => q.EnsureUnderLimitAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((Guid _, string _, int count, int _, string _, CancellationToken _) => requested = count)
            .ReturnsAsync(Result.Failure(Error.Validation("Plan", "stop")));
        var settings = new OllamaSettings { EnableStudioManyToMany = true };

        var orchestrator = new StudioAiSystemOrchestrator(_mediator.Object, _currentUser.Object, quota.Object, settings);
        var (success, _, _) = await orchestrator.ExecuteAsync(spec!, null, CancellationToken.None);

        Assert.False(success);
        // 0 existantes + 2 NOUVELLES entités + 1 relation N-N (drapeau ON) - 1 = 2.
        Assert.Equal(2, requested);
    }

    private void SetupJunctionSuccess()
    {
        _mediator.Setup(m => m.Send(It.IsAny<CreateManyToManyRelationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(JunctionDto()));
    }

    private static ManyToManyRelationDto JunctionDto() => new(
        EntityDto("employes_formations"),
        FieldDto(),
        FieldDto());
}
