using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.Studio.Workflows;
using FactuTrust.Application.Features.Studio.Workflows.Engine;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Entities.Studio.Workflows;
using FactuTrust.Domain.Enums;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

/// <summary>
/// 4.2e — boîte de réception + décision d'approbation (utilisateurs réels, sans HTTP) :
/// ciblage utilisateur/rôle, libellé premier champ texte, commentaire obligatoire au refus,
/// contexte mis à jour + instance due, reprise inline via le runner, notification + audit sans commentaire.
/// </summary>
public class StudioWorkflowApprovalFeaturesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ApproverId = Guid.NewGuid();
    private static readonly Guid StartedBy = Guid.NewGuid();

    private readonly Mock<IStudioWorkflowRepository> _workflows = new(MockBehavior.Strict);
    private readonly Mock<ICustomEntityRepository> _entities = new(MockBehavior.Strict);
    private readonly Mock<ICustomFieldRepository> _fields = new(MockBehavior.Strict);
    private readonly Mock<ICustomRecordRepository> _records = new(MockBehavior.Strict);
    private readonly Mock<IStudioWorkflowRunner> _runner = new(MockBehavior.Strict);
    private readonly Mock<INotificationService> _notifications = new(MockBehavior.Strict);
    private readonly Mock<IAuditService> _audit = new(MockBehavior.Strict);
    private readonly Mock<ICurrentUser> _currentUser = new(MockBehavior.Strict);
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 17, 10, 0, 0, TimeSpan.Zero));

    public StudioWorkflowApprovalFeaturesTests()
    {
        _currentUser.Setup(c => c.TenantId).Returns(TenantId);
        _currentUser.Setup(c => c.UserId).Returns(ApproverId);
        _currentUser.Setup(c => c.Role).Returns(UserRole.SalesRep);
    }

    private ListMyApprovalsQueryHandler CreateListHandler() =>
        new(_workflows.Object, _entities.Object, _fields.Object, _records.Object, _currentUser.Object);

    private CountMyApprovalsQueryHandler CreateCountHandler() => new(_workflows.Object, _currentUser.Object);

    private DecideApprovalCommandHandler CreateDecideHandler() =>
        new(_workflows.Object, _runner.Object, _notifications.Object, _entities.Object, _audit.Object,
            _currentUser.Object, _time);

    private void SetupReadPermission(bool granted = true) =>
        _currentUser.Setup(c => c.HasPermission(Permissions.CustomData.RecordsRead)).Returns(granted);

    private void SetupWritePermission(bool granted = true) =>
        _currentUser.Setup(c => c.HasPermission(Permissions.CustomData.RecordsWrite)).Returns(granted);

    [Fact]
    public async Task ListMyApprovals_returns_items_assigned_by_user_or_role_with_workflow_and_record_context()
    {
        var entity = CustomEntityDefinition.Create(TenantId, "customer", "Client", "Clients", null, null, null);
        var definition = NewDefinition(entity.Id);
        var directInstance = NewInstance(definition);
        var roleInstance = NewInstance(definition);
        var direct = NewApproval(directInstance.Id, assigneeUserId: ApproverId);
        var byRole = NewApproval(roleInstance.Id, assigneeRole: nameof(UserRole.SalesRep));
        var field = CustomFieldDefinition.Create(TenantId, entity.Id, "name", "Nom", CustomFieldType.Text,
            false, false, 0, null, null, null, null);
        var record = CustomRecord.Create(TenantId, entity.Id, "{\"name\":\"Dossier A\"}", StartedBy);

        SetupReadPermission();
        _workflows.Setup(r => r.ListPendingApprovalsForUserAsync(TenantId, ApproverId, nameof(UserRole.SalesRep), 100,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StudioWorkflowApproval> { direct, byRole });
        foreach (var (approval, instance) in new[] { (direct, directInstance), (byRole, roleInstance) })
        {
            _workflows.Setup(r => r.GetInstanceAsync(TenantId, approval.InstanceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(instance);
            _workflows.Setup(r => r.GetDefinitionAsync(TenantId, definition.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(definition);
            _entities.Setup(r => r.GetByIdAsync(TenantId, entity.Id, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
            _records.Setup(r => r.GetAsync(TenantId, entity.Id, instance.RecordId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(record);
            _fields.Setup(r => r.ListByEntityAsync(TenantId, entity.Id, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CustomFieldDefinition> { field });
        }

        var result = await CreateListHandler().Handle(new ListMyApprovalsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.All(result.Value, item =>
        {
            Assert.Equal(definition.Key, item.WorkflowKey);
            Assert.Equal(definition.Name, item.WorkflowName);
            Assert.Equal("customer", item.EntityKey);
            Assert.Equal("Client", item.EntityName);
            Assert.Equal("Dossier A", item.RecordLabel);
            Assert.Equal(StartedBy, item.StartedBy);
            Assert.Equal("pending", item.Approval.Status);
        });
    }

    [Fact]
    public async Task ListMyApprovals_clamps_max_to_200()
    {
        SetupReadPermission();
        _workflows.Setup(r => r.ListPendingApprovalsForUserAsync(TenantId, ApproverId, nameof(UserRole.SalesRep), 200,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StudioWorkflowApproval>());

        var result = await CreateListHandler().Handle(new ListMyApprovalsQuery(Max: 10_000), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task ListMyApprovals_skips_items_whose_instance_is_missing_or_terminal()
    {
        var definition = NewDefinition(Guid.NewGuid());
        var terminalInstance = NewInstance(definition);
        terminalInstance.Fail("boom");
        var orphan = NewApproval(Guid.NewGuid(), assigneeUserId: ApproverId);
        var closed = NewApproval(terminalInstance.Id, assigneeRole: nameof(UserRole.SalesRep));

        SetupReadPermission();
        _workflows.Setup(r => r.ListPendingApprovalsForUserAsync(TenantId, ApproverId, nameof(UserRole.SalesRep), 100,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StudioWorkflowApproval> { orphan, closed });
        _workflows.Setup(r => r.GetInstanceAsync(TenantId, orphan.InstanceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StudioWorkflowInstance?)null);
        _workflows.Setup(r => r.GetInstanceAsync(TenantId, closed.InstanceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(terminalInstance);

        var result = await CreateListHandler().Handle(new ListMyApprovalsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task CountMyApprovals_uses_single_count_query()
    {
        SetupReadPermission();
        _workflows.Setup(r => r.CountPendingApprovalsForUserAsync(TenantId, ApproverId, nameof(UserRole.SalesRep),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var result = await CreateCountHandler().Handle(new CountMyApprovalsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Count);
        _workflows.Verify(r => r.ListPendingApprovalsForUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Decide_returns_NotFound_when_approval_is_missing_or_not_assigned_to_current_user()
    {
        SetupWritePermission();
        var othersApproval = NewApproval(Guid.NewGuid(), assigneeUserId: Guid.NewGuid());
        _workflows.Setup(r => r.GetApprovalAsync(TenantId, othersApproval.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(othersApproval);
        _workflows.Setup(r => r.GetApprovalAsync(TenantId, It.Is<Guid>(id => id != othersApproval.Id),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((StudioWorkflowApproval?)null);

        var missing = await CreateDecideHandler()
            .Handle(new DecideApprovalCommand(Guid.NewGuid(), Approve: true, null), CancellationToken.None);
        var foreign = await CreateDecideHandler()
            .Handle(new DecideApprovalCommand(othersApproval.Id, Approve: true, null), CancellationToken.None);

        Assert.False(missing.IsSuccess);
        Assert.Equal("StudioWorkflowApproval.NotFound", missing.Error.Code);
        Assert.False(foreign.IsSuccess);
        Assert.Equal("StudioWorkflowApproval.NotFound", foreign.Error.Code);
    }

    [Fact]
    public async Task Decide_returns_Conflict_when_already_decided()
    {
        SetupWritePermission();
        var approval = NewApproval(Guid.NewGuid(), assigneeUserId: ApproverId);
        approval.Decide(StudioWorkflowApprovalStatus.Approved, ApproverId, null, DateTime.UtcNow);
        _workflows.Setup(r => r.GetApprovalAsync(TenantId, approval.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(approval);

        var result = await CreateDecideHandler()
            .Handle(new DecideApprovalCommand(approval.Id, Approve: false, "Non"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Conflict", result.Error.Code);
    }

    [Fact]
    public async Task Decide_reject_without_comment_returns_Validation_comment()
    {
        SetupWritePermission();
        var approval = NewApproval(Guid.NewGuid(), assigneeUserId: ApproverId);
        _workflows.Setup(r => r.GetApprovalAsync(TenantId, approval.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(approval);

        var result = await CreateDecideHandler()
            .Handle(new DecideApprovalCommand(approval.Id, Approve: false, "  "), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Validation.comment", result.Error.Code);
        _workflows.Verify(r => r.UpdateApprovalAsync(It.IsAny<StudioWorkflowApproval>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Decide_approve_writes_approved_status_in_context_and_makes_instance_due()
    {
        SetupWritePermission();
        var definition = NewDefinition(Guid.NewGuid());
        var instance = NewInstance(definition);
        instance.Suspend(StudioWorkflowInstanceStatus.WaitingApproval, DateTime.UtcNow.AddHours(-1), "{}");
        var approval = NewApproval(instance.Id, assigneeUserId: ApproverId);
        SetupOpenInstanceDecision(definition, instance, approval);

        var result = await CreateDecideHandler()
            .Handle(new DecideApprovalCommand(approval.Id, Approve: true, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(StudioWorkflowApprovalStatus.Approved, approval.Status);
        Assert.Equal(ApproverId, approval.DecidedBy);
        var context = StudioWorkflowContext.Parse(instance.ContextJson);
        Assert.Equal("approved", context.Approval[approval.StepKey]?["status"]?.GetValue<string>());
        // L'instance est devenue due sans changer d'étape (D-05).
        Assert.Equal(StudioWorkflowInstanceStatus.WaitingApproval, instance.Status);
        Assert.Equal(_time.GetUtcNow().UtcDateTime, instance.DueAt);
    }

    [Fact]
    public async Task Decide_resumes_instance_through_runner_and_returns_dto()
    {
        SetupWritePermission();
        var definition = NewDefinition(Guid.NewGuid());
        var instance = NewInstance(definition);
        instance.Suspend(StudioWorkflowInstanceStatus.WaitingApproval, DateTime.UtcNow.AddHours(-1), "{}");
        var approval = NewApproval(instance.Id, assigneeRole: nameof(UserRole.SalesRep));
        SetupOpenInstanceDecision(definition, instance, approval);

        var result = await CreateDecideHandler()
            .Handle(new DecideApprovalCommand(approval.Id, Approve: false, "Budget dépassé"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(instance.Id, result.Value.Id);
        Assert.Equal(definition.Key, result.Value.WorkflowKey);
        Assert.Equal(definition.Name, result.Value.WorkflowName);
        _runner.Verify(r => r.ResumeUnderStarterAsync(instance, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Decide_notifies_starter_and_audits_without_comment_in_audit()
    {
        SetupWritePermission();
        var entity = CustomEntityDefinition.Create(TenantId, "customer", "Client", "Clients", null, null, null);
        var definition = NewDefinition(entity.Id);
        var instance = NewInstance(definition);
        instance.Suspend(StudioWorkflowInstanceStatus.WaitingApproval, DateTime.UtcNow.AddHours(-1), "{}");
        var approval = NewApproval(instance.Id, assigneeUserId: ApproverId);

        _workflows.Setup(r => r.GetApprovalAsync(TenantId, approval.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(approval);
        _workflows.Setup(r => r.UpdateApprovalAsync(approval, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _workflows.Setup(r => r.GetInstanceAsync(TenantId, instance.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(instance);
        _workflows.Setup(r => r.GetDefinitionAsync(TenantId, definition.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(definition);
        _workflows.Setup(r => r.UpdateInstanceAsync(instance, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _entities.Setup(r => r.GetByIdAsync(TenantId, entity.Id, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        _notifications.Setup(n => n.CreateAsync(TenantId, null, NotificationType.StudioWorkflowApprovalDecided,
                "Approbation « Accord ? » refusée", "Trop risqué", $"/studio/d/customer/{instance.RecordId}/edit",
                StartedBy, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _runner.Setup(r => r.ResumeUnderStarterAsync(instance, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StudioWorkflowRunOutcome.Resumed);
        object? auditNewValues = null;
        _audit.Setup(a => a.LogAsync("Studio.Workflow.ApprovalDecided", "StudioWorkflowApproval", approval.Id, null,
                It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, Guid?, object?, object?, CancellationToken>((_, _, _, _, nv, _) => auditNewValues = nv)
            .Returns(Task.CompletedTask);

        var result = await CreateDecideHandler()
            .Handle(new DecideApprovalCommand(approval.Id, Approve: false, "Trop risqué"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _notifications.Verify(n => n.CreateAsync(TenantId, null, NotificationType.StudioWorkflowApprovalDecided,
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), StartedBy, It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.NotNull(auditNewValues);
        var serialized = System.Text.Json.JsonSerializer.Serialize(auditNewValues);
        Assert.Contains("rejected", serialized);
        Assert.DoesNotContain("Trop risqué", serialized); // commentaire absent de l'audit (S-base)
    }

    [Fact]
    public async Task Decide_requires_records_write_permission()
    {
        SetupWritePermission(granted: false);

        var result = await CreateDecideHandler()
            .Handle(new DecideApprovalCommand(Guid.NewGuid(), Approve: true, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Unauthorized", result.Error.Code);
        _workflows.Verify(r => r.GetApprovalAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>Câblage commun des trois tests de décision sur instance ouverte.</summary>
    private void SetupOpenInstanceDecision(
        StudioWorkflowDefinition definition, StudioWorkflowInstance instance, StudioWorkflowApproval approval)
    {
        _workflows.Setup(r => r.GetApprovalAsync(TenantId, approval.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(approval);
        _workflows.Setup(r => r.UpdateApprovalAsync(approval, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _workflows.Setup(r => r.GetInstanceAsync(TenantId, instance.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(instance);
        _workflows.Setup(r => r.GetDefinitionAsync(TenantId, definition.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(definition);
        _workflows.Setup(r => r.UpdateInstanceAsync(instance, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _entities.Setup(r => r.GetByIdAsync(TenantId, definition.EntityDefinitionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomEntityDefinition?)null);
        _notifications.Setup(n => n.CreateAsync(TenantId, null, NotificationType.StudioWorkflowApprovalDecided,
                It.IsAny<string>(), It.IsAny<string>(), null, StartedBy, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _runner.Setup(r => r.ResumeUnderStarterAsync(instance, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StudioWorkflowRunOutcome.Resumed);
        _audit.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private static StudioWorkflowDefinition NewDefinition(Guid entityId) =>
        StudioWorkflowDefinition.Create(TenantId, entityId, "wf-approve", "Approbation client", null,
            StudioWorkflowTriggerKind.OnCreate, "{}", "[]", true, StartedBy);

    private static StudioWorkflowInstance NewInstance(StudioWorkflowDefinition definition) =>
        StudioWorkflowInstance.Start(TenantId, definition, Guid.NewGuid(), StudioWorkflowTriggerKind.OnCreate,
            StartedBy, "{}", 0, null);

    private static StudioWorkflowApproval NewApproval(Guid instanceId, Guid? assigneeUserId = null, string? assigneeRole = null) =>
        StudioWorkflowApproval.Create(TenantId, instanceId, "approve", assigneeUserId, assigneeRole, "Accord ?", null, null);

    /// <summary>Horloge figée (pas de dépendance au package Microsoft.Extensions.TimeProvider.Testing).</summary>
    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
