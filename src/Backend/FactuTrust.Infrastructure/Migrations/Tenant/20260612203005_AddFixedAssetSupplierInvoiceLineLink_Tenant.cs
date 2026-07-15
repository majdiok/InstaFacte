using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddFixedAssetSupplierInvoiceLineLink_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SupplierInvoiceLineId",
                table: "FixedAssets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_SupplierInvoiceId",
                table: "FixedAssets",
                column: "SupplierInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_SupplierInvoiceLineId",
                table: "FixedAssets",
                column: "SupplierInvoiceLineId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FixedAssets_SupplierInvoiceId",
                table: "FixedAssets");

            migrationBuilder.DropIndex(
                name: "IX_FixedAssets_SupplierInvoiceLineId",
                table: "FixedAssets");

            migrationBuilder.DropColumn(
                name: "SupplierInvoiceLineId",
                table: "FixedAssets");
        }
    }
}
