using System;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Prorata automatique embauche/départ/suspension : suspensions, colonnes bulletin,
    /// drapeau d'activation par exercice. Toutes les valeurs par défaut sont neutres.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260808120000_AddPayrollProrataAndSuspensions_Tenant")]
    public partial class AddPayrollProrataAndSuspensions_Tenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeePayrollSuspensions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsPaid = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePayrollSuspensions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrollSuspensions_EmployeeId",
                table: "EmployeePayrollSuspensions",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrollSuspensions_StartDate_EndDate",
                table: "EmployeePayrollSuspensions",
                columns: new[] { "StartDate", "EndDate" });

            migrationBuilder.AddColumn<decimal>(
                name: "ProrataWorkedDays",
                table: "Payslips",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ProrataNonWorkedDays",
                table: "Payslips",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ProrataDeductionAmount",
                table: "Payslips",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "EnableAutomaticProrata",
                table: "PayrollYearParameters",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnableAutomaticProrata",
                table: "PayrollYearParameters");

            migrationBuilder.DropColumn(
                name: "ProrataDeductionAmount",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "ProrataNonWorkedDays",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "ProrataWorkedDays",
                table: "Payslips");

            migrationBuilder.DropTable(
                name: "EmployeePayrollSuspensions");
        }
    }
}
