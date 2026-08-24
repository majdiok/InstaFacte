using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations;

[DbContext(typeof(MasterDbContext))]
[Migration("20260824120000_AddPortalClientId_Master")]
public partial class AddPortalClientId_Master : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.Users', N'PortalClientId') IS NULL
    ALTER TABLE [Users] ADD [PortalClientId] uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.Users', N'PortalInviteTokenHash') IS NULL
    ALTER TABLE [Users] ADD [PortalInviteTokenHash] nvarchar(64) NULL;
IF COL_LENGTH(N'dbo.Users', N'PortalInviteExpiresAt') IS NULL
    ALTER TABLE [Users] ADD [PortalInviteExpiresAt] datetime2 NULL;
""");

        migrationBuilder.Sql("""
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Users_TenantId_PortalClientId' AND object_id = OBJECT_ID(N'dbo.Users'))
    CREATE INDEX [IX_Users_TenantId_PortalClientId] ON [Users] ([TenantId], [PortalClientId]);
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Users_TenantId_PortalClientId' AND object_id = OBJECT_ID(N'dbo.Users'))
    DROP INDEX [IX_Users_TenantId_PortalClientId] ON [Users];
IF COL_LENGTH(N'dbo.Users', N'PortalInviteExpiresAt') IS NOT NULL
    ALTER TABLE [Users] DROP COLUMN [PortalInviteExpiresAt];
IF COL_LENGTH(N'dbo.Users', N'PortalInviteTokenHash') IS NOT NULL
    ALTER TABLE [Users] DROP COLUMN [PortalInviteTokenHash];
IF COL_LENGTH(N'dbo.Users', N'PortalClientId') IS NOT NULL
    ALTER TABLE [Users] DROP COLUMN [PortalClientId];
""");
    }
}
