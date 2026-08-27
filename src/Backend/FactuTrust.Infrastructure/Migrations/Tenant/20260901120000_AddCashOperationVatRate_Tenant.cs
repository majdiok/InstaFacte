using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

/// <summary>
/// Ajoute le taux de TVA optionnel (nullable) sur les opérations de caisse « ventes au comptant »
/// (table CashExpenses). Migration manuelle, additive et idempotente — snapshot EF volontairement
/// non modifié (désynchronisé depuis 2026-08-01, cf. docs/backend-tenant-migrations.md).
/// </summary>
[DbContext(typeof(TenantDbContext))]
[Migration("20260901120000_AddCashOperationVatRate_Tenant")]
public partial class AddCashOperationVatRate_Tenant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[CashExpenses]', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.CashExpenses', N'VatRate') IS NULL
BEGIN
    ALTER TABLE [dbo].[CashExpenses] ADD [VatRate] int NULL;
END
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[CashExpenses]', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.CashExpenses', N'VatRate') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[CashExpenses] DROP COLUMN [VatRate];
END
""");
    }
}
