using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// CSS patronale : taux paramétrable par exercice, montant figé sur le bulletin et total du cycle.
    /// Toutes les colonnes portent un défaut neutre (0) : les cycles existants conservent leur comportement.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260807120000_AddCssEmployer_Tenant")]
    public partial class AddCssEmployer_Tenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CssEmployerRate",
                table: "PayrollYearParameters",
                type: "decimal(8,4)",
                precision: 8,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CssEmployer",
                table: "Payslips",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCssEmployer",
                table: "PayrollRuns",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CssEmployerRate",
                table: "PayrollYearParameters");

            migrationBuilder.DropColumn(
                name: "CssEmployer",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "TotalCssEmployer",
                table: "PayrollRuns");
        }
    }
}
