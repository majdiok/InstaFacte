using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddPayrollComplianceOptions_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CnssEmployeeRateRsa",
                table: "PayrollYearParameters",
                type: "decimal(8,4)",
                precision: 8,
                scale: 4,
                nullable: false,
                defaultValue: 9.18m);

            migrationBuilder.AddColumn<decimal>(
                name: "CnssEmployerRateRsa",
                table: "PayrollYearParameters",
                type: "decimal(8,4)",
                precision: 8,
                scale: 4,
                nullable: false,
                defaultValue: 16.57m);

            migrationBuilder.AddColumn<bool>(
                name: "EnableAllowanceQuadrantMatrix",
                table: "PayrollYearParameters",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableExtendedOvertimeRates",
                table: "PayrollYearParameters",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnforceSmigOnContracts",
                table: "PayrollYearParameters",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                """
                UPDATE PayrollYearParameters
                SET CnssEmployeeRateRsa = CnssEmployeeRate,
                    CnssEmployerRateRsa = CnssEmployerRate
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CnssEmployeeRateRsa",
                table: "PayrollYearParameters");

            migrationBuilder.DropColumn(
                name: "CnssEmployerRateRsa",
                table: "PayrollYearParameters");

            migrationBuilder.DropColumn(
                name: "EnableAllowanceQuadrantMatrix",
                table: "PayrollYearParameters");

            migrationBuilder.DropColumn(
                name: "EnableExtendedOvertimeRates",
                table: "PayrollYearParameters");

            migrationBuilder.DropColumn(
                name: "EnforceSmigOnContracts",
                table: "PayrollYearParameters");
        }
    }
}
