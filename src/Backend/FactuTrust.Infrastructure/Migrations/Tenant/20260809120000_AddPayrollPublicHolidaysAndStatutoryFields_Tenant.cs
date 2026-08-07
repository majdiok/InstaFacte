using System;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Jours fériés tunisiens (tenant) et champs statutaires complémentaires sur PayrollYearParameters.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260809120000_AddPayrollPublicHolidaysAndStatutoryFields_Tenant")]
    public partial class AddPayrollPublicHolidaysAndStatutoryFields_Tenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayrollPublicHolidays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    IsPaid = table.Column<bool>(type: "bit", nullable: false),
                    IsEstimated = table.Column<bool>(type: "bit", nullable: false),
                    DecreeReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollPublicHolidays", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPublicHolidays_Date",
                table: "PayrollPublicHolidays",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPublicHolidays_Year_Date",
                table: "PayrollPublicHolidays",
                columns: new[] { "Year", "Date" },
                unique: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AccidentWorkMonthlyCeiling",
                table: "PayrollYearParameters",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CnssDailyCeiling",
                table: "PayrollYearParameters",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CnssMonthlyCeiling",
                table: "PayrollYearParameters",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CssMonthlyCeiling",
                table: "PayrollYearParameters",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaternityLeaveDurationDays",
                table: "PayrollYearParameters",
                type: "int",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<decimal>(
                name: "MaternityEmployerTopUpDefault",
                table: "PayrollYearParameters",
                type: "decimal(8,4)",
                precision: 8,
                scale: 4,
                nullable: false,
                defaultValue: 100m);

            migrationBuilder.AddColumn<int>(
                name: "PaternityLeaveDurationDays",
                table: "PayrollYearParameters",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<decimal>(
                name: "SickLeaveIjRatePercent",
                table: "PayrollYearParameters",
                type: "decimal(8,4)",
                precision: 8,
                scale: 4,
                nullable: false,
                defaultValue: 66.67m);

            migrationBuilder.AddColumn<int>(
                name: "SickLeaveWaitingDays",
                table: "PayrollYearParameters",
                type: "int",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<string>(
                name: "MedicalCertificateNumber",
                table: "LeaveRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MedicalCertificateDate",
                table: "LeaveRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SubrogationEnabled",
                table: "LeaveRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "EmployerTopUpPercent",
                table: "LeaveRequests",
                type: "decimal(8,4)",
                precision: 8,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmployerTopUpDays",
                table: "LeaveRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpectedBirthDate",
                table: "LeaveRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActualBirthDate",
                table: "LeaveRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChildBirthCertificateNumber",
                table: "LeaveRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CnssIjClaims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaveRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CnssIjClaims", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CnssIjClaims_EmployeeId",
                table: "CnssIjClaims",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_CnssIjClaims_Year_Month",
                table: "CnssIjClaims",
                columns: new[] { "Year", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_CnssIjClaims_LeaveRequestId_Year_Month",
                table: "CnssIjClaims",
                columns: new[] { "LeaveRequestId", "Year", "Month" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollPublicHolidays");

            migrationBuilder.DropColumn(
                name: "AccidentWorkMonthlyCeiling",
                table: "PayrollYearParameters");

            migrationBuilder.DropColumn(
                name: "CnssDailyCeiling",
                table: "PayrollYearParameters");

            migrationBuilder.DropColumn(
                name: "CnssMonthlyCeiling",
                table: "PayrollYearParameters");

            migrationBuilder.DropColumn(
                name: "CssMonthlyCeiling",
                table: "PayrollYearParameters");

            migrationBuilder.DropColumn(
                name: "MaternityLeaveDurationDays",
                table: "PayrollYearParameters");

            migrationBuilder.DropColumn(
                name: "MaternityEmployerTopUpDefault",
                table: "PayrollYearParameters");

            migrationBuilder.DropColumn(
                name: "PaternityLeaveDurationDays",
                table: "PayrollYearParameters");

            migrationBuilder.DropColumn(
                name: "SickLeaveIjRatePercent",
                table: "PayrollYearParameters");

            migrationBuilder.DropColumn(
                name: "SickLeaveWaitingDays",
                table: "PayrollYearParameters");

            migrationBuilder.DropTable(
                name: "CnssIjClaims");

            migrationBuilder.DropColumn(
                name: "MedicalCertificateNumber",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "MedicalCertificateDate",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "SubrogationEnabled",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "EmployerTopUpPercent",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "EmployerTopUpDays",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "ExpectedBirthDate",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "ActualBirthDate",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "ChildBirthCertificateNumber",
                table: "LeaveRequests");
        }
    }
}
