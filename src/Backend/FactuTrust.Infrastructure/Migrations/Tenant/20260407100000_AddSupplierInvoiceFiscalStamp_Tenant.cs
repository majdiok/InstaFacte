using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

[DbContext(typeof(TenantDbContext))]
[Migration("20260407100000_AddSupplierInvoiceFiscalStamp_Tenant")]
public partial class AddSupplierInvoiceFiscalStamp_Tenant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "FiscalStampAmount",
            table: "SupplierInvoices",
            type: "decimal(18,3)",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<string>(
            name: "FiscalStampCurrency",
            table: "SupplierInvoices",
            type: "nvarchar(3)",
            maxLength: 3,
            nullable: false,
            defaultValue: "TND");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "FiscalStampAmount",
            table: "SupplierInvoices");

        migrationBuilder.DropColumn(
            name: "FiscalStampCurrency",
            table: "SupplierInvoices");
    }
}
