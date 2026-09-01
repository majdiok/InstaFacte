using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations;

/// <summary>
/// Provisioning mini-saga + NIF uniqueness (plan §1.4/§1.5).
/// <list type="bullet">
///   <item><c>Tenants.ProvisioningStatus</c> (int NOT NULL DEFAULT 0 = Ready): additive column so
///   every pre-existing row reads as already-provisioned; only <c>AuthController.Register</c>'s
///   self-registration flow explicitly inserts <c>Pending</c> (1) and transitions it to
///   <c>Ready</c>/<c>Failed</c> after the tenant database is provisioned.</item>
///   <item><c>IX_Tenants_NIF</c>: filtered unique index on the owned <c>NIF.Value</c> column,
///   scoped to active tenants (<c>WHERE [NIF] IS NOT NULL AND [IsActive] = 1</c>) — mirrors the
///   application-level rule <c>FirmManagedClientService</c> already enforces, so a deactivated
///   tenant's NIF remains reusable.</item>
/// </list>
/// Hand-written idempotent SQL following the <c>AddTenantSectorClassification_Master</c> pattern
/// (no Designer file; guarded so re-running is a no-op).
/// <para>
/// Caveat: this cannot be verified against production data from this sandbox (no SQL Server
/// available here) — if duplicate active NIFs already exist, this migration's index creation will
/// fail and those duplicates must be resolved (deactivate/correct one of them) before it can apply.
/// </para>
/// </summary>
[DbContext(typeof(MasterDbContext))]
[Migration("20260901151338_AddTenantProvisioningStatusAndNifUniqueIndex_Master")]
public partial class AddTenantProvisioningStatusAndNifUniqueIndex_Master : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.Tenants', N'ProvisioningStatus') IS NULL
    ALTER TABLE [Tenants] ADD [ProvisioningStatus] int NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Tenants_NIF' AND object_id = OBJECT_ID(N'dbo.Tenants'))
    CREATE UNIQUE INDEX [IX_Tenants_NIF] ON [Tenants] ([NIF]) WHERE [NIF] IS NOT NULL AND [IsActive] = 1;
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Tenants_NIF' AND object_id = OBJECT_ID(N'dbo.Tenants'))
    DROP INDEX [IX_Tenants_NIF] ON [Tenants];

IF COL_LENGTH(N'dbo.Tenants', N'ProvisioningStatus') IS NOT NULL
    ALTER TABLE [Tenants] DROP COLUMN [ProvisioningStatus];
""");
    }
}
