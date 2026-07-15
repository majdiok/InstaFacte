using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

[DbContext(typeof(TenantDbContext))]
[Migration("20260403220000_AddWithholdingFiscalYearParameters_Tenant")]
public partial class AddWithholdingFiscalYearParameters_Tenant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "WithholdingFiscalYearParameters",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FiscalYear = table.Column<int>(type: "int", nullable: false),
                Rs7TtcThresholdTnd = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WithholdingFiscalYearParameters", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_WithholdingFiscalYearParameters_FiscalYear",
            table: "WithholdingFiscalYearParameters",
            column: "FiscalYear",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "WithholdingFiscalYearParameters");
    }
}
