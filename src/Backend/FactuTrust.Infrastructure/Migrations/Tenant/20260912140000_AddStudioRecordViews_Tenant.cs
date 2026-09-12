using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Studio IA — PR 2.3 (vues enregistrées) : crée la table <c>CustomRecordViewDefinitions</c>
    /// (vues Liste / Kanban / Calendrier d'une table Studio : clé, libellé, <c>Mode</c> int défaut 0 = List,
    /// <c>DefinitionJson</c>, <c>IsDefault</c>, suppression logique), sa clé étrangère en cascade vers
    /// <c>CustomEntityDefinitions</c>, l'index unique filtré <c>(TenantId, EntityDefinitionId, Key) WHERE IsDeleted = 0</c>
    /// et l'index <c>(TenantId, EntityDefinitionId, IsDefault)</c>. Écrite à la main (pas de
    /// <c>dotnet ef migrations add</c>), idempotente, jumeau SQL :
    /// <c>docs/runbooks/sql/AddStudioRecordViews_Tenant.idempotent.sql</c>.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260912140000_AddStudioRecordViews_Tenant")]
    public partial class AddStudioRecordViews_Tenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[CustomRecordViewDefinitions]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CustomRecordViewDefinitions] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [EntityDefinitionId] uniqueidentifier NOT NULL,
        [Key] nvarchar(64) NOT NULL,
        [DisplayName] nvarchar(128) NOT NULL,
        [Mode] int NOT NULL CONSTRAINT [DF_CustomRecordViewDefinitions_Mode] DEFAULT (0),
        [DefinitionJson] nvarchar(max) NOT NULL,
        [IsDefault] bit NOT NULL CONSTRAINT [DF_CustomRecordViewDefinitions_IsDefault] DEFAULT (0),
        [IsActive] bit NOT NULL CONSTRAINT [DF_CustomRecordViewDefinitions_IsActive] DEFAULT (1),
        [IsDeleted] bit NOT NULL CONSTRAINT [DF_CustomRecordViewDefinitions_IsDeleted] DEFAULT (0),
        [DeletedAt] datetime2 NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CustomRecordViewDefinitions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CustomRecordViewDefinitions_CustomEntityDefinitions] FOREIGN KEY ([EntityDefinitionId])
            REFERENCES [dbo].[CustomEntityDefinitions] ([Id]) ON DELETE CASCADE
    );
END;
""");

            migrationBuilder.Sql("""
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'UX_CustomRecordViewDefinitions_Tenant_Entity_Key' AND [object_id] = OBJECT_ID(N'[dbo].[CustomRecordViewDefinitions]'))
    CREATE UNIQUE INDEX [UX_CustomRecordViewDefinitions_Tenant_Entity_Key]
        ON [dbo].[CustomRecordViewDefinitions] ([TenantId], [EntityDefinitionId], [Key])
        WHERE [IsDeleted] = 0;
""");

            migrationBuilder.Sql("""
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_CustomRecordViewDefinitions_Tenant_Entity_Default' AND [object_id] = OBJECT_ID(N'[dbo].[CustomRecordViewDefinitions]'))
    CREATE INDEX [IX_CustomRecordViewDefinitions_Tenant_Entity_Default]
        ON [dbo].[CustomRecordViewDefinitions] ([TenantId], [EntityDefinitionId], [IsDefault]);
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Les index tombent avec la table ; gardé pour qu'un rollback rejoué ne casse pas.
            migrationBuilder.Sql("DROP TABLE IF EXISTS [dbo].[CustomRecordViewDefinitions];");
        }
    }
}
