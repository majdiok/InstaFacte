using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations;

/// <summary>
/// Review R2 — adds <c>SectorRuleSetStamps.CatalogContentHash</c> (nullable, hand-written idempotent
/// SQL, following the <c>AddSectorRuleTables_Master</c> pattern: no Designer file). Lets
/// <c>SectorRuleSeeder.ReconcileOnStartupAsync</c> detect a catalog content change across app
/// restarts and re-run a full <c>force</c> seed only when the catalog actually drifted from what
/// was last seeded — rather than either never re-syncing (the old <c>SeedIfEmptyAsync</c> no-op)
/// or force-reseeding unconditionally on every boot.
/// </summary>
[DbContext(typeof(MasterDbContext))]
[Migration("20260901110000_AddSectorRuleSetStampCatalogHash_Master")]
public partial class AddSectorRuleSetStampCatalogHash_Master : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.SectorRuleSetStamps', N'U') IS NOT NULL
    AND NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.SectorRuleSetStamps') AND name = 'CatalogContentHash')
BEGIN
    ALTER TABLE [SectorRuleSetStamps] ADD [CatalogContentHash] nvarchar(64) NULL;
END
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.SectorRuleSetStamps', N'U') IS NOT NULL
    AND EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.SectorRuleSetStamps') AND name = 'CatalogContentHash')
BEGIN
    ALTER TABLE [SectorRuleSetStamps] DROP COLUMN [CatalogContentHash];
END
""");
    }
}
