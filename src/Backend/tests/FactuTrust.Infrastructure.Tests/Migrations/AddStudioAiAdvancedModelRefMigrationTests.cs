using Xunit;

namespace FactuTrust.Infrastructure.Tests.Migrations;

/// <summary>
/// Studio IA — PR 1.1 : verrouille le contrat de la migration Master ajoutant
/// <c>PlatformAiSettings.StudioAiAdvancedModelRef</c>. Les bases plateforme sont parfois migrées
/// à la main (script SQL) : la migration EF et son jumeau SQL doivent rester idempotents et
/// cohérents (même MigrationId, même type de colonne), sinon un rejeu casse le déploiement.
/// Assertions sur le TEXTE SOURCE : aucun serveur SQL requis.
/// </summary>
public sealed class AddStudioAiAdvancedModelRefMigrationTests
{
    private const string MigrationId = "20260910120000_AddStudioAiAdvancedModelRef_Master";

    /// <summary>Chemin absolu depuis la racine du dépôt (bin/Release/net8.0 ⇒ 7 niveaux au-dessus).</summary>
    private static string RepoRelativePath(params string[] segments) => Path.GetFullPath(Path.Combine(
        new[] { AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", ".." }
            .Concat(segments)
            .ToArray()));

    [Fact]
    public void Migration_adds_a_nullable_column_behind_an_existence_guard()
    {
        var sourcePath = RepoRelativePath(
            "src", "Backend", "FactuTrust.Infrastructure", "Migrations", $"{MigrationId}.cs");

        Assert.True(File.Exists(sourcePath), $"Migration source not found: {sourcePath}");

        var source = File.ReadAllText(sourcePath);

        Assert.Contains($"[Migration(\"{MigrationId}\")]", source);
        Assert.Contains("IF COL_LENGTH(N'dbo.PlatformAiSettings', N'StudioAiAdvancedModelRef') IS NULL", source);
        Assert.Contains("ADD [StudioAiAdvancedModelRef] nvarchar(500) NULL", source);
        // Down symétrique et lui aussi gardé : un rollback rejoué ne doit pas échouer.
        Assert.Contains("IF COL_LENGTH(N'dbo.PlatformAiSettings', N'StudioAiAdvancedModelRef') IS NOT NULL", source);
        Assert.Contains("DROP COLUMN [StudioAiAdvancedModelRef]", source);
    }

    [Fact]
    public void Idempotent_script_matches_the_migration_and_registers_the_history_row()
    {
        var scriptPath = RepoRelativePath(
            "docs", "runbooks", "sql", "AddStudioAiAdvancedModelRef_Master.idempotent.sql");

        Assert.True(File.Exists(scriptPath), $"Script SQL not found: {scriptPath}");

        var script = File.ReadAllText(scriptPath);

        Assert.Contains("IF COL_LENGTH('PlatformAiSettings', 'StudioAiAdvancedModelRef') IS NULL", script);
        Assert.Contains("ADD [StudioAiAdvancedModelRef] nvarchar(500) NULL", script);
        Assert.Contains($"WHERE [MigrationId] = N'{MigrationId}'", script);
        Assert.Contains($"VALUES (N'{MigrationId}', N'8.0.1')", script);
    }
}
