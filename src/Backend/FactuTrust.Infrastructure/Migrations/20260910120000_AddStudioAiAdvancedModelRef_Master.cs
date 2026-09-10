using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations;

/// <summary>
/// Studio IA — PR 1.1 : ajoute <c>PlatformAiSettings.StudioAiAdvancedModelRef</c> (nvarchar(500),
/// NULLABLE), le modèle Studio « avancé » (GPU distant ou cloud) sélectionné dans le back-office
/// plateforme et utilisé quand l'utilisateur active « Modèle avancé » dans le Studio.
///
/// Purement additive : NULL signifie « aucun modèle avancé configuré » ⇒ la capacité
/// <c>AdvancedModelAvailable</c> reste false et la chaîne de résolution du modèle Studio est
/// strictement inchangée. SQL idempotent écrit à la main, sans fichier Designer (même patron que
/// <c>AddTenantRegistrationProfile_Master</c>).
///
/// Jumeau SQL applicable hors EF : <c>docs/runbooks/sql/AddStudioAiAdvancedModelRef_Master.idempotent.sql</c>.
/// </summary>
[DbContext(typeof(MasterDbContext))]
[Migration("20260910120000_AddStudioAiAdvancedModelRef_Master")]
public partial class AddStudioAiAdvancedModelRef_Master : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.PlatformAiSettings', N'StudioAiAdvancedModelRef') IS NULL
BEGIN
    ALTER TABLE [PlatformAiSettings] ADD [StudioAiAdvancedModelRef] nvarchar(500) NULL;
END
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.PlatformAiSettings', N'StudioAiAdvancedModelRef') IS NOT NULL
BEGIN
    ALTER TABLE [PlatformAiSettings] DROP COLUMN [StudioAiAdvancedModelRef];
END
""");
    }
}
