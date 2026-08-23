using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations;

/// <summary>
/// Backfills <c>stock_vouchers</c> into existing Stock module feature-key lists
/// that already include <c>stock_transfers</c>.
/// </summary>
[DbContext(typeof(MasterDbContext))]
[Migration("20260819120100_BackfillStockVouchersFeatureKeys_Master")]
public partial class BackfillStockVouchersFeatureKeys_Master : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // AppModule.Stock = 7
        migrationBuilder.Sql("""
            UPDATE [UserModuleGrants]
            SET [EnabledFeatureKeys] = REPLACE(
                [EnabledFeatureKeys],
                '"stock_transfers"',
                '"stock_transfers","stock_vouchers"')
            WHERE [Module] = 7
              AND [IsEnabled] = 1
              AND [EnabledFeatureKeys] IS NOT NULL
              AND [EnabledFeatureKeys] LIKE '%"stock_transfers"%'
              AND [EnabledFeatureKeys] NOT LIKE '%"stock_vouchers"%';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE [UserModuleGrants]
            SET [EnabledFeatureKeys] = REPLACE(
                REPLACE(
                    [EnabledFeatureKeys],
                    ',"stock_vouchers"',
                    ''),
                '"stock_vouchers",',
                '')
            WHERE [Module] = 7
              AND [EnabledFeatureKeys] IS NOT NULL
              AND [EnabledFeatureKeys] LIKE '%"stock_vouchers"%';
            """);
    }
}
