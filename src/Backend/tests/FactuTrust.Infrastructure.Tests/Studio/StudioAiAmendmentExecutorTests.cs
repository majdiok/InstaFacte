using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Features.Studio.Ai;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.Entities;
using FactuTrust.Application.Features.Studio.Fields;
using FactuTrust.Application.Features.Studio.Forms;
using FactuTrust.Application.Features.Studio.Reports;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Services.Studio;
using MediatR;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

/// <summary>
/// Application d'une modification approuvée. Points sensibles couverts : le retrait d'un champ passe
/// par la commande de désactivation (données conservées), un échec partiel n'annule pas le reste, et
/// l'ajout d'options complète la liste existante au lieu de la remplacer.
/// </summary>
public sealed class StudioAiAmendmentExecutorTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private static readonly Guid EntityId = Guid.NewGuid();
    private static readonly Guid StatutFieldId = Guid.NewGuid();

    public StudioAiAmendmentExecutorTests()
    {
        _currentUser.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);
        _mediator.Setup(m => m.Send(It.IsAny<GetCustomEntitySchemaQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Schema()));
        _mediator.Setup(m => m.Send(It.IsAny<CreateCustomFieldCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Field("x", "X", CustomFieldType.Text)));
        _mediator.Setup(m => m.Send(It.IsAny<UpdateCustomFieldCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Field("statut", "Statut", CustomFieldType.Select)));
        _mediator.Setup(m => m.Send(It.IsAny<DeleteCustomFieldCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _mediator.Setup(m => m.Send(It.IsAny<UpdateCustomEntityCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Schema().Entity));
        _mediator.Setup(m => m.Send(It.IsAny<UpsertDefaultFormCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new CustomFormDto(Guid.NewGuid(), "f", "F", true, new FormLayout())));
        _mediator.Setup(m => m.Send(It.IsAny<UpsertCustomReportCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new CustomReportDto(Guid.NewGuid(), "r", "R",
                CustomReportDataSourceKind.CustomEntity, "contrats", new ReportDefinition(), true)));
    }

    [Fact]
    public async Task Adds_a_field_to_the_existing_table()
    {
        CreateCustomFieldRequest? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<CreateCustomFieldCommand>(), It.IsAny<CancellationToken>()))
            .Callback((IRequest<Result<CustomFieldDto>> c, CancellationToken _) =>
                sent = ((CreateCustomFieldCommand)c).Request)
            .ReturnsAsync(Result.Success(Field("motif_de_refus", "Motif de refus", CustomFieldType.MultilineText)));

        var (success, error, _) = await Execute("""
        { "target": { "entityKey": "contrats" }, "operations": [
          { "op": "add_field", "label": "Motif de refus", "type": "multilinetext" } ] }
        """);

        Assert.True(success, error);
        Assert.Equal("motif_de_refus", sent!.Key);
        Assert.Equal(CustomFieldType.MultilineText, sent.FieldType);
    }

    [Fact]
    public async Task Added_field_key_never_collides_with_an_existing_one()
    {
        CreateCustomFieldRequest? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<CreateCustomFieldCommand>(), It.IsAny<CancellationToken>()))
            .Callback((IRequest<Result<CustomFieldDto>> c, CancellationToken _) =>
                sent = ((CreateCustomFieldCommand)c).Request)
            .ReturnsAsync(Result.Success(Field("statut_2", "Statut", CustomFieldType.Text)));

        var (success, error, _) = await Execute("""
        { "target": { "entityKey": "contrats" }, "operations": [ { "op": "add_field", "label": "Statut", "type": "text" } ] }
        """);

        Assert.True(success, error);
        Assert.Equal("statut_2", sent!.Key);
    }

    [Fact]
    public async Task Removing_a_field_deactivates_it_and_preserves_record_data()
    {
        var (success, error, payload) = await Execute("""
        { "target": { "entityKey": "contrats" }, "operations": [ { "op": "remove_field", "key": "statut" } ] }
        """);

        Assert.True(success, error);
        // La commande de suppression Studio désactive le champ : aucune donnée d'enregistrement n'est touchée.
        _mediator.Verify(m => m.Send(It.Is<DeleteCustomFieldCommand>(c => c.Id == StatutFieldId),
            It.IsAny<CancellationToken>()), Times.Once);
        // (la sérialisation échappe les accents : on compare sur le radical)
        Assert.Contains("conserv", System.Text.Json.JsonSerializer.Serialize(payload), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Added_options_extend_the_existing_list_instead_of_replacing_it()
    {
        UpdateCustomFieldRequest? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<UpdateCustomFieldCommand>(), It.IsAny<CancellationToken>()))
            .Callback((IRequest<Result<CustomFieldDto>> c, CancellationToken _) =>
                sent = ((UpdateCustomFieldCommand)c).Request)
            .ReturnsAsync(Result.Success(Field("statut", "Statut", CustomFieldType.Select)));

        var (success, error, _) = await Execute("""
        { "target": { "entityKey": "contrats" }, "operations": [
          { "op": "update_field", "key": "statut", "addOptions": ["resilie"] } ] }
        """);

        Assert.True(success, error);
        Assert.Equal(new[] { "actif", "resilie" }, sent!.Options!.Select(o => o.Value));
    }

    [Fact]
    public async Task A_failing_operation_does_not_abort_the_remaining_ones()
    {
        _mediator.Setup(m => m.Send(It.IsAny<CreateCustomFieldCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<CustomFieldDto>(Error.Validation("field", "quota atteint")));

        var (success, error, payload) = await Execute("""
        { "target": { "entityKey": "contrats" }, "operations": [
          { "op": "add_field", "label": "Nouveau", "type": "text" },
          { "op": "remove_field", "key": "statut" } ] }
        """);

        Assert.True(success, error);
        // Aucune annulation globale : le retrait passe, l'ajout en échec est rapporté en avertissement.
        _mediator.Verify(m => m.Send(It.IsAny<DeleteCustomFieldCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains("quota atteint", System.Text.Json.JsonSerializer.Serialize(payload));
    }

    [Fact]
    public async Task Nothing_applicable_yields_a_failure_rather_than_a_false_success()
    {
        var (success, error, payload) = await Execute("""
        { "target": { "entityKey": "contrats" }, "operations": [ { "op": "remove_field", "key": "fantome" } ] }
        """);

        Assert.False(success);
        Assert.Null(payload);
        Assert.Contains("introuvable", error);
    }

    [Fact]
    public async Task Unknown_table_fails_without_touching_anything()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetCustomEntitySchemaQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<CustomEntitySchemaDto>(Error.NotFound("CustomEntity", Guid.Empty)));

        var (success, error, _) = await Execute("""
        { "target": { "entityKey": "inconnue" }, "operations": [ { "op": "add_field", "label": "X", "type": "text" } ] }
        """);

        Assert.False(success);
        Assert.Contains("introuvable", error);
        _mediator.Verify(m => m.Send(It.IsAny<CreateCustomFieldCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Progress_is_reported_step_by_step()
    {
        var steps = new List<StudioBuildStep>();
        var progress = new Mock<IStudioBuildProgress>();
        progress.Setup(p => p.Report(It.IsAny<StudioBuildStep>())).Callback((StudioBuildStep s) => steps.Add(s));

        Assert.True(StudioAiAmendmentSpec.TryParse("""
        { "target": { "entityKey": "contrats" }, "operations": [ { "op": "add_field", "label": "Note", "type": "text" } ] }
        """, out var spec, out var err), err);

        var (success, error, _) = await new StudioAiAmendmentExecutor(_mediator.Object, _currentUser.Object)
            .ExecuteAsync(spec!, progress.Object, CancellationToken.None);

        Assert.True(success, error);
        Assert.Contains(steps, s => s.Phase == "loading_schema" && s.Status == "done");
        Assert.Contains(steps, s => s.Phase == "adding_field" && s.Status == "running");
        Assert.Contains(steps, s => s.Phase == "completed");
    }

    // ---- PR 3.1b : garde anti-échec-silencieux pour les ops pas encore exécutables ----

    [Fact]
    public async Task An_operation_the_executor_does_not_know_is_skipped_with_a_warning()
    {
        var steps = new List<StudioBuildStep>();
        var progress = new Mock<IStudioBuildProgress>();
        progress.Setup(p => p.Report(It.IsAny<StudioBuildStep>())).Callback((StudioBuildStep s) => steps.Add(s));

        Assert.True(StudioAiAmendmentSpec.TryParse("""
        { "target": { "entityKey": "contrats" }, "operations": [
          { "op": "add_field", "label": "Note", "type": "text" },
          { "op": "reorder_fields", "fields": [ "note", "nom" ] } ] }
        """, out var spec, out var err), err);

        var (success, error, payload) = await new StudioAiAmendmentExecutor(_mediator.Object, _currentUser.Object)
            .ExecuteAsync(spec!, progress.Object, CancellationToken.None);

        Assert.True(success, error); // l'ajout de champ est appliqué, l'op inconnue est signalée
        Assert.Contains(steps, s => s.Phase == "skipped_operation" && s.Status == "skipped");
        var warnings = payload!.GetType().GetProperty("warnings")!.GetValue(payload) as IEnumerable<string>;
        Assert.Contains(warnings!, w => w.Contains("reorder_fields"));
    }

    [Fact]
    public async Task A_plan_with_only_not_yet_executable_operations_fails_loudly()
    {
        var (success, error, _) = await Execute("""
        { "target": { "entityKey": "contrats" }, "operations": [
          { "op": "set_automation", "trigger": "on_create", "action": "notify" } ] }
        """);

        Assert.False(success);
        Assert.Contains("set_automation", error);
        _mediator.Verify(m => m.Send(It.IsAny<CreateCustomFieldCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Every_pr31b_operation_reports_a_skipped_step_instead_of_nothing()
    {
        var steps = new List<StudioBuildStep>();
        var progress = new Mock<IStudioBuildProgress>();
        progress.Setup(p => p.Report(It.IsAny<StudioBuildStep>())).Callback((StudioBuildStep s) => steps.Add(s));

        Assert.True(StudioAiAmendmentSpec.TryParse("""
        { "target": { "entityKey": "contrats" }, "operations": [
          { "op": "reorder_fields", "fields": [ "nom" ] },
          { "op": "change_field_type", "key": "nom", "type": "multilinetext" },
          { "op": "add_relation", "kind": "many_to_one", "target": "clients" },
          { "op": "assign_system", "system": "rh" },
          { "op": "set_view", "mode": "list", "displayName": "Toutes" },
          { "op": "set_automation", "trigger": "on_create" } ] }
        """, out var spec, out var err), err);

        var (success, error, _) = await new StudioAiAmendmentExecutor(_mediator.Object, _currentUser.Object)
            .ExecuteAsync(spec!, progress.Object, CancellationToken.None);

        Assert.False(success); // rien d'appliqué, mais chaque op a produit une étape « skipped »
        var skipped = steps.Where(s => s.Status == "skipped").ToList();
        Assert.Equal(6, skipped.Count);
        Assert.All(new[] { "reorder_fields", "change_field_type", "add_relation", "assign_system", "set_view", "set_automation" },
            op => Assert.Contains(skipped, s => s.Label.Contains(op)));
        Assert.NotNull(error);
    }

    private async Task<(bool Success, string? Error, object? Payload)> Execute(string json)
    {
        Assert.True(StudioAiAmendmentSpec.TryParse(json, out var spec, out var err), err);
        return await new StudioAiAmendmentExecutor(_mediator.Object, _currentUser.Object)
            .ExecuteAsync(spec!, null, CancellationToken.None);
    }

    private static CustomEntitySchemaDto Schema() => new(
        new CustomEntityDto(EntityId, "contrats", "Contrat", "Contrats", null, null, true, 2, null,
            DateTime.UtcNow, DateTime.UtcNow),
        new[]
        {
            Field("nom", "Nom", CustomFieldType.Text),
            Field("statut", "Statut", CustomFieldType.Select,
                options: new[] { new SelectOptionDto("actif", "Actif") }, id: StatutFieldId)
        },
        new FormLayout());

    private static CustomFieldDto Field(
        string key, string label, CustomFieldType type,
        IReadOnlyList<SelectOptionDto>? options = null, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), key, label, type, false, false, 0, null, options, null, true);
}
