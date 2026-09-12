using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.Entities;
using FactuTrust.Application.Features.Studio.Fields;
using FactuTrust.Application.Features.Studio.Relations;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;
using MediatR;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

/// <summary>
/// Studio IA — PR 2.1 : la commande N‑N crée UNE table de jonction (<c>Kind = Junction</c>) et DEUX champs
/// <c>RelationCustom</c> requis, exclusivement via les commandes existantes (chemin unique IA / manuel),
/// refuse les cibles invalides (source == cible, jonction, autre tenant), compense la jonction si un champ
/// échoue et journalise <c>Studio.Relation.ManyToManyCreated</c>.
/// </summary>
public sealed class CreateManyToManyRelationCommandTests
{
    private static readonly Guid Tid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Uid = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<ICustomEntityRepository> _entities = new();
    private readonly Mock<ICustomFieldRepository> _fields = new();
    private readonly Mock<IStudioQuotaService> _quota = new();
    private readonly Mock<IAuditService> _audit = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IJsonIndexManager> _jsonIndex = new();

    private readonly List<object> _sent = new();
    private readonly CustomEntityDefinition _employes;
    private readonly CustomEntityDefinition _projets;

    public CreateManyToManyRelationCommandTests()
    {
        _currentUser.SetupGet(u => u.TenantId).Returns(Tid);
        _currentUser.SetupGet(u => u.UserId).Returns(Uid);

        _employes = CustomEntityDefinition.Create(Tid, "employes", "Employé", "Employés", "users", null, Uid);
        _projets = CustomEntityDefinition.Create(Tid, "projets", "Projet", "Projets", "briefcase", null, Uid);
        RegisterEntity(_employes);
        RegisterEntity(_projets);

        _entities.Setup(r => r.KeyExistsAsync(Tid, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _entities.Setup(r => r.CountAsync(Tid, It.IsAny<CancellationToken>())).ReturnsAsync(2);
        _quota.Setup(q => q.EnsureUnderLimitAsync(Tid, StudioQuotas.MaxEntitiesKey, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _jsonIndex.Setup(j => j.EnsureFieldIndexAsync(Tid, It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _audit.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Default happy-path mediator: every sub-command succeeds and echoes its request.
        _mediator.Setup(m => m.Send(It.IsAny<CreateCustomEntityCommand>(), It.IsAny<CancellationToken>()))
            .Returns((CreateCustomEntityCommand c, CancellationToken _) =>
            {
                _sent.Add(c);
                return Task.FromResult(Result.Success(EntityDto(c.Request)));
            });
        _mediator.Setup(m => m.Send(It.IsAny<CreateCustomFieldCommand>(), It.IsAny<CancellationToken>()))
            .Returns((CreateCustomFieldCommand c, CancellationToken _) =>
            {
                _sent.Add(c);
                return Task.FromResult(Result.Success(FieldDto(c.Request, _sent.Count)));
            });
        _mediator.Setup(m => m.Send(It.IsAny<DeleteCustomEntityCommand>(), It.IsAny<CancellationToken>()))
            .Returns((DeleteCustomEntityCommand c, CancellationToken _) =>
            {
                _sent.Add(c);
                return Task.FromResult(Result.Success());
            });
    }

    [Fact]
    public async Task Nominal_creates_one_junction_entity_and_two_required_relation_fields()
    {
        var result = await Handler().Handle(
            new CreateManyToManyRelationCommand(_employes.Id, new CreateManyToManyRelationRequest(_projets.Id, null, null, null)),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Description);

        var entityCommands = _sent.OfType<CreateCustomEntityCommand>().ToList();
        var fieldCommands = _sent.OfType<CreateCustomFieldCommand>().ToList();
        Assert.Single(entityCommands);
        Assert.Equal(2, fieldCommands.Count);
        Assert.Empty(_sent.OfType<DeleteCustomEntityCommand>());

        var junctionRequest = entityCommands[0].Request;
        Assert.Equal(CustomEntityKind.Junction, junctionRequest.Kind);
        Assert.Equal("employes_projets", junctionRequest.Key);
        Assert.Equal(_employes.SystemId, junctionRequest.SystemId);
        Assert.Equal("Employé – Projet", junctionRequest.DisplayName);

        Assert.All(fieldCommands, c =>
        {
            Assert.True(c.Request.IsRequired);
            Assert.False(c.Request.IsUnique);
            Assert.Equal(CustomFieldType.RelationCustom, c.Request.FieldType);
            Assert.Equal("custom", c.Request.Relation?.Kind);
        });
        Assert.Equal("employes", fieldCommands[0].Request.Key);
        Assert.Equal("employes", fieldCommands[0].Request.Relation!.Ref);
        Assert.Equal("projets", fieldCommands[1].Request.Key);
        Assert.Equal("projets", fieldCommands[1].Request.Relation!.Ref);
        // Both fields hang off the freshly created junction (never off the source/target).
        Assert.All(fieldCommands, c => Assert.Equal(result.Value.Junction.Id, c.EntityId));

        // Sequencing: entity first, then source field, then target field.
        Assert.Collection(_sent,
            c => Assert.IsType<CreateCustomEntityCommand>(c),
            c => Assert.IsType<CreateCustomFieldCommand>(c),
            c => Assert.IsType<CreateCustomFieldCommand>(c));

        // Result DTO: junction with FieldCount = 2 + both fields, in (source, target) order.
        Assert.Equal(2, result.Value.Junction.FieldCount);
        Assert.Equal(CustomEntityKind.Junction, result.Value.Junction.Kind);
        Assert.Equal("employes", result.Value.SourceField.Key);
        Assert.Equal("projets", result.Value.TargetField.Key);

        // R10: one non-unique jx_ index per link field (best effort).
        _jsonIndex.Verify(j => j.EnsureFieldIndexAsync(Tid, "employes", It.IsAny<CancellationToken>()), Times.Once);
        _jsonIndex.Verify(j => j.EnsureFieldIndexAsync(Tid, "projets", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Nominal_writes_the_many_to_many_audit_entry()
    {
        var result = await Handler().Handle(
            new CreateManyToManyRelationCommand(_employes.Id, new CreateManyToManyRelationRequest(_projets.Id, "Affectations", null, null)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Studio.Relation.ManyToManyCreated", CreateManyToManyRelationCommandHandler.AuditAction);
        _audit.Verify(a => a.LogAsync(
                CreateManyToManyRelationCommandHandler.AuditAction,
                "CustomEntity",
                result.Value.Junction.Id,
                null,
                It.IsAny<object?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Explicit_junction_key_and_display_name_are_honoured()
    {
        var result = await Handler().Handle(
            new CreateManyToManyRelationCommand(_employes.Id,
                new CreateManyToManyRelationRequest(_projets.Id, null, "Affectations", "Affectation")),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Description);
        var junctionRequest = _sent.OfType<CreateCustomEntityCommand>().Single().Request;
        Assert.Equal("affectations", junctionRequest.Key); // lower-cased, validated shape
        Assert.Equal("Affectation", junctionRequest.DisplayName);
    }

    [Fact]
    public async Task Default_key_falls_back_to_a_numeric_suffix_when_taken_and_explicit_key_conflicts()
    {
        _entities.Setup(r => r.KeyExistsAsync(Tid, "employes_projets", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var fallback = await Handler().Handle(
            new CreateManyToManyRelationCommand(_employes.Id, new CreateManyToManyRelationRequest(_projets.Id, null, null, null)),
            CancellationToken.None);
        Assert.True(fallback.IsSuccess, fallback.Error.Description);
        Assert.Equal("employes_projets_2", _sent.OfType<CreateCustomEntityCommand>().Single().Request.Key);

        _sent.Clear();
        var explicitConflict = await Handler().Handle(
            new CreateManyToManyRelationCommand(_employes.Id, new CreateManyToManyRelationRequest(_projets.Id, null, "employes_projets", null)),
            CancellationToken.None);
        Assert.True(explicitConflict.IsFailure);
        Assert.Equal("Conflict", explicitConflict.Error.Code);
        Assert.Empty(_sent); // nothing written
    }

    [Fact]
    public async Task Source_equal_to_target_is_rejected_before_any_write()
    {
        var result = await Handler().Handle(
            new CreateManyToManyRelationCommand(_employes.Id, new CreateManyToManyRelationRequest(_employes.Id, null, null, null)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.target", result.Error.Code);
        Assert.Empty(_sent);
    }

    [Fact]
    public async Task Junction_target_is_rejected()
    {
        var junction = CustomEntityDefinition.Create(Tid, "employes_projets", "Employé – Projet", "Employé – Projet", "link", null, Uid, null, CustomEntityKind.Junction);
        RegisterEntity(junction);

        var result = await Handler().Handle(
            new CreateManyToManyRelationCommand(_employes.Id, new CreateManyToManyRelationRequest(junction.Id, null, null, null)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.target", result.Error.Code);
        Assert.Empty(_sent);
    }

    [Fact]
    public async Task Junction_source_is_rejected()
    {
        var junction = CustomEntityDefinition.Create(Tid, "employes_projets", "Employé – Projet", "Employé – Projet", "link", null, Uid, null, CustomEntityKind.Junction);
        RegisterEntity(junction);

        var result = await Handler().Handle(
            new CreateManyToManyRelationCommand(junction.Id, new CreateManyToManyRelationRequest(_projets.Id, null, null, null)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.source", result.Error.Code);
        Assert.Empty(_sent);
    }

    [Fact]
    public async Task Unknown_or_other_tenant_source_is_not_found()
    {
        // The repository is tenant-scoped: an entity of another tenant is simply not returned.
        var foreign = Guid.NewGuid();

        var result = await Handler().Handle(
            new CreateManyToManyRelationCommand(foreign, new CreateManyToManyRelationRequest(_projets.Id, null, null, null)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("CustomEntity.NotFound", result.Error.Code);
        Assert.Empty(_sent);
    }

    [Fact]
    public async Task Unknown_target_is_a_validation_error()
    {
        var result = await Handler().Handle(
            new CreateManyToManyRelationCommand(_employes.Id, new CreateManyToManyRelationRequest(Guid.NewGuid(), null, null, null)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.target", result.Error.Code);
        Assert.Empty(_sent);
    }

    [Fact]
    public async Task Second_field_failure_compensates_by_deleting_the_junction_and_returns_the_original_error()
    {
        var fieldError = Error.Validation("key", "Un champ avec la clé « projets » existe déjà.");
        var calls = 0;
        _mediator.Setup(m => m.Send(It.IsAny<CreateCustomFieldCommand>(), It.IsAny<CancellationToken>()))
            .Returns((CreateCustomFieldCommand c, CancellationToken _) =>
            {
                _sent.Add(c);
                calls++;
                return Task.FromResult(calls == 1
                    ? Result.Success(FieldDto(c.Request, 1))
                    : Result.Failure<CustomFieldDto>(fieldError));
            });

        var result = await Handler().Handle(
            new CreateManyToManyRelationCommand(_employes.Id, new CreateManyToManyRelationRequest(_projets.Id, null, null, null)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(fieldError, result.Error);

        var junctionId = _sent.OfType<CreateCustomEntityCommand>().Single();
        var delete = Assert.Single(_sent.OfType<DeleteCustomEntityCommand>());
        Assert.Equal(EntityDto(junctionId.Request).Id, delete.Id);

        // No audit for a relation that does not exist; no index for a rolled-back junction.
        _audit.Verify(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()), Times.Never);
        _jsonIndex.Verify(j => j.EnsureFieldIndexAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Quota_failure_stops_before_any_write()
    {
        var quotaError = Error.Validation("quota", "Quota de tables atteint.");
        _quota.Setup(q => q.EnsureUnderLimitAsync(Tid, StudioQuotas.MaxEntitiesKey, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(quotaError));

        var result = await Handler().Handle(
            new CreateManyToManyRelationCommand(_employes.Id, new CreateManyToManyRelationRequest(_projets.Id, null, null, null)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(quotaError, result.Error);
        Assert.Empty(_sent);
    }

    [Fact]
    public async Task Missing_tenant_is_unauthorized()
    {
        _currentUser.SetupGet(u => u.TenantId).Returns((Guid?)null);

        var result = await Handler().Handle(
            new CreateManyToManyRelationCommand(_employes.Id, new CreateManyToManyRelationRequest(_projets.Id, null, null, null)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Unauthorized", result.Error.Code);
        Assert.Empty(_sent);
    }

    [Theory]
    [InlineData("employes", "projets", "employes", "projets")]
    [InlineData("id", "projets", "id_ref", "projets")]          // reserved field key → _ref
    [InlineData("contacts", "contacts", "contacts_a", "contacts_b")] // self-join → _a/_b
    public void Field_keys_avoid_reserved_names_and_self_join_collisions(string source, string target, string expectedSource, string expectedTarget)
    {
        var (s, t) = CreateManyToManyRelationCommandHandler.ResolveFieldKeys(source, target);
        Assert.Equal(expectedSource, s);
        Assert.Equal(expectedTarget, t);
        Assert.True(StudioKey.IsValidShape(s));
        Assert.True(StudioKey.IsValidShape(t));
        Assert.False(StudioKey.IsReservedFieldKey(s));
        Assert.False(StudioKey.IsReservedFieldKey(t));
    }

    // ---- helpers ----

    private CreateManyToManyRelationCommandHandler Handler() => new(
        _entities.Object, _fields.Object, _quota.Object, _audit.Object, _currentUser.Object, _mediator.Object, _jsonIndex.Object);

    private void RegisterEntity(CustomEntityDefinition entity) =>
        _entities.Setup(r => r.GetByIdAsync(Tid, entity.Id, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

    /// <summary>Deterministic junction id derived from the key so the compensation test can match it.</summary>
    private static CustomEntityDto EntityDto(CreateCustomEntityRequest req)
    {
        var bytes = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(req.Key));
        var id = new Guid(bytes);
        return new CustomEntityDto(id, req.Key, req.DisplayName, req.DisplayNamePlural ?? req.DisplayName, req.Icon, req.Description,
            true, 0, req.SystemId, DateTime.UtcNow, DateTime.UtcNow, req.Kind);
    }

    private static CustomFieldDto FieldDto(CreateCustomFieldRequest req, int sortOrder) =>
        new(Guid.NewGuid(), req.Key, req.Label, req.FieldType, req.IsRequired, req.IsUnique, sortOrder, req.Rules, req.Options, req.Relation, true);
}
