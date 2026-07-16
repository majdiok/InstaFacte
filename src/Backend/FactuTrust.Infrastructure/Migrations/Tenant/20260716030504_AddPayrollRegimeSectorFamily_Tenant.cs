using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddPayrollRegimeSectorFamily_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DisabledChildAnnualDeduction",
                table: "PayrollYearParameters",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 2000m);

            migrationBuilder.AddColumn<bool>(
                name: "IsIndustrialSector",
                table: "PayrollYearParameters",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "ParentAnnualDeductionCap",
                table: "PayrollYearParameters",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 450m);

            migrationBuilder.AddColumn<decimal>(
                name: "ParentDeductionRatePercent",
                table: "PayrollYearParameters",
                type: "decimal(8,4)",
                precision: 8,
                scale: 4,
                nullable: false,
                defaultValue: 5m);

            migrationBuilder.AddColumn<decimal>(
                name: "StudentChildAnnualDeduction",
                table: "PayrollYearParameters",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 1000m);

            migrationBuilder.AddColumn<int>(
                name: "WeeklyRegime",
                table: "EmploymentContracts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DependentParents",
                table: "Employees",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DisabledChildren",
                table: "Employees",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StudentChildren",
                table: "Employees",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisabledChildAnnualDeduction",
                table: "PayrollYearParameters");

            migrationBuilder.DropColumn(
                name: "IsIndustrialSector",
                table: "PayrollYearParameters");

            migrationBuilder.DropColumn(
                name: "ParentAnnualDeductionCap",
                table: "PayrollYearParameters");

            migrationBuilder.DropColumn(
                name: "ParentDeductionRatePercent",
                table: "PayrollYearParameters");

            migrationBuilder.DropColumn(
                name: "StudentChildAnnualDeduction",
                table: "PayrollYearParameters");

            migrationBuilder.DropColumn(
                name: "WeeklyRegime",
                table: "EmploymentContracts");

            migrationBuilder.DropColumn(
                name: "DependentParents",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "DisabledChildren",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "StudentChildren",
                table: "Employees");
        }
    }
}
