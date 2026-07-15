using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddInvoiceFodec_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FodecAmount",
                table: "Invoices",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "FodecAmountCurrency",
                table: "Invoices",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "FodecAmount",
                table: "InvoiceLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "FodecAmountCurrency",
                table: "InvoiceLines",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsFodecApplicable",
                table: "InvoiceLines",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FodecAmount",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "FodecAmountCurrency",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "FodecAmount",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "FodecAmountCurrency",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "IsFodecApplicable",
                table: "InvoiceLines");
        }
    }
}
