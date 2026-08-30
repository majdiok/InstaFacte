using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

/// <summary>
/// Retire PriceListId des contrats récurrents : la grille tarifaire n'est plus
/// saisie ni stockée sur le contrat (create / edit / avenant). Les grilles
/// Pricing (clients / produits) restent inchangées.
/// Migration manuelle idempotente (style module récurrents).
/// </summary>
[DbContext(typeof(TenantDbContext))]
[Migration("20260830180000_DropRecurringContractPriceListId_Tenant")]
public partial class DropRecurringContractPriceListId_Tenant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.RecurringContracts', N'PriceListId') IS NOT NULL
    ALTER TABLE [dbo].[RecurringContracts] DROP COLUMN [PriceListId];
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[RecurringContracts]', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.RecurringContracts', N'PriceListId') IS NULL
    ALTER TABLE [dbo].[RecurringContracts] ADD [PriceListId] uniqueidentifier NULL;
""");
    }
}
