using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Custom invoice/quote lines (forfait projet, wizard, devis libre) have no catalog product.
    /// ProductId must be nullable so INSERT does not violate FK_InvoiceLines_Products_ProductId.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260821120000_MakeInvoiceAndQuoteLineProductIdNullable_Tenant")]
    public partial class MakeInvoiceAndQuoteLineProductIdNullable_Tenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceLines_Products_ProductId",
                table: "InvoiceLines");

            migrationBuilder.DropForeignKey(
                name: "FK_QuoteLines_Products_ProductId",
                table: "QuoteLines");

            migrationBuilder.Sql("""
                UPDATE InvoiceLines SET ProductId = NULL WHERE ProductId = '00000000-0000-0000-0000-000000000000';
                UPDATE QuoteLines SET ProductId = NULL WHERE ProductId = '00000000-0000-0000-0000-000000000000';
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "ProductId",
                table: "InvoiceLines",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "ProductId",
                table: "QuoteLines",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceLines_Products_ProductId",
                table: "InvoiceLines",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuoteLines_Products_ProductId",
                table: "QuoteLines",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <summary>
        /// Down fails if any line has ProductId NULL — restore only on databases without custom lines.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceLines_Products_ProductId",
                table: "InvoiceLines");

            migrationBuilder.DropForeignKey(
                name: "FK_QuoteLines_Products_ProductId",
                table: "QuoteLines");

            migrationBuilder.Sql("""
                UPDATE InvoiceLines SET ProductId = '00000000-0000-0000-0000-000000000000' WHERE ProductId IS NULL;
                UPDATE QuoteLines SET ProductId = '00000000-0000-0000-0000-000000000000' WHERE ProductId IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "ProductId",
                table: "InvoiceLines",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ProductId",
                table: "QuoteLines",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceLines_Products_ProductId",
                table: "InvoiceLines",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuoteLines_Products_ProductId",
                table: "QuoteLines",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
