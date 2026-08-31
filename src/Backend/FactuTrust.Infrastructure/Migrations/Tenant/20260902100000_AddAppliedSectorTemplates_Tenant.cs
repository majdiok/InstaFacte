using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

/// <summary>
/// Suivi des modèles de données sectoriels appliqués (plan « Règles sectorielles en base »,
/// §WP-B6). Migration strictement ADDITIVE : une seule nouvelle table, aucune colonne ni table
/// existante n'est modifiée ou supprimée. Zéro risque pour les tenants existants.
/// </summary>
[DbContext(typeof(TenantDbContext))]
[Migration("20260902100000_AddAppliedSectorTemplates_Tenant")]
public partial class AddAppliedSectorTemplates_Tenant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[AppliedSectorTemplates]', N'U') IS NULL
BEGIN
    CREATE TABLE [AppliedSectorTemplates] (
        [Id] uniqueidentifier NOT NULL,
        [TemplateCode] nvarchar(50) NOT NULL,
        [Version] int NOT NULL,
        [AppliedAtUtc] datetime2 NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_AppliedSectorTemplates] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_AppliedSectorTemplates_TemplateCode_Version] ON [AppliedSectorTemplates] ([TemplateCode], [Version]);
END
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[AppliedSectorTemplates]', N'U') IS NOT NULL DROP TABLE [AppliedSectorTemplates];
""");
    }
}
