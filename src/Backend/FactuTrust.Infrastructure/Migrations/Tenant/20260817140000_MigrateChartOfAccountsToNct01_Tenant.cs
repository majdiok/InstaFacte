using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

/// <summary>
/// Table d'idempotence du remap PCG hybride → NCT 01. Le remap des numéros et l'UPSERT du
/// catalogue sont appliqués au boot tenant par <c>Nct01ChartMigrationService</c> (JSON embarqué),
/// pas dans cette migration SQL : les tenants déjà sur 20260326 gardent leur seed historique.
/// </summary>
[DbContext(typeof(TenantDbContext))]
[Migration("20260817140000_MigrateChartOfAccountsToNct01_Tenant")]
public partial class MigrateChartOfAccountsToNct01_Tenant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.ChartOfAccountRemapLogs', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ChartOfAccountRemapLogs] (
        [Id] uniqueidentifier NOT NULL PRIMARY KEY,
        [MapVersion] nvarchar(32) NOT NULL,
        [AppliedAt] datetime2 NOT NULL,
        [AccountCount] int NOT NULL
    );
    CREATE UNIQUE INDEX [IX_ChartOfAccountRemapLogs_MapVersion]
        ON [dbo].[ChartOfAccountRemapLogs] ([MapVersion]);
END
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.ChartOfAccountRemapLogs', N'U') IS NOT NULL
    DROP TABLE [dbo].[ChartOfAccountRemapLogs];
""");
    }
}
