using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

[DbContext(typeof(TenantDbContext))]
[Migration("20260824120000_AddClientPortal_Tenant")]
public partial class AddClientPortal_Tenant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.Companies', N'ClientPortalEnabled') IS NULL
    ALTER TABLE [Companies] ADD [ClientPortalEnabled] bit NOT NULL CONSTRAINT [DF_Companies_ClientPortalEnabled] DEFAULT 1;
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.ClientPortalContacts', N'U') IS NULL
BEGIN
    CREATE TABLE [ClientPortalContacts] (
        [Id] uniqueidentifier NOT NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [Status] int NOT NULL,
        [InvitedAt] datetime2 NOT NULL,
        [AcceptedAt] datetime2 NULL,
        [RevokedAt] datetime2 NULL,
        [LastAccessAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(256) NULL,
        [UpdatedBy] nvarchar(256) NULL,
        CONSTRAINT [PK_ClientPortalContacts] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_ClientPortalContacts_ClientId] ON [ClientPortalContacts] ([ClientId]);
    CREATE INDEX [IX_ClientPortalContacts_UserId] ON [ClientPortalContacts] ([UserId]);
    CREATE UNIQUE INDEX [IX_ClientPortalContacts_ClientId_Email_Active]
        ON [ClientPortalContacts] ([ClientId], [Email]) WHERE [Status] <> 2;
    CREATE UNIQUE INDEX [IX_ClientPortalContacts_UserId_Active]
        ON [ClientPortalContacts] ([UserId]) WHERE [Status] <> 2;
END
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS [ClientPortalContacts];");
        migrationBuilder.Sql("""
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_Companies_ClientPortalEnabled')
    ALTER TABLE [Companies] DROP CONSTRAINT [DF_Companies_ClientPortalEnabled];
IF COL_LENGTH(N'dbo.Companies', N'ClientPortalEnabled') IS NOT NULL
    ALTER TABLE [Companies] DROP COLUMN [ClientPortalEnabled];
""");
    }
}
