using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

[DbContext(typeof(TenantDbContext))]
[Migration("20260403140000_AddInvoiceIssuerAndElectronicInvoice_Tenant")]
public partial class AddInvoiceIssuerAndElectronicInvoice_Tenant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "ElectronicInvoiceSentAt",
            table: "Invoices",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ElectronicInvoiceTtn",
            table: "Invoices",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "IssuerCompanyId",
            table: "Invoices",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_IssuerCompanyId",
            table: "Invoices",
            column: "IssuerCompanyId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Invoices_IssuerCompanyId",
            table: "Invoices");

        migrationBuilder.DropColumn(
            name: "ElectronicInvoiceSentAt",
            table: "Invoices");

        migrationBuilder.DropColumn(
            name: "ElectronicInvoiceTtn",
            table: "Invoices");

        migrationBuilder.DropColumn(
            name: "IssuerCompanyId",
            table: "Invoices");
    }
}
