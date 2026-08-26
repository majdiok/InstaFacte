using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

/// <summary>
/// Durcissement de la numérotation des contrats récurrents (D11) : index unique filtré sur
/// RecurringContracts.Number. Migration manuelle idempotente (style « module récurrents » :
/// SQL brut, snapshot EF volontairement non modifié).
/// Garde défensive : si des doublons préexistent, l'index n'est PAS créé (sinon la migration
/// échouerait et bloquerait le tenant) et un avertissement est émis côté serveur SQL — le
/// balayage au démarrage (TenantMigrationHelper) logge alors l'écart dans les traces applicatives.
/// </summary>
[DbContext(typeof(TenantDbContext))]
[Migration("20260827120000_AddRecurringContractNumberUnique_Tenant")]
public partial class AddRecurringContractNumberUnique_Tenant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[RecurringContracts]', N'U') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_RecurringContracts_Number' AND object_id = OBJECT_ID(N'dbo.RecurringContracts'))
AND NOT EXISTS (
    SELECT [Number] FROM [dbo].[RecurringContracts]
    WHERE [Number] IS NOT NULL
    GROUP BY [Number] HAVING COUNT(*) > 1)
BEGIN
    CREATE UNIQUE INDEX [UX_RecurringContracts_Number]
        ON [dbo].[RecurringContracts] ([Number])
        WHERE [Number] IS NOT NULL;
END
""");

        // Avertissement serveur quand la garde anti-doublons saute la création de l'index :
        // l'unicité des numéros n'est alors pas garantie sur ce tenant (message InfoMessage,
        // tracé dans le journal SQL Server ; complété par le warning applicatif au démarrage).
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[RecurringContracts]', N'U') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_RecurringContracts_Number' AND object_id = OBJECT_ID(N'dbo.RecurringContracts'))
AND EXISTS (
    SELECT [Number] FROM [dbo].[RecurringContracts]
    WHERE [Number] IS NOT NULL
    GROUP BY [Number] HAVING COUNT(*) > 1)
BEGIN
    RAISERROR(N'[RecurringContracts] Des numéros de contrat en doublon existent : index UX_RecurringContracts_Number NON créé, unicité non garantie sur ce tenant.', 10, 1) WITH NOWAIT;
END
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_RecurringContracts_Number' AND object_id = OBJECT_ID(N'dbo.RecurringContracts'))
    DROP INDEX [UX_RecurringContracts_Number] ON [dbo].[RecurringContracts];
""");
    }
}
