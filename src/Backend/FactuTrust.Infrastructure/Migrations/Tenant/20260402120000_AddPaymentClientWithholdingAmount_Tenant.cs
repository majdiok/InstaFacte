using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

[DbContext(typeof(TenantDbContext))]
[Migration("20260402120000_AddPaymentClientWithholdingAmount_Tenant")]
public partial class AddPaymentClientWithholdingAmount_Tenant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "ClientWithholdingAmount",
            table: "Payments",
            type: "decimal(18,3)",
            precision: 18,
            scale: 3,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ClientWithholdingAmount",
            table: "Payments");
    }
}
