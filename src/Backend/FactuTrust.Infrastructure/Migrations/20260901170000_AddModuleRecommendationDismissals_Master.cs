using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations;

/// <summary>
/// Plan §3.3 — adds the <c>ModuleRecommendationDismissals</c> master table (per-tenant dismissal of
/// a module usage recommendation; once dismissed, a (tenant, module) pair never resurfaces).
/// Hand-written idempotent SQL, following the <c>AddModuleGrantAuditEntries_Master</c> pattern (no
/// Designer file).
/// </summary>
[DbContext(typeof(MasterDbContext))]
[Migration("20260901170000_AddModuleRecommendationDismissals_Master")]
public partial class AddModuleRecommendationDismissals_Master : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.ModuleRecommendationDismissals', N'U') IS NULL
BEGIN
    CREATE TABLE [ModuleRecommendationDismissals] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Module] int NOT NULL,
        [DismissedAtUtc] datetime2 NOT NULL,
        [DismissedByUserId] uniqueidentifier NULL,
        CONSTRAINT [PK_ModuleRecommendationDismissals] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_ModuleRecommendationDismissals_TenantId_Module] ON [ModuleRecommendationDismissals] ([TenantId], [Module]);
END
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.ModuleRecommendationDismissals', N'U') IS NOT NULL
BEGIN
    DROP TABLE [ModuleRecommendationDismissals];
END
""");
    }
}
