using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

[DbContext(typeof(TenantDbContext))]
[Migration("20260407000000_AddInvoiceFiscalStamp_Tenant")]
public partial class AddInvoiceFiscalStamp_Tenant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "FiscalStampAmount",
            table: "Invoices",
            type: "decimal(18,3)",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<string>(
            name: "FiscalStampCurrency",
            table: "Invoices",
            type: "nvarchar(3)",
            maxLength: 3,
            nullable: false,
            defaultValue: "TND");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "FiscalStampAmount",
            table: "Invoices");

        migrationBuilder.DropColumn(
            name: "FiscalStampCurrency",
            table: "Invoices");
    }
}
