using FactuTrust.Domain.Entities.Studio.Workflows;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Migrations;

/// <summary>
/// Studio IA — PR 4.1 : verrouille le modèle EF des quatre tables de workflows Studio
/// (<c>StudioWorkflowDefinitions</c>, <c>StudioWorkflowInstances</c>, <c>StudioWorkflowStepRuns</c>,
/// <c>StudioWorkflowApprovals</c>) : noms de tables, enums stockés en int, jetons de concurrence,
/// filtre de suppression logique + clé unique filtrée sur les définitions, les 9 index aux noms
/// figés (annexe A-41 §0.4) et l'absence totale de clé étrangère (tables autonomes).
/// Assertions sur le modèle EF en mémoire (aucun serveur SQL requis). Ce fichier est complété
/// en 4.1b2 par les assertions sur la migration manuelle
/// <c>20260912150000_AddStudioWorkflows_Tenant</c>, le snapshot et le jumeau SQL idempotent.
/// </summary>
public sealed class AddStudioWorkflowsMigrationTests
{
    /// <summary>Noms d'index figés par l'annexe A-41 §0.4 — toute modification est un acte délibéré.</summary>
    private static readonly string[] FrozenIndexNames =
    {
        "UX_StudioWorkflowDefinitions_Tenant_Entity_Key",
        "IX_StudioWorkflowDefinitions_Tenant_Entity_Trigger_Active",
        "IX_StudioWorkflowInstances_Tenant_Status_DueAt",
        "IX_StudioWorkflowInstances_Tenant_Record_StartedAt",
        "IX_StudioWorkflowInstances_Tenant_Definition_StartedAt",
        "IX_StudioWorkflowStepRuns_Tenant_Instance_Step",
        "IX_StudioWorkflowApprovals_Tenant_AssigneeUser_Status",
        "IX_StudioWorkflowApprovals_Tenant_AssigneeRole_Status",
        "IX_StudioWorkflowApprovals_Tenant_Instance",
    };

    private static readonly Type[] WorkflowEntityTypes =
    {
        typeof(StudioWorkflowDefinition),
        typeof(StudioWorkflowInstance),
        typeof(StudioWorkflowStepRun),
        typeof(StudioWorkflowApproval),
    };

    private static TenantDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseInMemoryDatabase($"studio-workflows-model-{Guid.NewGuid():N}")
            .Options;
        return new TenantDbContext(options);
    }

    [Fact]
    public void Runtime_model_maps_the_four_workflow_tables_with_expected_names()
    {
        using var context = CreateInMemoryContext();

        var definition = context.Model.FindEntityType(typeof(StudioWorkflowDefinition));
        var instance = context.Model.FindEntityType(typeof(StudioWorkflowInstance));
        var stepRun = context.Model.FindEntityType(typeof(StudioWorkflowStepRun));
        var approval = context.Model.FindEntityType(typeof(StudioWorkflowApproval));

        Assert.NotNull(definition);
        Assert.NotNull(instance);
        Assert.NotNull(stepRun);
        Assert.NotNull(approval);
        Assert.Equal("StudioWorkflowDefinitions", definition!.GetTableName());
        Assert.Equal("StudioWorkflowInstances", instance!.GetTableName());
        Assert.Equal("StudioWorkflowStepRuns", stepRun!.GetTableName());
        Assert.Equal("StudioWorkflowApprovals", approval!.GetTableName());
    }

    [Fact]
    public void Definition_has_soft_delete_filter_and_filtered_unique_key()
    {
        using var context = CreateInMemoryContext();

        var entityType = context.Model.FindEntityType(typeof(StudioWorkflowDefinition));
        Assert.NotNull(entityType);

        // Filtre global IsDeleted : aucune requête n'expose une définition supprimée.
        Assert.NotNull(entityType!.GetQueryFilter());

        // Index unique filtré (TenantId, EntityDefinitionId, Key) parmi les non supprimées.
        var tenantId = entityType.FindProperty(nameof(StudioWorkflowDefinition.TenantId))!;
        var entityDefId = entityType.FindProperty(nameof(StudioWorkflowDefinition.EntityDefinitionId))!;
        var key = entityType.FindProperty(nameof(StudioWorkflowDefinition.Key))!;
        var uniqueIndex = entityType.GetIndexes().Single(ix =>
            ix.IsUnique
            && ix.Properties.Count == 3
            && ix.Properties[0] == tenantId
            && ix.Properties[1] == entityDefId
            && ix.Properties[2] == key);

        Assert.Equal("[IsDeleted] = 0", uniqueIndex.GetFilter());
        Assert.Equal("UX_StudioWorkflowDefinitions_Tenant_Entity_Key", uniqueIndex.GetDatabaseName());
    }

    [Fact]
    public void Enums_are_stored_as_int()
    {
        using var context = CreateInMemoryContext();

        Assert.Equal(typeof(int), ProviderClrType<StudioWorkflowDefinition>(context, nameof(StudioWorkflowDefinition.Trigger)));
        Assert.Equal(typeof(int), ProviderClrType<StudioWorkflowInstance>(context, nameof(StudioWorkflowInstance.TriggerKind)));
        Assert.Equal(typeof(int), ProviderClrType<StudioWorkflowInstance>(context, nameof(StudioWorkflowInstance.Status)));
        Assert.Equal(typeof(int), ProviderClrType<StudioWorkflowStepRun>(context, nameof(StudioWorkflowStepRun.Status)));
        Assert.Equal(typeof(int), ProviderClrType<StudioWorkflowApproval>(context, nameof(StudioWorkflowApproval.Status)));
    }

    [Fact]
    public void Row_versions_are_concurrency_tokens()
    {
        using var context = CreateInMemoryContext();

        // La table des exécutions d'étapes est append-only : pas de RowVersion.
        AssertRowVersionIsConcurrencyToken<StudioWorkflowDefinition>(context);
        AssertRowVersionIsConcurrencyToken<StudioWorkflowInstance>(context);
        AssertRowVersionIsConcurrencyToken<StudioWorkflowApproval>(context);
    }

    [Fact]
    public void Nine_indexes_carry_the_frozen_names()
    {
        using var context = CreateInMemoryContext();

        var actual = WorkflowEntityTypes
            .SelectMany(t => context.Model.FindEntityType(t)!.GetIndexes())
            .Select(ix => ix.GetDatabaseName())
            .ToList();

        Assert.Equal(FrozenIndexNames.Length, actual.Count);
        Assert.Equal(
            FrozenIndexNames.OrderBy(n => n, StringComparer.Ordinal),
            actual.OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void No_foreign_key_is_declared()
    {
        using var context = CreateInMemoryContext();

        foreach (var entityType in WorkflowEntityTypes)
        {
            Assert.Empty(context.Model.FindEntityType(entityType)!.GetForeignKeys());
        }
    }

    private static Type? ProviderClrType<TEntity>(TenantDbContext context, string propertyName)
    {
        var entityType = context.Model.FindEntityType(typeof(TEntity));
        Assert.NotNull(entityType);
        var property = entityType!.FindProperty(propertyName);
        Assert.NotNull(property);
        return property!.GetProviderClrType();
    }

    private static void AssertRowVersionIsConcurrencyToken<TEntity>(TenantDbContext context)
    {
        var entityType = context.Model.FindEntityType(typeof(TEntity));
        Assert.NotNull(entityType);
        var property = entityType!.FindProperty(nameof(StudioWorkflowDefinition.RowVersion));
        Assert.NotNull(property);
        Assert.True(
            property!.IsConcurrencyToken,
            $"{typeof(TEntity).Name}.RowVersion doit être un jeton de concurrence.");
    }
}
