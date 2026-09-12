using System.Text.Json.Nodes;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.Studio.Automations;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.Records;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;
using MediatR;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

/// <summary>
/// Studio IA — PR 2.3 (R5) : PATCH partiel d'enregistrement. Fusion partielle (clés absentes
/// conservées), <c>null</c> = effacement, RowVersion absent ⇒ 400 / périmé ⇒ 409, champ calculé ⇒ 400,
/// unicité respectée, événement <c>OnUpdate</c> publié.
/// </summary>
public sealed class PatchCustomRecordCommandTests
{
    private static readonly Guid Tid = Guid.NewGuid();
    private static readonly Guid EntityId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly Mock<ICustomEntityRepository> _entities = new();
    private readonly Mock<ICustomFieldRepository> _fields = new();
    private readonly Mock<ICustomRecordRepository> _records = new();
    private readonly Mock<IStudioComputedFieldWriter> _computedWriter = new();
    private readonly Mock<IPublisher> _publisher = new();
    private readonly Mock<ICurrentUser> _currentUser = new();

    private readonly byte[] _rowVersion = { 0, 0, 0, 0, 0, 0, 0, 9 };
    private readonly CustomRecord _record;

    public PatchCustomRecordCommandTests()
    {
        _currentUser.Setup(u => u.TenantId).Returns(Tid);
        _currentUser.Setup(u => u.UserId).Returns(UserId);

        var entity = CustomEntityDefinition.Create(Tid, "chantiers", "Chantier", "Chantiers", null, null, UserId);
        typeof(CustomEntityDefinition).GetProperty(nameof(CustomEntityDefinition.Id))!.SetValue(entity, EntityId);
        _entities.Setup(e => e.GetByKeyAsync(Tid, "chantiers", It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        _fields.Setup(f => f.ListByEntityAsync(Tid, EntityId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Fields());

        _record = CustomRecord.Create(Tid, EntityId, """{"nom":"Alpha","statut":"encours","reference":"CH-0001"}""", UserId);
        typeof(CustomRecord).GetProperty(nameof(CustomRecord.RowVersion))!.SetValue(_record, _rowVersion);
        _records.Setup(r => r.GetAsync(Tid, EntityId, _record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_record);

        // Le writer réel préserve l'AutoNumber existant : le mock réinjecte la « reference » du JSON existant.
        _computedWriter.Setup(w => w.ApplyOnUpdateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<CustomFieldDefinition>>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<Guid, Guid, IReadOnlyList<CustomFieldDefinition>, string, string, CancellationToken>(
                (_, _, _, canonical, existing, _) =>
                {
                    var doc = (JsonObject)JsonNode.Parse(canonical)!;
                    var existingRef = (JsonNode.Parse(existing) as JsonObject)?["reference"];
                    if (existingRef is not null) doc["reference"] = existingRef.DeepClone();
                    return Task.FromResult(doc.ToJsonString());
                });
        _records.Setup(r => r.UpdateWithConcurrencyAsync(It.IsAny<CustomRecord>(), It.IsAny<byte[]?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private PatchCustomRecordCommandHandler Handler() =>
        new(_entities.Object, _fields.Object, _records.Object, _computedWriter.Object, _publisher.Object, _currentUser.Object);

    private string Rv() => Convert.ToBase64String(_rowVersion);

    private static CustomFieldDefinition[] Fields(bool nomUnique = false) => new[]
    {
        CustomFieldDefinition.Create(Tid, EntityId, "nom", "Nom", CustomFieldType.Text, true, nomUnique, 0, null, null, null, null),
        CustomFieldDefinition.Create(Tid, EntityId, "statut", "Statut", CustomFieldType.Select, false, false, 1, null,
            """{"options":[{"value":"encours","label":"En cours"},{"value":"termine","label":"Terminé"}]}""", null, null),
        CustomFieldDefinition.Create(Tid, EntityId, "reference", "Réf", CustomFieldType.AutoNumber, false, false, 2, null, null, null, null)
    };

    [Fact]
    public async Task Patch_merges_only_the_provided_keys_and_publishes_on_update()
    {
        var handler = Handler();
        var result = await handler.Handle(
            new PatchCustomRecordCommand("chantiers", _record.Id,
                new PatchCustomRecordRequest(new Dictionary<string, JsonNode?> { ["statut"] = JsonValue.Create("termine") }, Rv())),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var data = result.Value.Data!;
        Assert.Equal("termine", data["statut"]!.GetValue<string>());
        Assert.Equal("Alpha", data["nom"]!.GetValue<string>()); // clé absente conservée
        Assert.Equal("CH-0001", data["reference"]!.GetValue<string>()); // AutoNumber préservé
        _records.Verify(r => r.UpdateWithConcurrencyAsync(_record,
            It.Is<byte[]?>(b => b != null && b.SequenceEqual(_rowVersion)), It.IsAny<CancellationToken>()), Times.Once);
        _publisher.Verify(p => p.Publish(
            It.Is<CustomRecordLifecycleNotification>(n => n.Trigger == StudioAutomationTrigger.OnUpdate),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Patch_with_null_clears_an_optional_field()
    {
        var handler = Handler();
        var result = await handler.Handle(
            new PatchCustomRecordCommand("chantiers", _record.Id,
                new PatchCustomRecordRequest(new Dictionary<string, JsonNode?> { ["statut"] = null }, Rv())),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Data!["statut"]); // effacée
        Assert.Equal("Alpha", result.Value.Data["nom"]!.GetValue<string>());
    }

    [Fact]
    public async Task Patch_without_row_version_returns_400()
    {
        var handler = Handler();
        var result = await handler.Handle(
            new PatchCustomRecordCommand("chantiers", _record.Id,
                new PatchCustomRecordRequest(new Dictionary<string, JsonNode?> { ["statut"] = JsonValue.Create("termine") }, "")),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.rowVersion", result.Error.Code);
        _records.Verify(r => r.UpdateWithConcurrencyAsync(It.IsAny<CustomRecord>(), It.IsAny<byte[]?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Patch_with_a_stale_row_version_returns_409()
    {
        var handler = Handler();
        var stale = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 });
        var result = await handler.Handle(
            new PatchCustomRecordCommand("chantiers", _record.Id,
                new PatchCustomRecordRequest(new Dictionary<string, JsonNode?> { ["statut"] = JsonValue.Create("termine") }, stale)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
        _records.Verify(r => r.UpdateWithConcurrencyAsync(It.IsAny<CustomRecord>(), It.IsAny<byte[]?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Patch_rejects_a_computed_field_with_400()
    {
        var handler = Handler();
        var result = await handler.Handle(
            new PatchCustomRecordCommand("chantiers", _record.Id,
                new PatchCustomRecordRequest(new Dictionary<string, JsonNode?> { ["reference"] = JsonValue.Create("XX-9") }, Rv())),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.data", result.Error.Code);
        Assert.Contains("calculé", result.Error.Description);
    }

    [Fact]
    public async Task Patch_enforces_field_uniqueness()
    {
        // nom devient unique pour ce test (mêmes 3 champs que le record, pour que la fusion reste valide)
        _fields.Setup(f => f.ListByEntityAsync(Tid, EntityId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Fields(nomUnique: true));
        _records.Setup(r => r.ExistsWithFieldValueAsync(Tid, EntityId, "nom", "Doublon", _record.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = Handler();
        var result = await handler.Handle(
            new PatchCustomRecordCommand("chantiers", _record.Id,
                new PatchCustomRecordRequest(new Dictionary<string, JsonNode?> { ["nom"] = JsonValue.Create("Doublon") }, Rv())),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
        _records.Verify(r => r.UpdateWithConcurrencyAsync(It.IsAny<CustomRecord>(), It.IsAny<byte[]?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Patch_maps_a_concurrency_race_on_save_to_409()
    {
        _records.Setup(r => r.UpdateWithConcurrencyAsync(It.IsAny<CustomRecord>(), It.IsAny<byte[]?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException("stale"));

        var handler = Handler();
        var result = await handler.Handle(
            new PatchCustomRecordCommand("chantiers", _record.Id,
                new PatchCustomRecordRequest(new Dictionary<string, JsonNode?> { ["statut"] = JsonValue.Create("termine") }, Rv())),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
    }
}
