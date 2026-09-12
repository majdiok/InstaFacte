using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Migrations;

/// <summary>
/// Studio IA — PR 2.3 : verrouille le contrat de la migration tenant créant
/// <c>CustomRecordViewDefinitions</c> (vues Liste / Kanban / Calendrier) : gardes idempotentes
/// (<c>IF OBJECT_ID</c> / <c>IF NOT EXISTS</c>), jumeau SQL cohérent (même MigrationId, historique
/// EF inscrit), snapshot du modèle à jour. Assertions sur le TEXTE SOURCE (aucun serveur SQL requis)
/// + un contrôle du modèle EF en mémoire.
/// </summary>
public sealed class AddStudioRecordViewsMigrationTests
{
    private const string MigrationId = "20260912140000_AddStudioRecordViews_Tenant";

    /// <summary>Migration qui précède immédiatement celle-ci (PR 2.1).</summary>
    private const string PreviousMigrationId = "20260912130000_AddStudioEntityKind_Tenant";

    private static string RepoRelativePath(params string[] segments) => Path.GetFullPath(Path.Combine(
        new[] { AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", ".." }
            .Concat(segments)
            .ToArray()));

    [Fact]
    public void Migration_creates_the_table_and_indexes_behind_existence_guards()
    {
        var sourcePath = RepoRelativePath(
            "src", "Backend", "FactuTrust.Infrastructure", "Migrations", "Tenant", $"{MigrationId}.cs");

        Assert.True(File.Exists(sourcePath), $"Migration source not found: {sourcePath}");

        var source = File.ReadAllText(sourcePath);

        Assert.Contains($"[Migration(\"{MigrationId}\")]", source);
        Assert.Contains("[DbContext(typeof(TenantDbContext))]", source);
        Assert.Contains("IF OBJECT_ID(N'[dbo].[CustomRecordViewDefinitions]', N'U') IS NULL", source);
        Assert.Contains("CREATE TABLE [dbo].[CustomRecordViewDefinitions]", source);
        Assert.Contains("[Mode] int NOT NULL CONSTRAINT [DF_CustomRecordViewDefinitions_Mode] DEFAULT (0)", source);
        Assert.Contains("CONSTRAINT [FK_CustomRecordViewDefinitions_CustomEntityDefinitions] FOREIGN KEY ([EntityDefinitionId])", source);
        Assert.Contains("ON DELETE CASCADE", source);
        Assert.Contains("IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'UX_CustomRecordViewDefinitions_Tenant_Entity_Key'", source);
        Assert.Contains("CREATE UNIQUE INDEX [UX_CustomRecordViewDefinitions_Tenant_Entity_Key]", source);
        Assert.Contains("WHERE [IsDeleted] = 0", source);
        Assert.Contains("CREATE INDEX [IX_CustomRecordViewDefinitions_Tenant_Entity_Default]", source);
        // Down gardé : un rollback rejoué ne doit pas échouer.
        Assert.Contains("DROP TABLE IF EXISTS [dbo].[CustomRecordViewDefinitions]", source);
    }

    [Fact]
    public void Migration_id_is_present_and_ordered_after_its_predecessor()
    {
        var folder = RepoRelativePath("src", "Backend", "FactuTrust.Infrastructure", "Migrations", "Tenant");
        var ids = Directory.EnumerateFiles(folder, "2026*_Tenant.cs")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n is not null && !n.EndsWith(".Designer", StringComparison.Ordinal))
            .Select(n => n!)
            .ToList();

        Assert.Contains(MigrationId, ids);
        Assert.Contains(PreviousMigrationId, ids);
        // EF applique les migrations par ordre lexicographique de l'id : la nôtre suit strictement celle
        // de la PR 2.1. PAS d'assertion « la plus récente » : la PR 4.1 ajoutera 20260912150000.
        Assert.True(string.CompareOrdinal(MigrationId, PreviousMigrationId) > 0,
            $"{MigrationId} doit être ordonnée après {PreviousMigrationId}.");
    }

    [Fact]
    public void Idempotent_script_matches_the_migration_and_registers_the_history_row()
    {
        var scriptPath = RepoRelativePath(
            "docs", "runbooks", "sql", "AddStudioRecordViews_Tenant.idempotent.sql");

        Assert.True(File.Exists(scriptPath), $"Script SQL not found: {scriptPath}");

        var script = File.ReadAllText(scriptPath);

        Assert.Contains("IF OBJECT_ID(N'[dbo].[CustomRecordViewDefinitions]', N'U') IS NULL", script);
        Assert.Contains("CREATE TABLE [dbo].[CustomRecordViewDefinitions]", script);
        Assert.Contains("UX_CustomRecordViewDefinitions_Tenant_Entity_Key", script);
        Assert.Contains("IX_CustomRecordViewDefinitions_Tenant_Entity_Default", script);
        Assert.Contains($"WHERE [MigrationId] = N'{MigrationId}'", script);
        Assert.Contains($"VALUES (N'{MigrationId}', N'8.0.1')", script);
    }

    [Fact]
    public void Model_snapshot_declares_the_entity_its_filtered_unique_index_and_the_cascade()
    {
        var snapshotPath = RepoRelativePath(
            "src", "Backend", "FactuTrust.Infrastructure", "Migrations", "Tenant", "TenantDbContextModelSnapshot.cs");

        Assert.True(File.Exists(snapshotPath), $"Snapshot not found: {snapshotPath}");

        var snapshot = File.ReadAllText(snapshotPath);

        var entityStart = snapshot.IndexOf(
            "modelBuilder.Entity(\"FactuTrust.Domain.Entities.Studio.CustomRecordViewDefinition\"", StringComparison.Ordinal);
        Assert.True(entityStart >= 0, "CustomRecordViewDefinition entity block not found in the snapshot.");

        var entityEnd = snapshot.IndexOf(
            "modelBuilder.Entity(\"FactuTrust.Domain.Entities.Studio.CustomReportDefinition\"", StringComparison.Ordinal);
        Assert.True(entityEnd > entityStart, "CustomRecordViewDefinition block boundary not found.");
        var block = snapshot[entityStart..entityEnd];

        Assert.Contains("b.Property<int>(\"Mode\")", block);
        Assert.Contains("b.Property<string>(\"Key\")", block);
        Assert.Contains(".HasMaxLength(64)", block);
        Assert.Contains("b.HasIndex(\"TenantId\", \"EntityDefinitionId\", \"Key\")", block);
        Assert.Contains(".HasFilter(\"[IsDeleted] = 0\")", block);
        Assert.Contains("UX_CustomRecordViewDefinitions_Tenant_Entity_Key", block);

        // Relation en cascade vers CustomEntityDefinitions.
        Assert.Contains("b.HasOne(\"FactuTrust.Domain.Entities.Studio.CustomEntityDefinition\", null)", snapshot);
        Assert.Contains(".HasConstraintName(\"FK_CustomRecordViewDefinitions_CustomEntityDefinitions\")", snapshot);
    }

    [Fact]
    public void Runtime_model_maps_the_entity_with_soft_delete_filter_and_unique_key()
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseInMemoryDatabase($"record-views-model-{Guid.NewGuid():N}")
            .Options;
        using var context = new TenantDbContext(options);

        var entityType = context.Model.FindEntityType(typeof(CustomRecordViewDefinition));
        Assert.NotNull(entityType);
        Assert.Equal("CustomRecordViewDefinitions", entityType!.GetTableName());

        Assert.NotNull(entityType.FindProperty(nameof(CustomRecordViewDefinition.Mode)));
        Assert.Equal(typeof(int), entityType.FindProperty(nameof(CustomRecordViewDefinition.Mode))!.GetProviderClrType());

        // Filtre global IsDeleted.
        Assert.NotNull(entityType.GetQueryFilter());

        // Index unique filtré (TenantId, EntityDefinitionId, Key).
        var tenantId = entityType.FindProperty(nameof(CustomRecordViewDefinition.TenantId))!;
        var entityDefId = entityType.FindProperty(nameof(CustomRecordViewDefinition.EntityDefinitionId))!;
        var key = entityType.FindProperty(nameof(CustomRecordViewDefinition.Key))!;
        Assert.Contains(entityType.GetIndexes(), ix =>
            ix.IsUnique
            && ix.Properties.Count == 3
            && ix.Properties[0] == tenantId
            && ix.Properties[1] == entityDefId
            && ix.Properties[2] == key);

        // FK cascade vers CustomEntityDefinitions.
        var fk = entityType.GetForeignKeys().Single();
        Assert.Equal(typeof(CustomEntityDefinition), fk.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Cascade, fk.DeleteBehavior);
    }
}
