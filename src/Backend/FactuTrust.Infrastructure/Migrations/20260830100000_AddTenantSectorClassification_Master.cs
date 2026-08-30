using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations;

/// <summary>
/// Sector-aware registration wizard (plan §6.1 B2) — additive, nullable columns on
/// <c>Tenants</c> for the captured company segment / business domain. Hand-written idempotent SQL
/// following the <c>AddPortalClientId_Master</c> pattern (no Designer file; guarded by
/// <c>COL_LENGTH</c> so re-running is a no-op).
/// </summary>
[DbContext(typeof(MasterDbContext))]
[Migration("20260830100000_AddTenantSectorClassification_Master")]
public partial class AddTenantSectorClassification_Master : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.Tenants', N'CompanySegment') IS NULL
    ALTER TABLE [Tenants] ADD [CompanySegment] nvarchar(50) NULL;
IF COL_LENGTH(N'dbo.Tenants', N'BusinessDomain') IS NULL
    ALTER TABLE [Tenants] ADD [BusinessDomain] nvarchar(50) NULL;
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.Tenants', N'BusinessDomain') IS NOT NULL
    ALTER TABLE [Tenants] DROP COLUMN [BusinessDomain];
IF COL_LENGTH(N'dbo.Tenants', N'CompanySegment') IS NOT NULL
    ALTER TABLE [Tenants] DROP COLUMN [CompanySegment];
""");
    }
}
