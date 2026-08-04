using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations
{
    /// <summary>
    /// Backfills <c>purchase_receipts</c> into existing Purchases module feature-key lists
    /// that already include <c>purchase_orders</c> (pre-dating the receipt sub-feature).
    /// Null EnabledFeatureKeys (all features) need no change.
    /// </summary>
    [DbContext(typeof(MasterDbContext))]
    [Migration("20260802020000_BackfillPurchaseReceiptsFeatureKeys_Master")]
    public partial class BackfillPurchaseReceiptsFeatureKeys_Master : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // AppModule.Purchases = 6
            migrationBuilder.Sql("""
                UPDATE [UserModuleGrants]
                SET [EnabledFeatureKeys] = REPLACE(
                    [EnabledFeatureKeys],
                    '"purchase_orders"',
                    '"purchase_orders","purchase_receipts"')
                WHERE [Module] = 6
                  AND [IsEnabled] = 1
                  AND [EnabledFeatureKeys] IS NOT NULL
                  AND [EnabledFeatureKeys] LIKE '%"purchase_orders"%'
                  AND [EnabledFeatureKeys] NOT LIKE '%"purchase_receipts"%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE [UserModuleGrants]
                SET [EnabledFeatureKeys] = REPLACE(
                    REPLACE(
                        [EnabledFeatureKeys],
                        ',"purchase_receipts"',
                        ''),
                    '"purchase_receipts",',
                    '')
                WHERE [Module] = 6
                  AND [EnabledFeatureKeys] IS NOT NULL
                  AND [EnabledFeatureKeys] LIKE '%"purchase_receipts"%'
                  AND [EnabledFeatureKeys] LIKE '%"purchase_orders"%';
                """);
        }
    }
}
