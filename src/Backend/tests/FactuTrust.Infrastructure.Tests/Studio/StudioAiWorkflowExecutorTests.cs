using System.Text.Json;
using System.Text.Json.Nodes;
using FactuTrust.Application.Features.Studio.Ai;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.Fields;
using FactuTrust.Application.Features.Studio.Workflows;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Services.Studio;
using MediatR;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

/// <summary>
/// PR 4.3f1 — <see cref="StudioAiWorkflowExecutor"/> : création TOUT-OU-RIEN des workflows d'un plan
/// confirmé, uniquement via MediatR (<c>GetCustomEntitySchemaQuery</c>, <c>ListWorkflowsQuery</c>,
/// <c>CreateWorkflowCommand</c>, <c>DeleteWorkflowCommand</c>) : création INACTIVE dans l'ordre, clé
/// suffixée <c>_2</c> … <c>_9</c> si prise, rollback D-08 avec <see cref="CancellationToken.None"/>,
/// payload du contrat §D.
/// </summary>
public sealed class StudioAiWorkflowExecutorTests
{
    private static readonly Guid FacturesId = Guid.NewGuid();
    private static readonly Guid DevisId = Guid.NewGuid();

    private readonly Mock<IMediator> _mediator = new();
    private readonly List<CreateWorkflowCommand> _creates = new();

    /// <summary>Deux workflows sur « factures » (1 étape, puis 2 étapes).</summary>
    private const string TwoOnFactures = """
    { "workflows": [
        { "entityKey": "factures", "name": "Relance", "trigger": "manual",
          "steps": [ { "type": "notify", "to": { "kind": "startedBy" }, "title": "Relance" } ] },
        { "entityKey": "factures", "name": "Validation", "trigger": "on_create",
          "steps": [
            { "type": "condition", "filters": [ { "field": "montant", "op": "gt", "value": 1000 } ], "onFalse": "stop" },
            { "type": "notify", "to": { "kind": "startedBy" }, "title": "À valider" } ] } ] }
    """;

    /// <summary>Un seul workflow « Relance » sur « factures ».</summary>
    private const string OneOnFactures = """
    { "workflows": [
        { "entityKey": "factures", "name": "Relance", "trigger": "manual",
          "steps": [ { "type": "notify", "to": { "kind": "startedBy" }, "title": "Relance" } ] } ] }
    """;

    /// <summary>Deux tables : « factures » puis « devis ».</summary>
    private const string FacturesThenDevis = """
    { "workflows": [
        { "entityKey": "factures", "name": "Relance", "trigger": "manual",
          "steps": [ { "type": "notify", "to": { "kind": "startedBy" }, "title": "Relance" } ] },
        { "entityKey": "devis", "name": "Approbation devis", "trigger": "on_create",
          "steps": [ { "type": "notify", "to": { "kind": "startedBy" }, "title": "Devis" } ] } ] }
    """;

    public StudioAiWorkflowExecutorTests()
    {
        SetupSchema("factures", FacturesId);
        SetupSchema("devis", DevisId);
        SetupExistingKeys(FacturesId);
        SetupExistingKeys(DevisId);
        SetupCreateSucceeds();
        SetupDeleteSucceeds();
    }

    [Fact]
    public async Task Creates_each_workflow_inactive_in_order_and_reports_progress()
    {
        var steps = new List<StudioBuildStep>();

        var (success, error, _) = await Execute(TwoOnFactures, steps);

        Assert.True(success, error);
        Assert.Equal(2, _creates.Count);
        Assert.Equal("relance", _creates[0].Request.Key);
        Assert.Equal("validation", _creates[1].Request.Key);
        Assert.All(_creates, c =>
        {
            Assert.Equal(FacturesId, c.EntityId);
            Assert.False(c.Request.IsActive);
            Assert.Null(c.Request.RowVersion);
        });

        var creating = steps.Where(s => s.Phase == "creating_workflows").ToList();
        Assert.Equal(4, creating.Count);
        Assert.Equal(new[] { "running", "done", "running", "done" }, creating.Select(s => s.Status));
        Assert.Contains("Relance", creating[0].Label);
        Assert.Equal("factures", creating[0].EntityRef);
        Assert.Contains("inactif", creating[1].Detail);
        Assert.Contains("Validation", creating[2].Label);
        Assert.Equal("completed", steps[^1].Phase);
        Assert.Equal("done", steps[^1].Status);
        Assert.Contains("2 workflow(s)", steps[^1].Label);
        Assert.DoesNotContain(steps, s => s.Phase == "failed");
    }

    [Fact]
    public async Task Reads_schema_and_existing_keys_once_per_entity()
    {
        var (success, error, _) = await Execute(TwoOnFactures);

        Assert.True(success, error);
        _mediator.Verify(m => m.Send(It.IsAny<GetCustomEntitySchemaQuery>(), It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(It.IsAny<ListWorkflowsQuery>(), It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(It.IsAny<CreateWorkflowCommand>(), It.IsAny<CancellationToken>()), Times.Exactly(2));

        // Deux tables ⇒ une lecture par table, jamais plus.
        _creates.Clear();
        _mediator.Invocations.Clear();
        var (success2, error2, _) = await Execute(FacturesThenDevis);

        Assert.True(success2, error2);
        _mediator.Verify(m => m.Send(It.IsAny<GetCustomEntitySchemaQuery>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _mediator.Verify(m => m.Send(It.Is<GetCustomEntitySchemaQuery>(q => q.EntityKey == "devis"), It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(It.IsAny<ListWorkflowsQuery>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _mediator.Verify(m => m.Send(It.Is<ListWorkflowsQuery>(q => q.EntityId == DevisId), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(new[] { FacturesId, DevisId }, _creates.Select(c => c.EntityId));
    }

    [Fact]
    public async Task Suffixes_key_when_taken_then_fails_after_nine_attempts()
    {
        SetupExistingKeys(FacturesId, "relance");

        var (success, error, _) = await Execute(OneOnFactures);

        Assert.True(success, error);
        var create = Assert.Single(_creates);
        Assert.Equal("relance_2", create.Request.Key);
        Assert.Equal("Relance", create.Request.Name);

        // relance, relance_2 … relance_9 toutes prises ⇒ échec explicite, aucune création.
        _creates.Clear();
        _mediator.Invocations.Clear();
        SetupExistingKeys(FacturesId, new[] { "relance" }.Concat(Enumerable.Range(2, 8).Select(n => $"relance_{n}")).ToArray());
        var steps = new List<StudioBuildStep>();

        var (success2, error2, payload) = await Execute(OneOnFactures, steps);

        Assert.False(success2);
        Assert.Null(payload);
        Assert.Equal("Workflow « Relance » : clé « relance » indisponible (9 variantes essayées).", error2);
        Assert.Empty(_creates);
        _mediator.Verify(m => m.Send(It.IsAny<CreateWorkflowCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Contains(steps, s => s.Phase == "failed" && s.Status == "error" && s.Detail == error2);
    }

    [Fact]
    public void FreeKey_truncates_the_base_so_the_suffixed_key_stays_within_64_characters()
    {
        var baseKey = new string('a', 64);
        var taken = new HashSet<string>(StringComparer.Ordinal) { baseKey };

        var candidate = StudioAiWorkflowExecutor.FreeKey(baseKey, taken);

        Assert.NotNull(candidate);
        Assert.Equal(64, candidate!.Length);
        Assert.EndsWith("_2", candidate);
        Assert.StartsWith(new string('a', 62), candidate);

        // Clé libre ⇒ rendue telle quelle ; tout pris ⇒ null (9 variantes : base + _2 … _9).
        Assert.Equal("relance", StudioAiWorkflowExecutor.FreeKey("relance", new HashSet<string>()));
        var allTaken = new HashSet<string>(StringComparer.Ordinal) { "relance" };
        for (var n = 2; n <= StudioAiWorkflowExecutor.MaxKeyAttempts; n++) allTaken.Add($"relance_{n}");
        Assert.Null(StudioAiWorkflowExecutor.FreeKey("relance", allTaken));
        allTaken.Remove("relance_9");
        Assert.Equal("relance_9", StudioAiWorkflowExecutor.FreeKey("relance", allTaken));
    }

    [Fact]
    public async Task Fails_when_entity_is_missing_or_inactive_without_creating_anything()
    {
        // Table introuvable.
        _mediator.Setup(m => m.Send(It.Is<GetCustomEntitySchemaQuery>(q => q.EntityKey == "factures"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<CustomEntitySchemaDto>(Error.NotFound("Table introuvable")));
        var steps = new List<StudioBuildStep>();

        var (success, error, payload) = await Execute(OneOnFactures, steps);

        Assert.False(success);
        Assert.Null(payload);
        Assert.Equal("Table « factures » introuvable ou inactive.", error);
        Assert.Contains(steps, s => s.Phase == "failed" && s.Status == "error" && s.Detail == error);
        _mediator.Verify(m => m.Send(It.IsAny<CreateWorkflowCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        _mediator.Verify(m => m.Send(It.IsAny<DeleteWorkflowCommand>(), It.IsAny<CancellationToken>()), Times.Never);

        // Table inactive ⇒ même refus.
        SetupSchema("factures", FacturesId, isActive: false);
        steps.Clear();

        var (success2, error2, _) = await Execute(OneOnFactures, steps);

        Assert.False(success2);
        Assert.Equal("Table « factures » introuvable ou inactive.", error2);
        Assert.Contains(steps, s => s.Phase == "failed" && s.Status == "error");
        _mediator.Verify(m => m.Send(It.IsAny<CreateWorkflowCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        _mediator.Verify(m => m.Send(It.IsAny<ListWorkflowsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Failure_on_second_workflow_rolls_back_the_first_via_DeleteWorkflowCommand()
    {
        var idA = Guid.NewGuid();
        var deletes = new List<(DeleteWorkflowCommand Command, CancellationToken Token)>();
        _mediator.Setup(m => m.Send(It.IsAny<DeleteWorkflowCommand>(), It.IsAny<CancellationToken>()))
            .Callback((IRequest<Result<WorkflowDeletionResultDto>> c, CancellationToken t) => deletes.Add(((DeleteWorkflowCommand)c, t)))
            .ReturnsAsync(Result.Success(new WorkflowDeletionResultDto(0)));
        _mediator.Setup(m => m.Send(It.Is<CreateWorkflowCommand>(c => c.Request.Key == "relance"), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateWorkflowCommand c, CancellationToken _) => Result.Success(Dto(c, idA)));
        _mediator.Setup(m => m.Send(It.Is<CreateWorkflowCommand>(c => c.Request.Key == "validation"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<WorkflowDefinitionDto>(Error.Validation("Plan", "Quota de workflows atteint")));
        using var cts = new CancellationTokenSource();
        var steps = new List<StudioBuildStep>();

        var (success, error, payload) = await Execute(TwoOnFactures, steps, cts.Token);

        Assert.False(success);
        Assert.Null(payload);
        Assert.Equal("Workflow « Validation » : Quota de workflows atteint", error);

        // Rollback D-08 : le workflow A est supprimé avec CancellationToken.None (pas le jeton de la requête).
        var (deleteCommand, token) = Assert.Single(deletes);
        Assert.Equal(idA, deleteCommand.Id);
        Assert.Equal(CancellationToken.None, token);
        _mediator.Verify(m => m.Send(It.Is<DeleteWorkflowCommand>(d => d.Id == idA),
            It.Is<CancellationToken>(t => t == CancellationToken.None)), Times.Once);
        Assert.Contains(steps, s => s.Phase == "creating_workflows" && s.Status == "error" && s.Detail == "Quota de workflows atteint");
        Assert.Contains(steps, s => s.Phase == "failed" && s.Status == "error" && s.Detail == error);
        Assert.DoesNotContain(steps, s => s.Phase == "completed");

        // Un delete raté n'est jamais silencieux : étape « failed » + message persisté enrichi.
        deletes.Clear();
        steps.Clear();
        _mediator.Setup(m => m.Send(It.IsAny<DeleteWorkflowCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<WorkflowDeletionResultDto>(Error.Forbidden("Accès refusé")));

        var (success2, error2, _) = await Execute(TwoOnFactures, steps, cts.Token);

        Assert.False(success2);
        Assert.StartsWith("Workflow « Validation » : Quota de workflows atteint. Annulation incomplète", error2);
        Assert.Contains("« Relance » (clé « relance ») : Accès refusé", error2);
        Assert.Contains(steps, s => s.Phase == "failed" && s.Status == "error"
            && s.Label == "Annulation de « Relance » impossible" && s.EntityRef == "factures" && s.Detail == "Accès refusé");
    }

    [Fact]
    public async Task Payload_lists_created_workflows_with_step_count_open_url_and_inactive_message()
    {
        var id = Guid.NewGuid();
        _mediator.Setup(m => m.Send(It.IsAny<CreateWorkflowCommand>(), It.IsAny<CancellationToken>()))
            .Callback((IRequest<Result<WorkflowDefinitionDto>> c, CancellationToken _) => _creates.Add((CreateWorkflowCommand)c))
            .ReturnsAsync((CreateWorkflowCommand c, CancellationToken _) => Result.Success(Dto(c, id)));

        var (success, error, payload) = await Execute(OneOnFactures);

        Assert.True(success, error);
        Assert.NotNull(payload);
        var json = JsonSerializer.Serialize(payload);
        Assert.Contains("\"success\":true", json);
        Assert.Contains($"\"workflows\":[{{\"id\":\"{id}\",\"key\":\"relance\",\"entityKey\":\"factures\",\"name\":\"Relance\",\"stepCount\":1}}]", json);
        Assert.Contains("\"openUrl\":\"/studio/workflows\"", json);
        Assert.Contains("\"warnings\":[]", json);

        var node = JsonNode.Parse(json)!.AsObject();
        Assert.Equal("1 workflow créé — inactif : activez-le depuis le hub après relecture.", node["message"]!.GetValue<string>());
        var only = Assert.Single(node["workflows"]!.AsArray());
        Assert.Equal(id, only!["id"]!.GetValue<Guid>());
        Assert.Equal(1, only["stepCount"]!.GetValue<int>());

        // Pluriel pour plusieurs workflows.
        var (success2, error2, payload2) = await Execute(TwoOnFactures);

        Assert.True(success2, error2);
        var node2 = JsonNode.Parse(JsonSerializer.Serialize(payload2))!.AsObject();
        Assert.Equal(2, node2["workflows"]!.AsArray().Count);
        Assert.Equal(2, node2["workflows"]![1]!["stepCount"]!.GetValue<int>());
        Assert.Equal("2 workflows créés — inactifs : activez-les depuis le hub après relecture.", node2["message"]!.GetValue<string>());
    }

    // ---------------------------------------------------------------- helpers

    private async Task<(bool Success, string? Error, object? Payload)> Execute(
        string specJson, List<StudioBuildStep>? steps = null, CancellationToken ct = default)
    {
        Assert.True(StudioAiWorkflowSpec.TryParse(specJson, out var spec, out var err), err);
        return await new StudioAiWorkflowExecutor(_mediator.Object).ExecuteAsync(spec!, CaptureSteps(steps), ct);
    }

    /// <summary>Progression factice alimentant <paramref name="steps"/> (null ⇒ pas de progression).</summary>
    private static IStudioBuildProgress? CaptureSteps(List<StudioBuildStep>? steps)
    {
        if (steps is null) return null;
        var progress = new Mock<IStudioBuildProgress>();
        progress.Setup(p => p.Report(It.IsAny<StudioBuildStep>())).Callback((StudioBuildStep s) => steps.Add(s));
        return progress.Object;
    }

    /// <summary>Schéma réel de la table (motif <c>SetupInterventionsSchema</c>).</summary>
    private void SetupSchema(string entityKey, Guid entityId, bool isActive = true)
    {
        var fields = new List<CustomFieldDto>
        {
            new(Guid.NewGuid(), "montant", "Montant", CustomFieldType.Number, false, false, 1, null, null, null, true),
            new(Guid.NewGuid(), "statut", "Statut", CustomFieldType.Select, false, false, 2, null,
                new List<SelectOptionDto> { new("brouillon", "Brouillon"), new("validee", "Validée") }, null, true)
        };
        _mediator.Setup(m => m.Send(It.Is<GetCustomEntitySchemaQuery>(q => q.EntityKey == entityKey), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new CustomEntitySchemaDto(
                new CustomEntityDto(entityId, entityKey, entityKey, entityKey, null, null, isActive, 2, null, DateTime.UtcNow, DateTime.UtcNow),
                fields, new FormLayout())));
    }

    /// <summary>Clés de workflows déjà prises sur la table (<c>ListWorkflowsQuery</c>).</summary>
    private void SetupExistingKeys(Guid entityId, params string[] keys)
    {
        IReadOnlyList<WorkflowDefinitionDto> existing = keys.Select(k => new WorkflowDefinitionDto(
            Guid.NewGuid(), entityId, k, k, null, "manual", new JsonObject(), new JsonObject(), 1, 1, true, 0,
            DateTime.UtcNow, DateTime.UtcNow, "AAAA")).ToList();
        _mediator.Setup(m => m.Send(It.Is<ListWorkflowsQuery>(q => q.EntityId == entityId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(existing));
    }

    private void SetupCreateSucceeds()
    {
        _mediator.Setup(m => m.Send(It.IsAny<CreateWorkflowCommand>(), It.IsAny<CancellationToken>()))
            .Callback((IRequest<Result<WorkflowDefinitionDto>> c, CancellationToken _) => _creates.Add((CreateWorkflowCommand)c))
            .ReturnsAsync((CreateWorkflowCommand c, CancellationToken _) => Result.Success(Dto(c, Guid.NewGuid())));
    }

    private void SetupDeleteSucceeds()
    {
        _mediator.Setup(m => m.Send(It.IsAny<DeleteWorkflowCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new WorkflowDeletionResultDto(0)));
    }

    /// <summary>DTO renvoyé par <c>CreateWorkflowCommand</c> : reflète la demande (clé, nom, nombre d'étapes).</summary>
    private static WorkflowDefinitionDto Dto(CreateWorkflowCommand c, Guid id)
    {
        var r = c.Request;
        var stepCount = r.Steps["steps"] is JsonArray a ? a.Count : 0;
        return new WorkflowDefinitionDto(id, c.EntityId, r.Key, r.Name, r.Description, r.Trigger,
            r.TriggerConfig ?? new JsonObject(), r.Steps, stepCount, 1, r.IsActive, 0, DateTime.UtcNow, DateTime.UtcNow, "AAAA");
    }
}
