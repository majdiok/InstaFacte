using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations;

/// <summary>
/// Plan §2.2 — adds the <c>ModuleGrantAuditEntries</c> master table (who/when/diff trail for
/// module-grant changes made via <c>CompanyModulesController</c>). Hand-written idempotent SQL,
/// following the <c>AddSectorRuleTables_Master</c> pattern (no Designer file).
/// </summary>
[DbContext(typeof(MasterDbContext))]
[Migration("20260901160500_AddModuleGrantAuditEntries_Master")]
public partial class AddModuleGrantAuditEntries_Master : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.ModuleGrantAuditEntries', N'U') IS NULL
BEGIN
    CREATE TABLE [ModuleGrantAuditEntries] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ActorUserId] uniqueidentifier NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [Action] nvarchar(100) NOT NULL,
        [DiffJson] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_ModuleGrantAuditEntries] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_ModuleGrantAuditEntries_TenantId_CreatedAtUtc] ON [ModuleGrantAuditEntries] ([TenantId], [CreatedAtUtc]);
END
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.ModuleGrantAuditEntries', N'U') IS NOT NULL
BEGIN
    DROP TABLE [ModuleGrantAuditEntries];
END
""");
    }
}
