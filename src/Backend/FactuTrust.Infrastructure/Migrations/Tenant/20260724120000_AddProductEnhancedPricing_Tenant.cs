using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260724120000_AddProductEnhancedPricing_Tenant")]
    public partial class AddProductEnhancedPricing_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDiscountEnabled",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "LastPurchasePrice",
                table: "Products",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastPurchasePriceCurrency",
                table: "Products",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxDiscountPercent",
                table: "Products",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProfitMarginPercent",
                table: "Products",
                type: "decimal(7,3)",
                precision: 7,
                scale: 3,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE Products
                SET ProfitMarginPercent = ROUND(((UnitPrice - PurchasePrice) / NULLIF(PurchasePrice, 0)) * 100, 3)
                WHERE PurchasePrice IS NOT NULL AND PurchasePrice > 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDiscountEnabled",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "LastPurchasePrice",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "LastPurchasePriceCurrency",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MaxDiscountPercent",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ProfitMarginPercent",
                table: "Products");
        }
    }
}
