using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddSupplierInvoiceLineFixedAssetFields_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssetAccountNumber",
                table: "SupplierInvoiceLines",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DepreciationRateCategoryId",
                table: "SupplierInvoiceLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFixedAsset",
                table: "SupplierInvoiceLines",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvoiceLines_IsFixedAsset",
                table: "SupplierInvoiceLines",
                column: "IsFixedAsset");

            migrationBuilder.CreateIndex(
                name: "IX_DepreciationScheduleLines_FixedAssetId_FiscalYear",
                table: "DepreciationScheduleLines",
                columns: new[] { "FixedAssetId", "FiscalYear" },
                unique: true,
                filter: "[PeriodMonth] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SupplierInvoiceLines_IsFixedAsset",
                table: "SupplierInvoiceLines");

            migrationBuilder.DropIndex(
                name: "IX_DepreciationScheduleLines_FixedAssetId_FiscalYear",
                table: "DepreciationScheduleLines");

            migrationBuilder.DropColumn(
                name: "AssetAccountNumber",
                table: "SupplierInvoiceLines");

            migrationBuilder.DropColumn(
                name: "DepreciationRateCategoryId",
                table: "SupplierInvoiceLines");

            migrationBuilder.DropColumn(
                name: "IsFixedAsset",
                table: "SupplierInvoiceLines");
        }
    }
}
