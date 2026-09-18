using System.Text.Json.Nodes;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.Studio.Workflows;
using FactuTrust.Application.Features.Studio.Workflows.Engine;
using FactuTrust.Application.Features.Studio.Workflows.Spec;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Entities.Studio.Workflows;
using FactuTrust.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

/// <summary>
/// Studio IA — 4.1j1 / 4.1j2 : handlers de conception des workflows (dépôts mockés, aucun SQL).
/// Create (version 1, audit, clé dupliquée ⇒ 409, déclencheur planifié ⇒ 400, quota ⇒ Validation.Plan,
/// table inconnue ⇒ 404, première erreur d'étape avec son chemin), Update (jeton obligatoire / périmé,
/// clé immuable, Version incrémentée), Toggle idempotent, Delete (soft + annulation des instances ouvertes),
/// Duplicate (copie inactive « _copie » … « _copie_9 »), Validate à blanc (200 même invalide), catalogue
/// des étapes, instances (clamp 1..200, clé du workflow, « previous » masqué).
/// </summary>
public sealed class StudioWorkflowFeaturesTests
{
    private static readonly Guid Tid = Guid.NewGuid();
    private static readonly Guid Uid = Guid.NewGuid();

    private static readonly CustomEntityDefinition Entity =
        CustomEntityDefinition.Create(Tid, "commandes", "Commande", "Commandes", null, null, null);

    private static readonly IReadOnlyList<CustomFieldDefinition> Fields = new[]
    {
        Field(Entity.Id, "statut", CustomFieldType.Select, 0),
        Field(Entity.Id, "montant", CustomFieldType.Decimal, 1),
        Field(Entity.Id, "client", CustomFieldType.RelationExisting, 2),
        Field(Entity.Id, "total", CustomFieldType.Formula, 3),
        Field(Entity.Id, "ref", CustomFieldType.AutoNumber, 4)
    };

    private static readonly byte[] RowVersion1 = { 0, 0, 0, 0, 0, 0, 0, 1 };
    private static readonly byte[] RowVersion2 = { 0, 0, 0, 0, 0, 0, 0, 2 };

    private readonly Mock<IStudioWorkflowRepository> _workflows = new(MockBehavior.Strict);
    private readonly Mock<ICustomEntityRepository> _entities = new();
    private readonly Mock<ICustomFieldRepository> _fields = new();
    private readonly Mock<IStudioQuotaService> _quota = new();
    private readonly Mock<IAuditService> _audit = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IStudioWorkflowEngine> _engine = new(MockBehavior.Strict);

    public StudioWorkflowFeaturesTests()
    {
        _currentUser.Setup(u => u.TenantId).Returns(Tid);
        _currentUser.Setup(u => u.UserId).Returns(Uid);

        _entities.Setup(e => e.GetByIdAsync(Tid, Entity.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Entity);
        _fields.Setup(f => f.ListByEntityAsync(Tid, Entity.Id, false, It.IsAny<CancellationToken>())).ReturnsAsync(Fields);

        _quota.Setup(q => q.EnsureUnderLimitAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _audit.Setup(a => a.LogAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    // ---- Fixtures ----

    private static CustomFieldDefinition Field(Guid entityId, string key, CustomFieldType type, int sort)
        => CustomFieldDefinition.Create(Tid, entityId, key, key, type, false, false, sort, null, null, null, null);

    private static JsonObject Steps(params string[] steps)
        => (JsonObject)JsonNode.Parse($$"""{ "version": 1, "steps": [{{string.Join(",", steps)}}] }""")!;

    private const string ConditionStep =
        """{ "key": "verif", "type": "condition", "filters": [ { "field": "statut", "op": "eq", "value": "valide" } ], "onFalse": "skip" }""";

    private const string UpdateStep =
        """{ "key": "maj", "type": "update_field", "set": { "montant": 12.5 } }""";

    private static JsonObject ValidSteps() => Steps(ConditionStep, UpdateStep);

    private static SaveWorkflowRequest Request(
        string key = "relance",
        string trigger = "on_update",
        string? rowVersion = null,
        bool isActive = true,
        JsonObject? steps = null,
        string name = "Relance")
        => new(key, name, "  Relance des commandes  ", trigger, null, steps ?? ValidSteps(), isActive, rowVersion);

    private static StudioWorkflowDefinition Definition(string key = "relance", bool isActive = true, byte[]? rowVersion = null)
    {
        var def = StudioWorkflowDefinition.Create(
            Tid, Entity.Id, key, "Relance", null, StudioWorkflowTriggerKind.OnUpdate, "{}", ValidSteps().ToJsonString(), isActive, Uid);
        typeof(StudioWorkflowDefinition).GetProperty(nameof(StudioWorkflowDefinition.RowVersion))!.SetValue(def, rowVersion ?? RowVersion1);
        return def;
    }

    private void SetupDefinition(StudioWorkflowDefinition def, int openInstances = 0)
    {
        _workflows.Setup(w => w.GetDefinitionAsync(Tid, def.Id, It.IsAny<CancellationToken>())).ReturnsAsync(def);
        _workflows.Setup(w => w.CountOpenInstancesForDefinitionAsync(Tid, def.Id, It.IsAny<CancellationToken>())).ReturnsAsync(openInstances);
    }

    private void SetupCreateRepository(StudioWorkflowDefinition? existingByKey = null, int count = 0)
    {
        _workflows.Setup(w => w.GetDefinitionByKeyAsync(Tid, Entity.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingByKey);
        _workflows.Setup(w => w.CountByEntityAsync(Tid, Entity.Id, It.IsAny<CancellationToken>())).ReturnsAsync(count);
        _workflows.Setup(w => w.AddDefinitionAsync(It.IsAny<StudioWorkflowDefinition>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private CreateWorkflowCommandHandler CreateHandler() =>
        new(_workflows.Object, _entities.Object, _fields.Object, _quota.Object, _audit.Object, _currentUser.Object);

    private UpdateWorkflowCommandHandler UpdateHandler() =>
        new(_workflows.Object, _entities.Object, _fields.Object, _audit.Object, _currentUser.Object);

    private ToggleWorkflowCommandHandler ToggleHandler() =>
        new(_workflows.Object, _audit.Object, _currentUser.Object);

    private DeleteWorkflowCommandHandler DeleteHandler() =>
        new(_workflows.Object, _engine.Object, _audit.Object, _currentUser.Object, NullLogger<DeleteWorkflowCommandHandler>.Instance);

    private DuplicateWorkflowCommandHandler DuplicateHandler() =>
        new(_workflows.Object, _quota.Object, _audit.Object, _currentUser.Object);

    private ValidateWorkflowQueryHandler ValidateHandler() =>
        new(_entities.Object, _fields.Object, _currentUser.Object);

    private ListWorkflowInstancesQueryHandler ListInstancesHandler() =>
        new(_workflows.Object, _currentUser.Object);

    private GetWorkflowInstanceQueryHandler GetInstanceHandler() =>
        new(_workflows.Object, _currentUser.Object);

    private ListTenantWorkflowsQueryHandler ListTenantHandler() =>
        new(_workflows.Object, _currentUser.Object);

    private void SetupDesignPermission(bool granted = true) =>
        _currentUser.Setup(u => u.HasPermission(Permissions.Studio.DesignEntities)).Returns(granted);

    private void VerifyAudit(string action, Times times) =>
        _audit.Verify(a => a.LogAsync(action, "StudioWorkflowDefinition", It.IsAny<Guid?>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()), times);

    // ---- Create ----

    [Fact]
    public async Task Create_persists_a_version_one_workflow_and_audits()
    {
        StudioWorkflowDefinition? added = null;
        SetupCreateRepository();
        _workflows.Setup(w => w.AddDefinitionAsync(It.IsAny<StudioWorkflowDefinition>(), It.IsAny<CancellationToken>()))
            .Callback<StudioWorkflowDefinition, CancellationToken>((d, _) => added = d)
            .Returns(Task.CompletedTask);

        var result = await CreateHandler().Handle(new CreateWorkflowCommand(Entity.Id, Request(isActive: false)), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.NotNull(added);
        Assert.Equal("relance", added!.Key);
        Assert.Equal("Relance", added.Name);
        Assert.Equal("Relance des commandes", added.Description);
        Assert.Equal(StudioWorkflowTriggerKind.OnUpdate, added.Trigger);
        Assert.Equal(ValidSteps().ToJsonString(), added.StepsJson);
        Assert.False(added.IsActive);
        Assert.Equal(Tid, added.TenantId);
        Assert.Equal(Uid, added.CreatedBy);

        var dto = result.Value;
        Assert.Equal(added.Id, dto.Id);
        Assert.Equal(Entity.Id, dto.EntityDefinitionId);
        Assert.Equal(1, dto.Version);
        Assert.Equal(2, dto.StepCount);
        Assert.Equal(0, dto.OpenInstances);
        Assert.Equal("on_update", dto.Trigger);
        Assert.Equal("maj", dto.Steps["steps"]![1]!["key"]!.GetValue<string>());
        Assert.Empty(dto.TriggerConfig);
        VerifyAudit("Studio.Workflow.Created", Times.Once());
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_key_with_conflict()
    {
        SetupCreateRepository(existingByKey: Definition());

        var result = await CreateHandler().Handle(new CreateWorkflowCommand(Entity.Id, Request()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
        Assert.Equal("Un workflow avec la clé « relance » existe déjà pour cette table.", result.Error.Description);
        _workflows.Verify(w => w.AddDefinitionAsync(It.IsAny<StudioWorkflowDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyAudit("Studio.Workflow.Created", Times.Never());
    }

    [Fact]
    public async Task Create_rejects_the_scheduled_trigger_as_a_validation_error()
    {
        SetupCreateRepository();

        var result = await CreateHandler().Handle(new CreateWorkflowCommand(Entity.Id, Request(trigger: "scheduled")), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.trigger", result.Error.Code);
        Assert.Equal("Déclencheur planifié : bientôt disponible.", result.Error.Description);
        _workflows.Verify(w => w.AddDefinitionAsync(It.IsAny<StudioWorkflowDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_stops_at_the_plan_quota()
    {
        SetupCreateRepository(count: 20);
        _quota.Setup(q => q.EnsureUnderLimitAsync(
                Tid, "MaxWorkflowsPerEntity", 20, 20, "workflows par table", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.Validation("Plan", "Quota de workflows par table atteint (20).")));

        var result = await CreateHandler().Handle(new CreateWorkflowCommand(Entity.Id, Request()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Plan", result.Error.Code);
        _workflows.Verify(w => w.AddDefinitionAsync(It.IsAny<StudioWorkflowDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyAudit("Studio.Workflow.Created", Times.Never());
    }

    [Fact]
    public async Task Create_returns_not_found_for_an_unknown_or_foreign_entity()
    {
        var foreign = Guid.NewGuid();
        _entities.Setup(e => e.GetByIdAsync(Tid, foreign, It.IsAny<CancellationToken>())).ReturnsAsync((CustomEntityDefinition?)null);

        var result = await CreateHandler().Handle(new CreateWorkflowCommand(foreign, Request()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("CustomEntity.NotFound", result.Error.Code);
        _workflows.VerifyNoOtherCalls();
    }

    public static IEnumerable<object[]> InvalidStepDocuments()
    {
        // Type inconnu : échec de forme (Parse) ⇒ chemin steps[0].type.
        yield return new object[]
        {
            Steps("""{ "key": "aa", "type": "teleport" }"""),
            "Validation.steps[0].type",
            false
        };
        // Champ inconnu dans update_field ⇒ steps[1].set ; une seule erreur.
        yield return new object[]
        {
            Steps(ConditionStep, """{ "key": "maj", "type": "update_field", "set": { "fantome": 1 } }"""),
            "Validation.steps[1].set",
            false
        };
        // 31 étapes ⇒ borne de forme sur « steps ».
        yield return new object[]
        {
            Steps(Enumerable.Range(0, 31).Select(i => $$"""{ "key": "s{{i}}", "type": "wait", "hours": 1 }""").ToArray()),
            "Validation.steps",
            false
        };
        // Deux erreurs de contenu ⇒ première avec son chemin + suffixe « (+1 autre(s) … ) ».
        yield return new object[]
        {
            Steps(
                """{ "key": "aa", "type": "update_field", "set": { "fantome": 1 } }""",
                """{ "key": "bb", "type": "update_field", "set": { "inconnu": 2 } }"""),
            "Validation.steps[0].set",
            true
        };
    }

    [Theory]
    [MemberData(nameof(InvalidStepDocuments))]
    public async Task Create_reports_the_first_step_error_with_its_path(JsonObject steps, string expectedCode, bool expectsSuffix)
    {
        SetupCreateRepository();

        var result = await CreateHandler().Handle(new CreateWorkflowCommand(Entity.Id, Request(steps: steps)), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error.Code);
        Assert.Equal(expectsSuffix, result.Error.Description.Contains("autre(s) erreur(s) — utilisez la validation pour la liste complète.", StringComparison.Ordinal));
        _workflows.Verify(w => w.AddDefinitionAsync(It.IsAny<StudioWorkflowDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- Update ----

    [Theory]
    [InlineData(null, "Le jeton de concurrence (rowVersion) est obligatoire pour mettre à jour un workflow.")]
    [InlineData("", "Le jeton de concurrence (rowVersion) est obligatoire pour mettre à jour un workflow.")]
    [InlineData("pas-du-base64", "Le jeton de concurrence (rowVersion) est mal formé.")]
    public async Task Update_requires_a_well_formed_row_version(string? rowVersion, string expectedMessage)
    {
        var def = Definition();
        SetupDefinition(def);

        var result = await UpdateHandler().Handle(new UpdateWorkflowCommand(def.Id, Request(rowVersion: rowVersion)), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.rowVersion", result.Error.Code);
        Assert.Equal(expectedMessage, result.Error.Description);
        _workflows.Verify(w => w.UpdateDefinitionWithConcurrencyAsync(It.IsAny<StudioWorkflowDefinition>(), It.IsAny<byte[]?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_with_a_stale_row_version_is_a_conflict()
    {
        const string expectedMessage = "Le workflow a été modifié entre-temps. Rechargez-le avant de réessayer.";

        // 1. Pré-contrôle déterministe : jeton différent de la version courante.
        var def = Definition(rowVersion: RowVersion2);
        SetupDefinition(def);

        var stale = await UpdateHandler().Handle(
            new UpdateWorkflowCommand(def.Id, Request(rowVersion: Convert.ToBase64String(RowVersion1))), CancellationToken.None);

        Assert.True(stale.IsFailure);
        Assert.Equal("Conflict", stale.Error.Code);
        Assert.Equal(expectedMessage, stale.Error.Description);
        _workflows.Verify(w => w.UpdateDefinitionWithConcurrencyAsync(It.IsAny<StudioWorkflowDefinition>(), It.IsAny<byte[]?>(), It.IsAny<CancellationToken>()), Times.Never);

        // 2. Course perdue à l'écriture : EF lève DbUpdateConcurrencyException ⇒ même 409.
        _workflows.Setup(w => w.UpdateDefinitionWithConcurrencyAsync(def, It.Is<byte[]>(b => b.SequenceEqual(RowVersion2)), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("race"));

        var raced = await UpdateHandler().Handle(
            new UpdateWorkflowCommand(def.Id, Request(rowVersion: Convert.ToBase64String(RowVersion2))), CancellationToken.None);

        Assert.True(raced.IsFailure);
        Assert.Equal("Conflict", raced.Error.Code);
        Assert.Equal(expectedMessage, raced.Error.Description);
        VerifyAudit("Studio.Workflow.Updated", Times.Never());
    }

    [Fact]
    public async Task Update_keeps_the_key_immutable_and_bumps_the_version_when_steps_change()
    {
        var def = Definition();
        SetupDefinition(def, openInstances: 3);
        var token = Convert.ToBase64String(RowVersion1);

        // Clé différente ⇒ Validation.key, aucune écriture.
        var renamed = await UpdateHandler().Handle(new UpdateWorkflowCommand(def.Id, Request(key: "autre", rowVersion: token)), CancellationToken.None);
        Assert.True(renamed.IsFailure);
        Assert.Equal("Validation.key", renamed.Error.Code);
        Assert.Equal("La clé d'un workflow ne peut pas être modifiée.", renamed.Error.Description);
        _workflows.Verify(w => w.UpdateDefinitionWithConcurrencyAsync(It.IsAny<StudioWorkflowDefinition>(), It.IsAny<byte[]?>(), It.IsAny<CancellationToken>()), Times.Never);

        // Étapes différentes + désactivation ⇒ Version 2, IsActive appliqué, audit Updated.
        _workflows.Setup(w => w.UpdateDefinitionWithConcurrencyAsync(def, It.Is<byte[]>(b => b.SequenceEqual(RowVersion1)), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var newSteps = Steps(UpdateStep);

        var updated = await UpdateHandler().Handle(
            new UpdateWorkflowCommand(def.Id, Request(rowVersion: token, isActive: false, steps: newSteps, name: "Relance v2")), CancellationToken.None);

        Assert.True(updated.IsSuccess, updated.Error.Description);
        Assert.Equal(2, updated.Value.Version);
        Assert.Equal(1, updated.Value.StepCount);
        Assert.False(updated.Value.IsActive);
        Assert.Equal("Relance v2", updated.Value.Name);
        Assert.Equal("relance", updated.Value.Key);
        Assert.Equal(3, updated.Value.OpenInstances);
        Assert.Equal(newSteps.ToJsonString(), def.StepsJson);
        _workflows.Verify(w => w.UpdateDefinitionWithConcurrencyAsync(def, It.Is<byte[]>(b => b.SequenceEqual(RowVersion1)), It.IsAny<CancellationToken>()), Times.Once);
        VerifyAudit("Studio.Workflow.Updated", Times.Once());
    }

    // ---- Toggle / Delete ----

    [Fact]
    public async Task Toggle_and_delete_map_a_lost_write_race_to_a_conflict()
    {
        const string expectedMessage = "Le workflow a été modifié entre-temps. Rechargez-le avant de réessayer.";
        var def = Definition(isActive: true);
        SetupDefinition(def, openInstances: 1);
        _workflows.Setup(w => w.UpdateDefinitionWithConcurrencyAsync(def, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("race"));
        _workflows.Setup(w => w.ListOpenInstancesForDefinitionAsync(Tid, def.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { StudioWorkflowInstance.Start(Tid, def, Guid.NewGuid(), StudioWorkflowTriggerKind.Manual, Uid, "{}", 0, null) });

        var toggled = await ToggleHandler().Handle(new ToggleWorkflowCommand(def.Id, IsActive: false), CancellationToken.None);
        Assert.True(toggled.IsFailure);
        Assert.Equal("Conflict", toggled.Error.Code);
        Assert.Equal(expectedMessage, toggled.Error.Description);
        VerifyAudit("Studio.Workflow.Toggled", Times.Never());

        var deleted = await DeleteHandler().Handle(new DeleteWorkflowCommand(def.Id), CancellationToken.None);
        Assert.True(deleted.IsFailure);
        Assert.Equal("Conflict", deleted.Error.Code);
        Assert.Equal(expectedMessage, deleted.Error.Description);
        // Aucune instance n'est annulée et aucun audit n'est écrit quand la suppression n'est pas persistée.
        _engine.Verify(e => e.CancelAsync(It.IsAny<StudioWorkflowInstance>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyAudit("Studio.Workflow.Deleted", Times.Never());
    }

    [Fact]
    public async Task Toggle_and_delete_update_state_and_cancel_open_instances()
    {
        var def = Definition(isActive: true);
        SetupDefinition(def, openInstances: 2);
        _workflows.Setup(w => w.UpdateDefinitionWithConcurrencyAsync(def, null, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Toggle : désactivation persistée sans jeton, audit Toggled.
        var toggled = await ToggleHandler().Handle(new ToggleWorkflowCommand(def.Id, IsActive: false), CancellationToken.None);
        Assert.True(toggled.IsSuccess, toggled.Error.Description);
        Assert.False(toggled.Value.IsActive);
        Assert.False(def.IsActive);
        Assert.Equal(1, def.Version);
        Assert.Equal(2, toggled.Value.OpenInstances);
        _workflows.Verify(w => w.UpdateDefinitionWithConcurrencyAsync(def, null, It.IsAny<CancellationToken>()), Times.Once);
        VerifyAudit("Studio.Workflow.Toggled", Times.Once());

        // 2ᵉ appel identique ⇒ idempotent : aucune écriture ni audit supplémentaire.
        var again = await ToggleHandler().Handle(new ToggleWorkflowCommand(def.Id, IsActive: false), CancellationToken.None);
        Assert.True(again.IsSuccess);
        Assert.False(again.Value.IsActive);
        _workflows.Verify(w => w.UpdateDefinitionWithConcurrencyAsync(def, null, It.IsAny<CancellationToken>()), Times.Once);
        VerifyAudit("Studio.Workflow.Toggled", Times.Once());

        // Delete : soft delete + annulation des 2 instances ouvertes (l'une échoue : comptée à part, on continue).
        var open1 = StudioWorkflowInstance.Start(Tid, def, Guid.NewGuid(), StudioWorkflowTriggerKind.OnUpdate, Uid, "{}", 0, null);
        var open2 = StudioWorkflowInstance.Start(Tid, def, Guid.NewGuid(), StudioWorkflowTriggerKind.Manual, Uid, "{}", 0, null);
        _workflows.Setup(w => w.ListOpenInstancesForDefinitionAsync(Tid, def.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { open1, open2 });
        _engine.Setup(e => e.CancelAsync(It.IsAny<StudioWorkflowInstance>(), "Workflow supprimé", Uid, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var deleted = await DeleteHandler().Handle(new DeleteWorkflowCommand(def.Id), CancellationToken.None);

        Assert.True(deleted.IsSuccess, deleted.Error.Description);
        Assert.Equal(2, deleted.Value.CancelledInstances);
        Assert.True(def.IsDeleted);
        Assert.False(def.IsActive);
        Assert.NotNull(def.DeletedAt);
        _engine.Verify(e => e.CancelAsync(open1, "Workflow supprimé", Uid, It.IsAny<CancellationToken>()), Times.Once);
        _engine.Verify(e => e.CancelAsync(open2, "Workflow supprimé", Uid, It.IsAny<CancellationToken>()), Times.Once);
        _workflows.Verify(w => w.UpdateDefinitionWithConcurrencyAsync(def, null, It.IsAny<CancellationToken>()), Times.Exactly(2));
        VerifyAudit("Studio.Workflow.Deleted", Times.Once());

        // Une suppression dont l'annulation échoue ne fait pas échouer la commande.
        var def2 = Definition(key: "relance2");
        SetupDefinition(def2);
        _workflows.Setup(w => w.UpdateDefinitionWithConcurrencyAsync(def2, null, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var open3 = StudioWorkflowInstance.Start(Tid, def2, Guid.NewGuid(), StudioWorkflowTriggerKind.OnCreate, Uid, "{}", 0, null);
        _workflows.Setup(w => w.ListOpenInstancesForDefinitionAsync(Tid, def2.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { open3 });
        _engine.Setup(e => e.CancelAsync(open3, "Workflow supprimé", Uid, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var partial = await DeleteHandler().Handle(new DeleteWorkflowCommand(def2.Id), CancellationToken.None);

        Assert.True(partial.IsSuccess, partial.Error.Description);
        Assert.Equal(0, partial.Value.CancelledInstances);
        Assert.True(def2.IsDeleted);

        // Définition inconnue (ou déjà supprimée : filtre IsDeleted) ⇒ 404.
        var unknown = Guid.NewGuid();
        _workflows.Setup(w => w.GetDefinitionAsync(Tid, unknown, It.IsAny<CancellationToken>())).ReturnsAsync((StudioWorkflowDefinition?)null);
        var missing = await DeleteHandler().Handle(new DeleteWorkflowCommand(unknown), CancellationToken.None);
        Assert.True(missing.IsFailure);
        Assert.Equal("StudioWorkflowDefinition.NotFound", missing.Error.Code);
    }

    // ---- Duplicate (4.1j2) ----

    [Fact]
    public async Task Duplicate_creates_an_inactive_copy_with_the_copie_suffix()
    {
        var source = Definition(key: "relance", isActive: true);
        SetupDefinition(source);
        StudioWorkflowDefinition? added = null;
        _workflows.Setup(w => w.CountByEntityAsync(Tid, Entity.Id, It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _workflows.Setup(w => w.GetDefinitionByKeyAsync(Tid, Entity.Id, "relance_copie", It.IsAny<CancellationToken>()))
            .ReturnsAsync((StudioWorkflowDefinition?)null);
        _workflows.Setup(w => w.AddDefinitionAsync(It.IsAny<StudioWorkflowDefinition>(), It.IsAny<CancellationToken>()))
            .Callback<StudioWorkflowDefinition, CancellationToken>((d, _) => added = d)
            .Returns(Task.CompletedTask);

        var result = await DuplicateHandler().Handle(new DuplicateWorkflowCommand(source.Id), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.NotNull(added);
        Assert.Equal("relance_copie", result.Value.Key);
        Assert.Equal("Relance (copie)", result.Value.Name);
        Assert.False(result.Value.IsActive);
        Assert.Equal(1, result.Value.Version);
        Assert.Equal(0, result.Value.OpenInstances);
        Assert.Equal(source.StepsJson, added!.StepsJson);
        Assert.Equal(source.TriggerConfigJson, added.TriggerConfigJson);
        Assert.Equal(source.Trigger, added.Trigger);
        Assert.NotEqual(source.Id, added.Id);
        VerifyAudit("Studio.Workflow.Duplicated", Times.Once());

        // Définition inconnue ⇒ 404.
        var unknown = Guid.NewGuid();
        _workflows.Setup(w => w.GetDefinitionAsync(Tid, unknown, It.IsAny<CancellationToken>())).ReturnsAsync((StudioWorkflowDefinition?)null);
        var missing = await DuplicateHandler().Handle(new DuplicateWorkflowCommand(unknown), CancellationToken.None);
        Assert.Equal("StudioWorkflowDefinition.NotFound", missing.Error.Code);
    }

    [Fact]
    public async Task Duplicate_falls_back_to_numbered_suffixes_and_fails_after_nine()
    {
        var source = Definition(key: "relance");
        SetupDefinition(source);
        _workflows.Setup(w => w.CountByEntityAsync(Tid, Entity.Id, It.IsAny<CancellationToken>())).ReturnsAsync(3);
        var taken = new HashSet<string>(StringComparer.Ordinal) { "relance_copie", "relance_copie_2" };
        _workflows.Setup(w => w.GetDefinitionByKeyAsync(Tid, Entity.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Guid _, string key, CancellationToken _) => taken.Contains(key) ? Definition(key: key) : null);
        _workflows.Setup(w => w.AddDefinitionAsync(It.IsAny<StudioWorkflowDefinition>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var third = await DuplicateHandler().Handle(new DuplicateWorkflowCommand(source.Id), CancellationToken.None);
        Assert.True(third.IsSuccess, third.Error.Description);
        Assert.Equal("relance_copie_3", third.Value.Key);

        // Les 9 clés candidates sont prises ⇒ 409 figé, aucune écriture supplémentaire.
        taken.Add("relance_copie_3");
        foreach (var n in Enumerable.Range(4, 6)) taken.Add($"relance_copie_{n}");
        var exhausted = await DuplicateHandler().Handle(new DuplicateWorkflowCommand(source.Id), CancellationToken.None);
        Assert.True(exhausted.IsFailure);
        Assert.Equal("Conflict", exhausted.Error.Code);
        Assert.Equal("Impossible de dupliquer : les clés « relance_copie » à « relance_copie_9 » sont déjà utilisées.", exhausted.Error.Description);
        _workflows.Verify(w => w.AddDefinitionAsync(It.IsAny<StudioWorkflowDefinition>(), It.IsAny<CancellationToken>()), Times.Once);

        // Quota atteint ⇒ Validation.Plan avant toute recherche de clé.
        _quota.Setup(q => q.EnsureUnderLimitAsync(Tid, "MaxWorkflowsPerEntity", 3, 20, "workflows par table", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.Validation("Plan", "Quota atteint.")));
        var quota = await DuplicateHandler().Handle(new DuplicateWorkflowCommand(source.Id), CancellationToken.None);
        Assert.Equal("Validation.Plan", quota.Error.Code);

        // Clé longue (62 caractères) ⇒ candidat tronqué à 64 au plus, sans « _ » final avant le suffixe.
        var longKey = "a" + new string('b', 60) + "_";
        Assert.Equal(62, longKey.Length);
        var suffixed = DuplicateWorkflowCommandHandler.Suffix(longKey, "_copie");
        Assert.True(suffixed.Length <= 64);
        Assert.EndsWith("_copie", suffixed);
        Assert.DoesNotContain("__", suffixed);
        Assert.Equal("relance_copie_2", DuplicateWorkflowCommandHandler.Suffix("relance", "_copie_2"));
    }

    // ---- Validate à blanc (4.1j2) ----

    [Fact]
    public async Task Validate_returns_success_with_issues_for_an_invalid_definition()
    {
        // Déclencheur planifié + étape invalide ⇒ 200, IsValid false, deux erreurs localisées, StepCount renseigné.
        var invalidSteps = Steps("""{ "key": "aa", "type": "update_field", "set": { "fantome": 1 } }""", UpdateStep);
        var result = await ValidateHandler().Handle(
            new ValidateWorkflowQuery(Entity.Id, Request(trigger: "scheduled", steps: invalidSteps)), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.False(result.Value.IsValid);
        Assert.Contains(result.Value.Errors, e => e.Path == "trigger" && e.Message == "Déclencheur planifié : bientôt disponible.");
        Assert.Contains(result.Value.Errors, e => e.Path == "steps[0].set");
        Assert.Equal(2, result.Value.StepCount);
        Assert.Empty(result.Value.Warnings);

        // Déclencheur inconnu ⇒ issue « trigger » seule, StepCount 0, RowVersion ignoré.
        var unknownTrigger = await ValidateHandler().Handle(
            new ValidateWorkflowQuery(Entity.Id, Request(trigger: "on_full_moon", rowVersion: "ignored")), CancellationToken.None);
        Assert.True(unknownTrigger.IsSuccess);
        Assert.False(unknownTrigger.Value.IsValid);
        var only = Assert.Single(unknownTrigger.Value.Errors);
        Assert.Equal("trigger", only.Path);
        Assert.Equal("Déclencheur inconnu : « on_full_moon ». Valeurs acceptées : on_create, on_update, field_changed, manual.", only.Message);
        Assert.Equal(0, unknownTrigger.Value.StepCount);

        // Définition valide ⇒ IsValid true (avertissements de lint possibles), StepCount 2 ; unicité de clé non vérifiée.
        var valid = await ValidateHandler().Handle(new ValidateWorkflowQuery(Entity.Id, Request()), CancellationToken.None);
        Assert.True(valid.IsSuccess);
        Assert.True(valid.Value.IsValid);
        Assert.Empty(valid.Value.Errors);
        Assert.Equal(2, valid.Value.StepCount);
        _workflows.VerifyNoOtherCalls();

        // Table inconnue ⇒ 404 (seul échec possible).
        var foreign = Guid.NewGuid();
        _entities.Setup(e => e.GetByIdAsync(Tid, foreign, It.IsAny<CancellationToken>())).ReturnsAsync((CustomEntityDefinition?)null);
        var missing = await ValidateHandler().Handle(new ValidateWorkflowQuery(foreign, Request()), CancellationToken.None);
        Assert.Equal("CustomEntity.NotFound", missing.Error.Code);
    }

    // ---- Catalogue (4.1j2) ----

    [Fact]
    public async Task Step_catalog_mirrors_the_seven_step_types()
    {
        var result = await new GetWorkflowStepCatalogQueryHandler().Handle(new GetWorkflowStepCatalogQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value.Entries.Count);
        Assert.Equal(StudioWorkflowStepTypes.All, result.Value.Entries.Select(e => e.Type).ToList());
        var erpAction = Assert.Single(result.Value.Entries, e => e.Type == "erp_action");
        var action = Assert.Single(erpAction.Properties, p => p.Name == "action");
        Assert.True(action.Required);
        Assert.All(result.Value.Entries, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.Label));
            Assert.False(string.IsNullOrWhiteSpace(e.Description));
        });
    }

    // ---- Catalogue tenant (4.5c2 / D-44-20) ----

    [Fact]
    public async Task List_tenant_workflows_returns_paged_items_with_entity_context_and_open_counts()
    {
        SetupDesignPermission();
        var d1 = Definition("wf_a");
        var d2 = Definition("wf_b");
        _workflows.Setup(w => w.CountByTenantAsync(Tid, null, It.IsAny<CancellationToken>())).ReturnsAsync(7);
        _workflows.Setup(w => w.ListByTenantAsync(Tid, null, 10, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new StudioWorkflowCatalogRow(d1, "commandes", "Commande"), new StudioWorkflowCatalogRow(d2, "commandes", "Commande") });
        _workflows.Setup(w => w.CountOpenInstancesForDefinitionsAsync(
                Tid, It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2 && ids.Contains(d1.Id) && ids.Contains(d2.Id)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [d1.Id] = 3 });

        var result = await ListTenantHandler().Handle(new ListTenantWorkflowsQuery(null, Page: 3, PageSize: 5), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var page = result.Value;
        Assert.Equal(7, page.TotalCount);
        Assert.Equal(3, page.Page);
        Assert.Equal(5, page.PageSize);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(d1.Id, page.Items[0].Workflow.Id);
        Assert.Equal("wf_a", page.Items[0].Workflow.Key);
        Assert.Equal("commandes", page.Items[0].EntityKey);
        Assert.Equal("Commande", page.Items[0].EntityDisplayName);
        Assert.Equal(3, page.Items[0].Workflow.OpenInstances);
        Assert.Equal(0, page.Items[1].Workflow.OpenInstances);
        Assert.True(page.HasPreviousPage);
        Assert.False(page.HasNextPage);
        _workflows.Verify(w => w.CountOpenInstancesForDefinitionsAsync(Tid, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task List_tenant_workflows_clamps_page_and_page_size_trims_search_and_skips_listing_when_total_is_zero()
    {
        SetupDesignPermission();
        _workflows.Setup(w => w.CountByTenantAsync(Tid, "relance", It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _workflows.Setup(w => w.CountOpenInstancesForDefinitionsAsync(
                Tid, It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 0), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int>());

        var result = await ListTenantHandler().Handle(new ListTenantWorkflowsQuery("  relance  ", Page: 0, PageSize: 999), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var page = result.Value;
        Assert.Equal(1, page.Page);
        Assert.Equal(ListTenantWorkflowsQueryHandler.MaxPageSize, page.PageSize);
        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.False(page.HasNextPage);
        _workflows.Verify(w => w.ListByTenantAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task List_tenant_workflows_truncates_overlong_search_before_querying()
    {
        SetupDesignPermission();
        var longSearch = new string('a', ListTenantWorkflowsQueryHandler.MaxSearchLength + 40);
        var expected = longSearch[..ListTenantWorkflowsQueryHandler.MaxSearchLength];
        _workflows.Setup(w => w.CountByTenantAsync(Tid, expected, It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _workflows.Setup(w => w.CountOpenInstancesForDefinitionsAsync(Tid, It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 0), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int>());

        var result = await ListTenantHandler().Handle(new ListTenantWorkflowsQuery(longSearch), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Page);
        Assert.Equal(50, result.Value.PageSize);
        _workflows.Verify(w => w.CountByTenantAsync(Tid, expected, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task List_tenant_workflows_requires_design_permission_and_calls_no_repository()
    {
        SetupDesignPermission(false);

        var result = await ListTenantHandler().Handle(new ListTenantWorkflowsQuery("x"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Unauthorized", result.Error.Code);
        _workflows.VerifyNoOtherCalls();
    }

    // ---- Instances (4.1j2) ----

    [Fact]
    public async Task List_instances_clamps_max_and_carries_the_workflow_key()
    {
        var def = Definition();
        SetupDefinition(def);
        var instance = StudioWorkflowInstance.Start(Tid, def, Guid.NewGuid(), StudioWorkflowTriggerKind.FieldChanged, Uid, "{}", 0, null);
        var requestedMax = new List<int>();
        _workflows.Setup(w => w.ListInstancesForDefinitionAsync(Tid, def.Id, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, int, CancellationToken>((_, _, max, _) => requestedMax.Add(max))
            .ReturnsAsync(new[] { instance });

        var big = await ListInstancesHandler().Handle(new ListWorkflowInstancesQuery(def.Id, Max: 500), CancellationToken.None);
        var zero = await ListInstancesHandler().Handle(new ListWorkflowInstancesQuery(def.Id, Max: 0), CancellationToken.None);
        var dflt = await ListInstancesHandler().Handle(new ListWorkflowInstancesQuery(def.Id), CancellationToken.None);

        Assert.Equal(new[] { 200, 1, 50 }, requestedMax);
        Assert.True(big.IsSuccess && zero.IsSuccess && dflt.IsSuccess);
        var dto = Assert.Single(big.Value);
        Assert.Equal(instance.Id, dto.Id);
        Assert.Equal(def.Key, dto.WorkflowKey);
        Assert.Equal(def.Name, dto.WorkflowName);
        Assert.Equal("field_changed", dto.Trigger);
        Assert.Equal("running", dto.Status);
        Assert.Equal(def.Version, dto.DefinitionVersion);
        Assert.Equal(Uid, dto.StartedBy);

        var unknown = Guid.NewGuid();
        _workflows.Setup(w => w.GetDefinitionAsync(Tid, unknown, It.IsAny<CancellationToken>())).ReturnsAsync((StudioWorkflowDefinition?)null);
        var missing = await ListInstancesHandler().Handle(new ListWorkflowInstancesQuery(unknown), CancellationToken.None);
        Assert.Equal("StudioWorkflowDefinition.NotFound", missing.Error.Code);
    }

    [Fact]
    public async Task Get_instance_masks_previous_and_returns_steps_and_approvals()
    {
        var def = Definition();
        SetupDefinition(def);
        const string contextJson = """{ "record": { "statut": "valide" }, "previous": { "statut": "brouillon", "secret": "x" }, "vars": { "a": 1 } }""";
        var instance = StudioWorkflowInstance.Start(Tid, def, Guid.NewGuid(), StudioWorkflowTriggerKind.OnUpdate, Uid, contextJson, 1, Guid.NewGuid());
        _workflows.Setup(w => w.GetInstanceAsync(Tid, instance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(instance);

        var now = DateTime.UtcNow;
        var runB = StudioWorkflowStepRun.Record(Tid, instance.Id, 1, "maj", "update_field", StudioWorkflowStepRunStatus.Succeeded,
            StudioWorkflowStepOutcome.Continue, null, """{ "updated": ["montant"] }""", null, now.AddSeconds(1), now.AddSeconds(2), Uid);
        var runA = StudioWorkflowStepRun.Record(Tid, instance.Id, 0, "verif", "condition", StudioWorkflowStepRunStatus.Succeeded,
            StudioWorkflowStepOutcome.Continue, null, "pas-un-objet", null, now, now.AddSeconds(1), Uid);
        _workflows.Setup(w => w.ListStepRunsAsync(Tid, instance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { runB, runA });

        var approved = StudioWorkflowApproval.Create(Tid, instance.Id, "validation", null, "Administrators", "Valider ?", null, null);
        approved.Decide(StudioWorkflowApprovalStatus.Approved, Uid, "OK", now);
        var pending = StudioWorkflowApproval.Create(Tid, instance.Id, "validation2", Uid, null, "Encore ?", "Merci", now.AddDays(1));
        _workflows.Setup(w => w.ListApprovalsForInstanceAsync(Tid, instance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { approved, pending });

        var result = await GetInstanceHandler().Handle(new GetWorkflowInstanceQuery(instance.Id), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Description);
        var detail = result.Value;
        Assert.Equal(instance.Id, detail.Instance.Id);
        Assert.Equal("relance", detail.Instance.WorkflowKey);
        Assert.Equal(1, detail.Instance.Depth);
        Assert.NotNull(detail.Instance.OriginInstanceId);

        // « previous » : clé conservée, valeur masquée ; le reste du contexte est intact.
        Assert.True(detail.Context.ContainsKey("previous"));
        Assert.Null(detail.Context["previous"]);
        Assert.Equal("valide", detail.Context["record"]!["statut"]!.GetValue<string>());
        Assert.Equal(1, detail.Context["vars"]!["a"]!.GetValue<int>());
        Assert.DoesNotContain("brouillon", detail.Context.ToJsonString(), StringComparison.Ordinal);

        // Étapes triées par index ; Result reparsé (objet) ou null (JSON non objet).
        Assert.Equal(new[] { 0, 1 }, detail.Steps.Select(s => s.StepIndex).ToArray());
        Assert.Null(detail.Steps[0].Result);
        Assert.Equal("montant", detail.Steps[1].Result!["updated"]![0]!.GetValue<string>());
        Assert.Equal("succeeded", detail.Steps[1].Status);
        Assert.Equal("continue", detail.Steps[1].Outcome);

        // Approbations : toutes les statuts (méthode additive), snake_case.
        Assert.Equal(2, detail.Approvals.Count);
        Assert.Contains(detail.Approvals, a => a.Status == "approved" && a.DecidedBy == Uid && a.Comment == "OK");
        Assert.Contains(detail.Approvals, a => a.Status == "pending" && a.AssigneeUserId == Uid && a.DueAt is not null);
        _workflows.Verify(w => w.ListPendingApprovalsForInstanceAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

        // Instance inconnue ⇒ 404 ; définition supprimée ⇒ WorkflowKey null.
        var unknown = Guid.NewGuid();
        _workflows.Setup(w => w.GetInstanceAsync(Tid, unknown, It.IsAny<CancellationToken>())).ReturnsAsync((StudioWorkflowInstance?)null);
        var missing = await GetInstanceHandler().Handle(new GetWorkflowInstanceQuery(unknown), CancellationToken.None);
        Assert.Equal("StudioWorkflowInstance.NotFound", missing.Error.Code);

        _workflows.Setup(w => w.GetDefinitionAsync(Tid, def.Id, It.IsAny<CancellationToken>())).ReturnsAsync((StudioWorkflowDefinition?)null);
        var orphan = await GetInstanceHandler().Handle(new GetWorkflowInstanceQuery(instance.Id), CancellationToken.None);
        Assert.True(orphan.IsSuccess);
        Assert.Null(orphan.Value.Instance.WorkflowKey);
        Assert.Null(orphan.Value.Instance.WorkflowName);
    }
}
