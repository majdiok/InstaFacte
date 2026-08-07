using System;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260808150000_AddPayrollTerminationsBonusesAndCivp_Tenant")]
    public partial class AddPayrollTerminationsBonusesAndCivp_Tenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CivpStartDate",
                table: "EmploymentContracts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CivpEndDate",
                table: "EmploymentContracts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CivpStateGrant",
                table: "EmploymentContracts",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CivpEmployerAllowance",
                table: "EmploymentContracts",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "AnetiReference",
                table: "EmploymentContracts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "PayrollVariableAllowanceLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "AnnualBonusRuleId",
                table: "PayrollVariableAllowanceLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollVariableAllowanceLines_EmployeeId_Year_Month_AnnualBonusRuleId_Source",
                table: "PayrollVariableAllowanceLines",
                columns: new[] { "EmployeeId", "Year", "Month", "AnnualBonusRuleId", "Source" });

            migrationBuilder.CreateTable(
                name: "TerminationSettlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    TerminationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SeniorityMonths = table.Column<int>(type: "int", nullable: false),
                    GrossMonthlyReference = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    LegalIndemnityAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    NoticeIndemnityAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnusedLeaveAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    OtherIndemnityAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table => { table.PrimaryKey("PK_TerminationSettlements", x => x.Id); });

            migrationBuilder.CreateIndex(
                name: "IX_TerminationSettlements_EmployeeId_Year_Month",
                table: "TerminationSettlements",
                columns: new[] { "EmployeeId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TerminationSettlements_Year_Month",
                table: "TerminationSettlements",
                columns: new[] { "Year", "Month" });

            migrationBuilder.CreateTable(
                name: "AnnualBonusRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Formula = table.Column<int>(type: "int", nullable: false),
                    PaymentMonth = table.Column<int>(type: "int", nullable: false),
                    FixedAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    RatePercent = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false),
                    MonthsOfBase = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false),
                    Taxable = table.Column<bool>(type: "bit", nullable: false),
                    SubjectToCnss = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    FiscalYear = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table => { table.PrimaryKey("PK_AnnualBonusRules", x => x.Id); });

            migrationBuilder.CreateIndex(
                name: "IX_AnnualBonusRules_Code",
                table: "AnnualBonusRules",
                column: "Code",
                unique: true);

            migrationBuilder.CreateTable(
                name: "EmployeeAnnualBonusRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnnualBonusRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    OverrideFixedAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    OverrideRatePercent = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: true),
                    OverrideMonthsOfBase = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table => { table.PrimaryKey("PK_EmployeeAnnualBonusRules", x => x.Id); });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAnnualBonusRules_EmployeeId",
                table: "EmployeeAnnualBonusRules",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAnnualBonusRules_EmployeeId_AnnualBonusRuleId",
                table: "EmployeeAnnualBonusRules",
                columns: new[] { "EmployeeId", "AnnualBonusRuleId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "EmployeeAnnualBonusRules");
            migrationBuilder.DropTable(name: "AnnualBonusRules");
            migrationBuilder.DropTable(name: "TerminationSettlements");

            migrationBuilder.DropIndex(
                name: "IX_PayrollVariableAllowanceLines_EmployeeId_Year_Month_AnnualBonusRuleId_Source",
                table: "PayrollVariableAllowanceLines");

            migrationBuilder.DropColumn(name: "AnnualBonusRuleId", table: "PayrollVariableAllowanceLines");
            migrationBuilder.DropColumn(name: "Source", table: "PayrollVariableAllowanceLines");
            migrationBuilder.DropColumn(name: "AnetiReference", table: "EmploymentContracts");
            migrationBuilder.DropColumn(name: "CivpEmployerAllowance", table: "EmploymentContracts");
            migrationBuilder.DropColumn(name: "CivpStateGrant", table: "EmploymentContracts");
            migrationBuilder.DropColumn(name: "CivpEndDate", table: "EmploymentContracts");
            migrationBuilder.DropColumn(name: "CivpStartDate", table: "EmploymentContracts");
        }
    }
}
