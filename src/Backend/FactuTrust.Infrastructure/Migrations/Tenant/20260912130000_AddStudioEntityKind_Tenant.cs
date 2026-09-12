using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Studio IA — PR 2.1 (relations plusieurs‑à‑plusieurs) : ajoute la colonne
    /// <c>CustomEntityDefinitions.Kind</c> (int, NOT NULL, défaut 0 = Standard ; 1 = Junction) et
    /// l'index <c>(TenantId, Kind)</c> qui sert la navigation (exclusion des jonctions) et la
    /// recherche des relations. Écrite à la main (pas de <c>dotnet ef migrations add</c>), idempotente,
    /// jumeau SQL : <c>docs/runbooks/sql/AddStudioEntityKind_Tenant.idempotent.sql</c>.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260912130000_AddStudioEntityKind_Tenant")]
    public partial class AddStudioEntityKind_Tenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.CustomEntityDefinitions', N'Kind') IS NULL
    ALTER TABLE [CustomEntityDefinitions] ADD [Kind] int NOT NULL CONSTRAINT [DF_CustomEntityDefinitions_Kind] DEFAULT (0);
""");

            migrationBuilder.Sql("""
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_CustomEntityDefinitions_TenantId_Kind' AND [object_id] = OBJECT_ID(N'dbo.CustomEntityDefinitions'))
    CREATE INDEX [IX_CustomEntityDefinitions_TenantId_Kind] ON [CustomEntityDefinitions] ([TenantId], [Kind]);
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_CustomEntityDefinitions_TenantId_Kind' AND [object_id] = OBJECT_ID(N'dbo.CustomEntityDefinitions'))
    DROP INDEX [IX_CustomEntityDefinitions_TenantId_Kind] ON [CustomEntityDefinitions];
IF COL_LENGTH(N'dbo.CustomEntityDefinitions', N'Kind') IS NOT NULL
    ALTER TABLE [CustomEntityDefinitions] DROP CONSTRAINT [DF_CustomEntityDefinitions_Kind];
IF COL_LENGTH(N'dbo.CustomEntityDefinitions', N'Kind') IS NOT NULL
    ALTER TABLE [CustomEntityDefinitions] DROP COLUMN [Kind];
""");
        }
    }
}
