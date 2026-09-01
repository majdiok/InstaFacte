using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations;

/// <summary>
/// Plan §2.1 — adds <c>Tenants.SectorCatalogVersion</c> (nullable, hand-written idempotent SQL,
/// following the <c>AddTenantSectorClassification_Master</c> / <c>AddTenantProvisioningStatusAndNifUniqueIndex_Master</c>
/// pattern: no Designer file). Records the sector-catalog version tag (e.g. "static:0", "db:12")
/// active when <c>CompanySegment</c>/<c>BusinessDomain</c> were last resolved — informational only,
/// never gates behavior.
/// </summary>
[DbContext(typeof(MasterDbContext))]
[Migration("20260901160000_AddTenantSectorCatalogVersion_Master")]
public partial class AddTenantSectorCatalogVersion_Master : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.Tenants', N'SectorCatalogVersion') IS NULL
BEGIN
    ALTER TABLE [Tenants] ADD [SectorCatalogVersion] nvarchar(50) NULL;
END
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.Tenants', N'SectorCatalogVersion') IS NOT NULL
BEGIN
    ALTER TABLE [Tenants] DROP COLUMN [SectorCatalogVersion];
END
""");
    }
}
