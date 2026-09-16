using FactuTrust.Domain.Entities.Studio.Workflows;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Repositories.Studio;
using FactuTrust.Infrastructure.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

/// <summary>
/// Studio IA — PR 4.1c : <see cref="StudioWorkflowRepository"/> contre un SQL Server RÉEL.
/// Vérifie l'isolement par tenant (TenantId dans chaque requête), le masquage des définitions
/// supprimées (filtre global + index unique filtré), la concurrence optimiste sur RowVersion,
/// l'ordre et la borne <c>Math.Clamp(max, 1, 200)</c> des instances, le prédicat de chaîne
/// d'origine et le tri des exécutions d'étapes. Une base dédiée par classe
/// (<see cref="SqlTestDatabase"/>) ; <c>Skipped</c> explicite sans SQL.
/// </summary>
public sealed class StudioWorkflowRepositoryTests : IClassFixture<StudioWorkflowRepositoryTests.SqlFixture>
{
    private const string SkipMessage = "SQL Server/LocalDB indisponible dans ce bac à sable.";

    private readonly SqlFixture _sql;

    public StudioWorkflowRepositoryTests(SqlFixture sql) => _sql = sql;

    [SkippableFact]
    public async Task Definitions_are_isolated_by_tenant_and_soft_deleted_ones_are_hidden()
    {
        Skip.If(!_sql.CanRun, SkipMessage);

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var repo = _sql.NewRepository();

        var activeA = NewDefinition(tenantA, entityId, "active_a");
        var deletedA = NewDefinition(tenantA, entityId, "deleted_a");
        deletedA.SoftDelete(null);
        var otherTenant = NewDefinition(tenantB, entityId, "active_a"); // même clé, autre tenant : autorisé.

        await repo.AddDefinitionAsync(activeA);
        await repo.AddDefinitionAsync(deletedA);
        await repo.AddDefinitionAsync(otherTenant);

        var listA = await repo.ListByEntityAsync(tenantA, entityId);
        Assert.Equal(new[] { activeA.Id }, listA.Select(d => d.Id).ToArray());

        var listB = await repo.ListByEntityAsync(tenantB, entityId);
        Assert.Equal(new[] { otherTenant.Id }, listB.Select(d => d.Id).ToArray());

        Assert.Null(await repo.GetDefinitionAsync(tenantB, activeA.Id));
        Assert.Null(await repo.GetDefinitionAsync(tenantA, deletedA.Id));
        Assert.NotNull(await repo.GetDefinitionAsync(tenantA, activeA.Id));
    }

    [SkippableFact]
    public async Task Filtered_unique_key_allows_reuse_after_soft_delete()
    {
        Skip.If(!_sql.CanRun, SkipMessage);

        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var repo = _sql.NewRepository();

        var deleted = NewDefinition(tenantId, entityId, "reuse");
        deleted.SoftDelete(null);
        await repo.AddDefinitionAsync(deleted);

        // L'index unique filtré ([IsDeleted] = 0) ignore la ligne supprimée : la clé est réutilisable.
        var active = NewDefinition(tenantId, entityId, "reuse");
        await repo.AddDefinitionAsync(active);

        var duplicate = NewDefinition(tenantId, entityId, "reuse");
        await Assert.ThrowsAsync<DbUpdateException>(() => repo.AddDefinitionAsync(duplicate));
    }

    [SkippableFact]
    public async Task Update_with_stale_row_version_throws_concurrency_exception()
    {
        Skip.If(!_sql.CanRun, SkipMessage);

        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var repo = _sql.NewRepository();

        var definition = NewDefinition(tenantId, entityId, "concurrency");
        await repo.AddDefinitionAsync(definition);

        var loaded = await repo.GetDefinitionAsync(tenantId, definition.Id);
        Assert.NotNull(loaded);
        loaded!.Update("Nouveau nom", null, StudioWorkflowTriggerKind.OnCreate, "{}", "[]", null);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => repo.UpdateDefinitionWithConcurrencyAsync(loaded, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }));
    }

    [SkippableFact]
    public async Task List_active_by_trigger_returns_only_active_definitions_of_that_trigger()
    {
        Skip.If(!_sql.CanRun, SkipMessage);

        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var repo = _sql.NewRepository();

        var activeOnCreate = NewDefinition(tenantId, entityId, "on_create_active", isActive: true, StudioWorkflowTriggerKind.OnCreate);
        var inactiveOnCreate = NewDefinition(tenantId, entityId, "on_create_inactive", isActive: false, StudioWorkflowTriggerKind.OnCreate);
        var activeOnUpdate = NewDefinition(tenantId, entityId, "on_update_active", isActive: true, StudioWorkflowTriggerKind.OnUpdate);

        await repo.AddDefinitionAsync(activeOnCreate);
        await repo.AddDefinitionAsync(inactiveOnCreate);
        await repo.AddDefinitionAsync(activeOnUpdate);

        var list = await repo.ListActiveByTriggerAsync(tenantId, entityId, StudioWorkflowTriggerKind.OnCreate);

        Assert.Equal(new[] { activeOnCreate.Id }, list.Select(d => d.Id).ToArray());
    }

    [SkippableFact]
    public async Task Instances_for_record_are_ordered_newest_first_and_clamped()
    {
        Skip.If(!_sql.CanRun, SkipMessage);

        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var repo = _sql.NewRepository();

        var definition = NewDefinition(tenantId, entityId, "inst_clamp");
        await repo.AddDefinitionAsync(definition);

        var recordId = Guid.NewGuid();
        var ids = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            var instance = NewInstance(tenantId, definition, recordId);
            await repo.AddInstanceAsync(instance);
            ids.Add(instance.Id);
        }

        var two = await repo.ListInstancesForRecordAsync(tenantId, recordId, 2);
        Assert.Equal(2, two.Count);
        Assert.True(two[0].StartedAt >= two[1].StartedAt);

        // max = 0 est borné à 1 (Math.Clamp(max, 1, 200)).
        var clampedToOne = await repo.ListInstancesForRecordAsync(tenantId, recordId, 0);
        Assert.Single(clampedToOne);

        // max = 999 est borné à 200 : les 5 instances reviennent, plus récentes d'abord.
        var all = await repo.ListInstancesForRecordAsync(tenantId, recordId, 999);
        Assert.Equal(ids.ToHashSet(), all.Select(i => i.Id).ToHashSet());
        Assert.Equal(all.Select(i => i.StartedAt).OrderByDescending(t => t), all.Select(i => i.StartedAt));
    }

    [SkippableFact]
    public async Task Has_open_instance_in_chain_detects_running_waiting_and_origin_links()
    {
        Skip.If(!_sql.CanRun, SkipMessage);

        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var repo = _sql.NewRepository();

        var definition = NewDefinition(tenantId, entityId, "chain");
        await repo.AddDefinitionAsync(definition);
        var recordId = Guid.NewGuid();

        // En cours ⇒ chaîne ouverte.
        var first = NewInstance(tenantId, definition, recordId);
        await repo.AddInstanceAsync(first);
        Assert.True(await repo.HasOpenInstanceInChainAsync(tenantId, definition.Id, recordId, null));

        // En attente ⇒ toujours ouverte.
        first.Suspend(StudioWorkflowInstanceStatus.Waiting, null, "{}");
        await repo.UpdateInstanceAsync(first);
        Assert.True(await repo.HasOpenInstanceInChainAsync(tenantId, definition.Id, recordId, null));

        // Terminée ⇒ fermée.
        first.Complete("{}");
        await repo.UpdateInstanceAsync(first);
        Assert.False(await repo.HasOpenInstanceInChainAsync(tenantId, definition.Id, recordId, null));

        // Chaîne d'origine : un enfant en cours dont OriginInstanceId correspond.
        var child = NewInstance(tenantId, definition, recordId, depth: 1, originInstanceId: first.Id);
        await repo.AddInstanceAsync(child);
        Assert.True(await repo.HasOpenInstanceInChainAsync(tenantId, definition.Id, recordId, first.Id));
        // … ou dont l'Id lui-même correspond à l'origine demandée.
        Assert.True(await repo.HasOpenInstanceInChainAsync(tenantId, definition.Id, recordId, child.Id));
        // Origine inconnue ⇒ aucune instance ouverte dans cette chaîne.
        Assert.False(await repo.HasOpenInstanceInChainAsync(tenantId, definition.Id, recordId, Guid.NewGuid()));
    }

    [SkippableFact]
    public async Task Count_open_instances_for_definition_ignores_terminal_ones()
    {
        Skip.If(!_sql.CanRun, SkipMessage);

        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var repo = _sql.NewRepository();

        var definition = NewDefinition(tenantId, entityId, "count_open");
        await repo.AddDefinitionAsync(definition);

        var running = NewInstance(tenantId, definition, Guid.NewGuid());
        var waiting = NewInstance(tenantId, definition, Guid.NewGuid());
        waiting.Suspend(StudioWorkflowInstanceStatus.WaitingApproval, DateTime.UtcNow.AddHours(1), "{}");
        var completed = NewInstance(tenantId, definition, Guid.NewGuid());
        completed.Complete("{}");

        await repo.AddInstanceAsync(running);
        await repo.AddInstanceAsync(waiting);
        await repo.AddInstanceAsync(completed);

        Assert.Equal(2, await repo.CountOpenInstancesForDefinitionAsync(tenantId, definition.Id));
    }

    [SkippableFact]
    public async Task Step_runs_are_listed_by_index_and_approvals_by_instance()
    {
        Skip.If(!_sql.CanRun, SkipMessage);

        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var repo = _sql.NewRepository();

        var definition = NewDefinition(tenantId, entityId, "runs");
        await repo.AddDefinitionAsync(definition);
        var instance = NewInstance(tenantId, definition, Guid.NewGuid());
        await repo.AddInstanceAsync(instance);
        var otherInstance = NewInstance(tenantId, definition, Guid.NewGuid());
        await repo.AddInstanceAsync(otherInstance);

        // Insertions volontairement désordonnées.
        var now = DateTime.UtcNow;
        await repo.AddStepRunAsync(StudioWorkflowStepRun.Record(
            tenantId, instance.Id, 2, "step_c", "notify", StudioWorkflowStepRunStatus.Succeeded,
            StudioWorkflowStepOutcome.Continue, null, null, null, now, now, null));
        await repo.AddStepRunAsync(StudioWorkflowStepRun.Record(
            tenantId, instance.Id, 0, "step_a", "condition", StudioWorkflowStepRunStatus.Succeeded,
            StudioWorkflowStepOutcome.Continue, null, null, null, now, now, null));
        await repo.AddStepRunAsync(StudioWorkflowStepRun.Record(
            tenantId, instance.Id, 1, "step_b", "update_field", StudioWorkflowStepRunStatus.Skipped,
            StudioWorkflowStepOutcome.Skip, null, null, null, now, now, null));

        var runs = await repo.ListStepRunsAsync(tenantId, instance.Id);
        Assert.Equal(new[] { 0, 1, 2 }, runs.Select(r => r.StepIndex).ToArray());

        var pending1 = StudioWorkflowApproval.Create(tenantId, instance.Id, "step_a", null, "Administrators", "Approbation 1", null, null);
        var pending2 = StudioWorkflowApproval.Create(tenantId, instance.Id, "step_c", null, "Administrators", "Approbation 2", null, null);
        var decided = StudioWorkflowApproval.Create(tenantId, instance.Id, "step_b", null, "Administrators", "Approbation 3", null, null);
        decided.Decide(StudioWorkflowApprovalStatus.Approved, Guid.NewGuid(), null, DateTime.UtcNow);
        var otherInstancePending = StudioWorkflowApproval.Create(tenantId, otherInstance.Id, "step_a", null, "Administrators", "Approbation 4", null, null);

        await repo.AddApprovalAsync(pending1);
        await repo.AddApprovalAsync(pending2);
        await repo.AddApprovalAsync(decided);
        await repo.AddApprovalAsync(otherInstancePending);

        var pendingApprovals = await repo.ListPendingApprovalsForInstanceAsync(tenantId, instance.Id);
        Assert.Equal(
            new HashSet<Guid> { pending1.Id, pending2.Id },
            pendingApprovals.Select(a => a.Id).ToHashSet());
    }

    private static StudioWorkflowDefinition NewDefinition(
        Guid tenantId, Guid entityId, string key, bool isActive = true,
        StudioWorkflowTriggerKind trigger = StudioWorkflowTriggerKind.OnCreate)
        => StudioWorkflowDefinition.Create(tenantId, entityId, key, $"Workflow {key}", null, trigger, "{}", "[]", isActive, null);

    private static StudioWorkflowInstance NewInstance(
        Guid tenantId, StudioWorkflowDefinition definition, Guid recordId, int depth = 0, Guid? originInstanceId = null)
        => StudioWorkflowInstance.Start(
            tenantId, definition, recordId, StudioWorkflowTriggerKind.OnCreate, null, "{}", depth, originInstanceId);

    // ---- fixture ----

    /// <summary>Une base SQL dédiée à la classe (tables issues du modèle EF, tranche 4.1b1).</summary>
    public sealed class SqlFixture : IDisposable
    {
        private readonly SqlTestDatabase _db = new(nameof(StudioWorkflowRepositoryTests));

        public SqlFixture()
        {
            if (!_db.CanRun)
                return;

            Options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseSqlServer(_db.ConnectionString!)
                .Options;
            Factory = new SingleConnectionTenantDbContextFactory(Options);
        }

        public bool CanRun => _db.CanRun;
        public DbContextOptions<TenantDbContext> Options { get; } = null!;
        public ITenantDbContextFactory Factory { get; } = null!;

        public StudioWorkflowRepository NewRepository() => new(Factory);

        public void Dispose() => _db.Dispose();

        private sealed class SingleConnectionTenantDbContextFactory : ITenantDbContextFactory
        {
            private readonly DbContextOptions<TenantDbContext> _options;
            public SingleConnectionTenantDbContextFactory(DbContextOptions<TenantDbContext> options) => _options = options;
            public TenantDbContext CreateContext() => new(_options);
        }
    }
}
