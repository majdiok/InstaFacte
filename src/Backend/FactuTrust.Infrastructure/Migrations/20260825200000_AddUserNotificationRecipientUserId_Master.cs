using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations;

/// <summary>
/// Ciblage optionnel d'une notification in-app vers un utilisateur (collaborateur affecté).
/// Colonne nullable : les lignes existantes restent visibles via tenant + rôle.
/// </summary>
/// <remarks>
/// Écrite à la main : le <c>MasterDbContextModelSnapshot</c> n'est pas régénéré
/// (même convention que <c>AddExchangeRequestComments_Master</c>).
/// </remarks>
[DbContext(typeof(MasterDbContext))]
[Migration("20260825200000_AddUserNotificationRecipientUserId_Master")]
public partial class AddUserNotificationRecipientUserId_Master : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.UserNotifications', N'RecipientUserId') IS NULL
    ALTER TABLE [UserNotifications] ADD [RecipientUserId] uniqueidentifier NULL;
""");

        migrationBuilder.Sql("""
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_UserNotifications_RecipientTenantId_RecipientUserId_CreatedAt'
      AND object_id = OBJECT_ID(N'dbo.UserNotifications'))
    CREATE INDEX [IX_UserNotifications_RecipientTenantId_RecipientUserId_CreatedAt]
        ON [UserNotifications] ([RecipientTenantId], [RecipientUserId], [CreatedAt] DESC);
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_UserNotifications_RecipientTenantId_RecipientUserId_CreatedAt'
      AND object_id = OBJECT_ID(N'dbo.UserNotifications'))
    DROP INDEX [IX_UserNotifications_RecipientTenantId_RecipientUserId_CreatedAt] ON [UserNotifications];
IF COL_LENGTH(N'dbo.UserNotifications', N'RecipientUserId') IS NOT NULL
    ALTER TABLE [UserNotifications] DROP COLUMN [RecipientUserId];
""");
    }
}
