using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Exonération IRPP SMIG (art. 21) : mode configurable par exercice, traçabilité sur bulletin et cycle.
    /// Toutes les colonnes ajoutées portent un défaut neutre (0 / None) : rétro-compatibilité totale.
    /// </summary>
    [DbContext(typeof(FactuTrust.Infrastructure.Persistence.TenantDbContext))]
    [Migration("20260806300000_AddPayrollSmigIrppExemption_Tenant")]
    public partial class AddPayrollSmigIrppExemption_Tenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SmigIrppExemptionMode",
                table: "PayrollYearParameters",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "SmigIrppExemptionRateOverride",
                table: "PayrollYearParameters",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IrppBeforeSmigExemption",
                table: "Payslips",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "IrppSmigExemption",
                table: "Payslips",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalIrppSmigExemption",
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
                name: "SmigIrppExemptionMode",
                table: "PayrollYearParameters");

            migrationBuilder.DropColumn(
                name: "SmigIrppExemptionRateOverride",
                table: "PayrollYearParameters");

            migrationBuilder.DropColumn(
                name: "IrppBeforeSmigExemption",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "IrppSmigExemption",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "TotalIrppSmigExemption",
                table: "PayrollRuns");
        }
    }
}
