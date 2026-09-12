using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Migrations;

/// <summary>
/// Studio IA — PR 2.1 : verrouille le contrat de la migration tenant ajoutant
/// <c>CustomEntityDefinitions.Kind</c> (0 = Standard, 1 = Junction) et l'index <c>(TenantId, Kind)</c>.
/// Les bases tenant sont parfois migrées à la main (script SQL) : la migration EF et son jumeau SQL
/// doivent rester idempotents et cohérents (même MigrationId, même défaut 0), et le snapshot du modèle
/// doit connaître la colonne pour qu'une future migration générée ne la ré-ajoute pas.
/// Assertions sur le TEXTE SOURCE (aucun serveur SQL requis) + un contrôle du modèle EF en mémoire.
/// </summary>
public sealed class AddStudioEntityKindMigrationTests
{
    private const string MigrationId = "20260912130000_AddStudioEntityKind_Tenant";

    /// <summary>Chemin absolu depuis la racine du dépôt (bin/Release/net8.0 ⇒ 7 niveaux au-dessus).</summary>
    private static string RepoRelativePath(params string[] segments) => Path.GetFullPath(Path.Combine(
        new[] { AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", ".." }
            .Concat(segments)
            .ToArray()));

    [Fact]
    public void Migration_adds_kind_column_with_default_zero_and_index_behind_existence_guards()
    {
        var sourcePath = RepoRelativePath(
            "src", "Backend", "FactuTrust.Infrastructure", "Migrations", "Tenant", $"{MigrationId}.cs");

        Assert.True(File.Exists(sourcePath), $"Migration source not found: {sourcePath}");

        var source = File.ReadAllText(sourcePath);

        Assert.Contains($"[Migration(\"{MigrationId}\")]", source);
        Assert.Contains("IF COL_LENGTH(N'dbo.CustomEntityDefinitions', N'Kind') IS NULL", source);
        Assert.Contains("ADD [Kind] int NOT NULL CONSTRAINT [DF_CustomEntityDefinitions_Kind] DEFAULT (0)", source);
        Assert.Contains("IX_CustomEntityDefinitions_TenantId_Kind", source);
        Assert.Contains("ON [CustomEntityDefinitions] ([TenantId], [Kind])", source);
        // Down symétrique et lui aussi gardé : un rollback rejoué ne doit pas échouer.
        Assert.Contains("IF COL_LENGTH(N'dbo.CustomEntityDefinitions', N'Kind') IS NOT NULL", source);
        Assert.Contains("DROP CONSTRAINT [DF_CustomEntityDefinitions_Kind]", source);
        Assert.Contains("DROP COLUMN [Kind]", source);
        Assert.Contains("DROP INDEX [IX_CustomEntityDefinitions_TenantId_Kind]", source);
    }

    [Fact]
    public void Migration_id_is_the_newest_tenant_migration()
    {
        var folder = RepoRelativePath("src", "Backend", "FactuTrust.Infrastructure", "Migrations", "Tenant");
        var ids = Directory.EnumerateFiles(folder, "2026*_Tenant.cs")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n is not null && !n.EndsWith(".Designer", StringComparison.Ordinal))
            .Select(n => n!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.Contains(MigrationId, ids);
        // EF applique les migrations par ordre lexicographique de l'id : la nôtre doit rester la dernière
        // de sa série (une renumérotation silencieuse casserait les bases déjà migrées).
        Assert.Equal(MigrationId, ids.Last());
    }

    [Fact]
    public void Idempotent_script_matches_the_migration_and_registers_the_history_row()
    {
        var scriptPath = RepoRelativePath(
            "docs", "runbooks", "sql", "AddStudioEntityKind_Tenant.idempotent.sql");

        Assert.True(File.Exists(scriptPath), $"Script SQL not found: {scriptPath}");

        var script = File.ReadAllText(scriptPath);

        Assert.Contains("IF COL_LENGTH('CustomEntityDefinitions', 'Kind') IS NULL", script);
        Assert.Contains("ADD [Kind] int NOT NULL CONSTRAINT [DF_CustomEntityDefinitions_Kind] DEFAULT (0)", script);
        Assert.Contains("IX_CustomEntityDefinitions_TenantId_Kind", script);
        Assert.Contains($"WHERE [MigrationId] = N'{MigrationId}'", script);
        Assert.Contains($"VALUES (N'{MigrationId}', N'8.0.1')", script);
    }

    [Fact]
    public void Model_snapshot_declares_kind_with_default_zero_and_the_tenant_kind_index()
    {
        var snapshotPath = RepoRelativePath(
            "src", "Backend", "FactuTrust.Infrastructure", "Migrations", "Tenant", "TenantDbContextModelSnapshot.cs");

        Assert.True(File.Exists(snapshotPath), $"Snapshot not found: {snapshotPath}");

        var snapshot = File.ReadAllText(snapshotPath);
        var start = snapshot.IndexOf("modelBuilder.Entity(\"FactuTrust.Domain.Entities.Studio.CustomEntityDefinition\"", StringComparison.Ordinal);
        var end = snapshot.IndexOf("modelBuilder.Entity(\"FactuTrust.Domain.Entities.Studio.CustomFieldDefinition\"", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "CustomEntityDefinition block not found in the snapshot.");

        var block = snapshot[start..end];
        Assert.Contains("b.Property<int>(\"Kind\")", block);
        Assert.Contains(".HasDefaultValue(0)", block);
        Assert.Contains("b.HasIndex(\"TenantId\", \"Kind\")", block);
    }

    [Fact]
    public void Runtime_model_maps_kind_as_int_with_default_standard()
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseInMemoryDatabase($"kind-model-{Guid.NewGuid():N}")
            .Options;
        using var context = new TenantDbContext(options);

        var entityType = context.Model.FindEntityType(typeof(CustomEntityDefinition));
        Assert.NotNull(entityType);

        var kind = entityType!.FindProperty(nameof(CustomEntityDefinition.Kind));
        Assert.NotNull(kind);
        Assert.Equal(CustomEntityKind.Standard, kind!.GetDefaultValue());
        Assert.Equal(typeof(int), kind.GetProviderClrType()); // HasConversion<int>() : stocké en int, pas en chaîne

        var tenantId = entityType.FindProperty(nameof(CustomEntityDefinition.TenantId))!;
        Assert.Contains(entityType.GetIndexes(), ix =>
            ix.Properties.Count == 2 && ix.Properties[0] == tenantId && ix.Properties[1] == kind);
    }
}
