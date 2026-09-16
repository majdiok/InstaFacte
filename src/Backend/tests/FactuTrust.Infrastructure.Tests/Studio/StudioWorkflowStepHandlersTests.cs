using System.Text.Json.Nodes;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.Studio.Automations;
using FactuTrust.Application.Features.Studio.Workflows.Engine;
using FactuTrust.Application.Features.Studio.Workflows.Spec;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Entities.Studio.Workflows;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Services.Studio.Workflows.Steps;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

/// <summary>
/// PR 4.1g — handlers d'étapes 1/2 : <c>condition</c>, <c>update_field</c>, <c>erp_action</c>.
/// La fixture (construction du <see cref="StepExecutionContext"/>) est factorisée pour être
/// réutilisée par les handlers 2/2 en 4.1h.
/// </summary>
public sealed class StudioWorkflowStepHandlersTests
{
    private static readonly Guid Tid = Guid.NewGuid();
    private static readonly Guid StartedBy = Guid.NewGuid();
    private static readonly DateTime NowUtc = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);

    // ---------- Fixture partagée (réutilisée en 4.1h) ----------

    private sealed record Fixture(
        CustomEntityDefinition Entity,
        StudioWorkflowDefinition Definition,
        StudioWorkflowInstance Instance,
        CustomRecord Record,
        StudioWorkflowContext Context);

    private static Fixture NewFixture(string dataJson, JsonObject? previous = null)
    {
        var entity = CustomEntityDefinition.Create(Tid, "clients", "Client", "Clients", null, null, null);
        var record = CustomRecord.Create(Tid, entity.Id, dataJson, StartedBy);
        var context = StudioWorkflowContext.Create(record.Id, entity.Key, StartedBy, null, previous);
        var definition = StudioWorkflowDefinition.Create(Tid, entity.Id, "wf_test", "WF test", null,
            StudioWorkflowTriggerKind.OnUpdate, "{}", "{}", true, StartedBy);
        var instance = StudioWorkflowInstance.Start(Tid, definition, record.Id,
            StudioWorkflowTriggerKind.OnUpdate, StartedBy, context.Serialize().Value, 0, null);
        return new Fixture(entity, definition, instance, record, context);
    }

    private static StepExecutionContext StepCtx(
        Fixture f, string stepKey, string stepType, JsonObject raw, IReadOnlyList<CustomFieldDefinition> fields)
    {
        var recordData = JsonNode.Parse(f.Record.DataJson) as JsonObject ?? new JsonObject();
        return new StepExecutionContext(Tid, f.Definition, f.Instance, f.Entity, fields, f.Record,
            recordData, f.Context, new WorkflowStepSpec(stepKey, stepType, null, raw), 0, false, NowUtc);
    }

    private static CustomFieldDefinition Field(
        Guid entityId, string key, CustomFieldType type = CustomFieldType.Text, bool isUnique = false) =>
        CustomFieldDefinition.Create(Tid, entityId, key, key, type, false, isUnique, 0, null, null, null, null);

    private static JsonObject StepRaw(string json) => JsonNode.Parse(json)!.AsObject();

    private sealed record UpdateFieldMocks(
        Mock<ICustomRecordRepository> Records,
        Mock<IStudioComputedFieldWriter> ComputedWriter,
        Mock<IPublisher> Publisher,
        UpdateFieldStepHandler Handler);

    private static UpdateFieldMocks NewUpdateFieldHandler()
    {
        var records = new Mock<ICustomRecordRepository>();
        var computedWriter = new Mock<IStudioComputedFieldWriter>();
        computedWriter
            .Setup(w => w.ApplyOnUpdateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<IReadOnlyList<CustomFieldDefinition>>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Guid _, IReadOnlyList<CustomFieldDefinition> _, string canonical, string _, CancellationToken _) => canonical);
        var publisher = new Mock<IPublisher>();
        publisher
            .Setup(p => p.Publish(It.IsAny<CustomRecordLifecycleNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var handler = new UpdateFieldStepHandler(
            records.Object, computedWriter.Object, publisher.Object, NullLogger<UpdateFieldStepHandler>.Instance);
        return new UpdateFieldMocks(records, computedWriter, publisher, handler);
    }

    // ---------- condition ----------

    [Fact]
    public async Task Condition_true_continues_with_passed_result()
    {
        var f = NewFixture("""{"montant":150}""");
        var fields = new[] { Field(f.Entity.Id, "montant", CustomFieldType.Decimal) };
        var raw = StepRaw("""{"filters":[{"field":"montant","op":"gte","value":100}],"match":"all"}""");

        var outcome = await new ConditionStepHandler().ExecuteAsync(StepCtx(f, "check", "condition", raw, fields), CancellationToken.None);

        var cont = Assert.IsType<StepOutcome.Continue>(outcome);
        Assert.True(cont.Result!["passed"]!.GetValue<bool>());
        Assert.Equal("all", cont.Result!["match"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("stop")]
    [InlineData("skip")]
    [InlineData("goto")]
    public async Task Condition_false_stop_skip_and_goto_follow_on_false(string onFalse)
    {
        var f = NewFixture("""{"montant":10}""");
        var fields = new[] { Field(f.Entity.Id, "montant", CustomFieldType.Decimal) };
        var raw = StepRaw($$"""{"filters":[{"field":"montant","op":"gte","value":100}],"onFalse":"{{onFalse}}","gotoKey":"later"}""");

        var outcome = await new ConditionStepHandler().ExecuteAsync(StepCtx(f, "check", "condition", raw, fields), CancellationToken.None);

        switch (onFalse)
        {
            case "stop":
                Assert.Equal("Condition non remplie", Assert.IsType<StepOutcome.Stop>(outcome).Reason);
                break;
            case "skip":
                Assert.Equal("Condition non remplie", Assert.IsType<StepOutcome.Skip>(outcome).Reason);
                break;
            default:
                Assert.Equal("later", Assert.IsType<StepOutcome.Goto>(outcome).TargetKey);
                break;
        }
    }

    [Fact]
    public async Task Condition_reads_previous_and_approval_variables()
    {
        var previous = new JsonObject { ["montant"] = 100 };
        var f = NewFixture("""{"montant":150}""", previous);
        f.Context.SetApproval("rev", "approved", null, null, null);
        var fields = new[] { Field(f.Entity.Id, "montant", CustomFieldType.Decimal) };
        var raw = StepRaw("""
            {"filters":[
                {"field":"_previous.montant","op":"eq","value":100},
                {"field":"_approval.rev.status","op":"eq","value":"approved"}]}
            """);

        var outcome = await new ConditionStepHandler().ExecuteAsync(StepCtx(f, "check", "condition", raw, fields), CancellationToken.None);

        var cont = Assert.IsType<StepOutcome.Continue>(outcome);
        Assert.True(cont.Result!["passed"]!.GetValue<bool>());
    }

    // ---------- update_field ----------

    [Fact]
    public async Task Update_field_renders_templates_merges_and_publishes_legacy_on_update()
    {
        var f = NewFixture("""{"nom":"ACME","ville":"Tunis","note":null}""");
        var fields = new[]
        {
            Field(f.Entity.Id, "nom"), Field(f.Entity.Id, "ville"), Field(f.Entity.Id, "note")
        };
        var raw = StepRaw("""{"set":{"note":"Client {{nom}} de {{ville}}"}}""");
        var mocks = NewUpdateFieldHandler();

        var outcome = await mocks.Handler.ExecuteAsync(StepCtx(f, "maj", "update_field", raw, fields), CancellationToken.None);

        var cont = Assert.IsType<StepOutcome.Continue>(outcome);
        var set = Assert.IsType<JsonArray>(cont.Result!["set"]);
        Assert.Equal("note", set[0]!.GetValue<string>());
        Assert.Empty(Assert.IsType<JsonArray>(cont.Result!["warnings"]));
        Assert.Contains("Client ACME de Tunis", f.Record.DataJson);
        mocks.Records.Verify(r => r.UpdateWithConcurrencyAsync(f.Record, It.IsAny<byte[]?>(), It.IsAny<CancellationToken>()), Times.Once);
        mocks.Publisher.Verify(p => p.Publish(
            It.Is<CustomRecordLifecycleNotification>(n =>
                n.Trigger == StudioAutomationTrigger.OnUpdate && n.RecordId == f.Record.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_field_rejects_computed_field_via_merge_patch()
    {
        var f = NewFixture("""{"nom":"ACME"}""");
        var fields = new[] { Field(f.Entity.Id, "nom"), Field(f.Entity.Id, "num", CustomFieldType.AutoNumber) };
        var raw = StepRaw("""{"set":{"num":"A-0001"}}""");
        var mocks = NewUpdateFieldHandler();

        var outcome = await mocks.Handler.ExecuteAsync(StepCtx(f, "maj", "update_field", raw, fields), CancellationToken.None);

        var fail = Assert.IsType<StepOutcome.Fail>(outcome);
        Assert.Equal("Le champ calculé « num » n'est pas modifiable.", fail.Error);
        mocks.Records.Verify(r => r.UpdateWithConcurrencyAsync(It.IsAny<CustomRecord>(), It.IsAny<byte[]?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_field_concurrency_conflict_fails_with_the_frozen_message()
    {
        var f = NewFixture("""{"nom":"ACME"}""");
        var fields = new[] { Field(f.Entity.Id, "nom") };
        var raw = StepRaw("""{"set":{"nom":"ACME 2"}}""");
        var mocks = NewUpdateFieldHandler();
        mocks.Records
            .Setup(r => r.UpdateWithConcurrencyAsync(It.IsAny<CustomRecord>(), It.IsAny<byte[]?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("boom"));

        var outcome = await mocks.Handler.ExecuteAsync(StepCtx(f, "maj", "update_field", raw, fields), CancellationToken.None);

        var fail = Assert.IsType<StepOutcome.Fail>(outcome);
        Assert.Equal("Enregistrement modifié entre-temps.", fail.Error);
        Assert.False(fail.ContinueAnyway);
        mocks.Publisher.Verify(p => p.Publish(It.IsAny<CustomRecordLifecycleNotification>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------- erp_action ----------

    private sealed record ErpMocks(Mock<IStudioBridgeExecutor> Bridge, ErpActionStepHandler Handler);

    private static ErpMocks NewErpHandler(StudioBridgeOutcome outcome)
    {
        var bridge = new Mock<IStudioBridgeExecutor>();
        bridge
            .Setup(b => b.ExecuteActionAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(outcome);
        return new ErpMocks(bridge, new ErpActionStepHandler(bridge.Object, NullLogger<ErpActionStepHandler>.Instance));
    }

    private static JsonObject ErpRaw(string mapping, string? extra = null) => StepRaw($$"""
        {"action":"generate_invoice","mapping":{{mapping}}{{extra}}}
        """);

    private const string FullMapping = """
        [{"param":"client_id","source":"field","value":"client"},
         {"param":"product_id","source":"const","value":"p-9"},
         {"param":"quantity","source":"template","value":"{{quantite}}"}]
        """;

    [Fact]
    public async Task Erp_action_maps_field_const_and_template_sources()
    {
        var f = NewFixture("""{"client":"c-42","quantite":3}""");
        var mocks = NewErpHandler(new StudioBridgeOutcome(true, null, "{}"));
        var raw = ErpRaw(FullMapping);

        var outcome = await mocks.Handler.ExecuteAsync(StepCtx(f, "gen", "erp_action", raw, Array.Empty<CustomFieldDefinition>()), CancellationToken.None);

        Assert.IsType<StepOutcome.Continue>(outcome);
        mocks.Bridge.Verify(b => b.ExecuteActionAsync(
            "generate_invoice",
            It.Is<IReadOnlyDictionary<string, object?>>(a =>
                (string?)a["client_id"] == "c-42" &&
                (string?)a["product_id"] == "p-9" &&
                (string?)a["quantity"] == "3"),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Erp_action_failure_continues_when_on_failure_is_continue()
    {
        var f = NewFixture("""{"client":"c-42","quantite":3}""");
        var mocks = NewErpHandler(new StudioBridgeOutcome(false, "Stock insuffisant.", null));
        var raw = ErpRaw(FullMapping, ",\"onFailure\":\"continue\"");

        var outcome = await mocks.Handler.ExecuteAsync(StepCtx(f, "gen", "erp_action", raw, Array.Empty<CustomFieldDefinition>()), CancellationToken.None);

        var fail = Assert.IsType<StepOutcome.Fail>(outcome);
        Assert.Equal("Stock insuffisant.", fail.Error);
        Assert.True(fail.ContinueAnyway);
    }

    [Fact]
    public async Task Erp_action_saves_result_under_save_result_as()
    {
        var f = NewFixture("""{"client":"c-42","quantite":3}""");
        var mocks = NewErpHandler(new StudioBridgeOutcome(true, null, """{"invoiceId":"abc"}"""));
        var raw = ErpRaw(FullMapping, ",\"saveResultAs\":\"facture\"");
        var ctx = StepCtx(f, "gen", "erp_action", raw, Array.Empty<CustomFieldDefinition>());

        var outcome = await mocks.Handler.ExecuteAsync(ctx, CancellationToken.None);

        Assert.IsType<StepOutcome.Continue>(outcome);
        var saved = Assert.IsType<JsonObject>(ctx.Context.Results["facture"]);
        Assert.Equal("abc", saved["invoiceId"]!.GetValue<string>());
    }

    [Fact]
    public async Task Erp_action_uses_the_workflow_correlation_id()
    {
        var f = NewFixture("""{"client":"c-42","quantite":3}""");
        var mocks = NewErpHandler(new StudioBridgeOutcome(true, null, "{}"));
        var raw = ErpRaw(FullMapping);

        await mocks.Handler.ExecuteAsync(StepCtx(f, "gen", "erp_action", raw, Array.Empty<CustomFieldDefinition>()), CancellationToken.None);

        mocks.Bridge.Verify(b => b.ExecuteActionAsync(
            "generate_invoice",
            It.IsAny<IReadOnlyDictionary<string, object?>>(),
            $"studio-workflow:{f.Instance.Id:N}:gen",
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
