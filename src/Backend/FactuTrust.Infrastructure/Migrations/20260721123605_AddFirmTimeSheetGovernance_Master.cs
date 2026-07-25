using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFirmTimeSheetGovernance_Master : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Hours",
                table: "FirmTimeSheetEntries",
                type: "decimal(9,3)",
                precision: 9,
                scale: 3,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(8,2)",
                oldPrecision: 8,
                oldScale: 2);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidatedAt",
                table: "FirmTimeSheetEntries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidatedByDisplayName",
                table: "FirmTimeSheetEntries",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ValidatedByUserId",
                table: "FirmTimeSheetEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PayrollEmployeeId",
                table: "FirmCollaboratorProfiles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FirmActivityCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirmTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    IsBillableByDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmActivityCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FirmCollaboratorYearCosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirmTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollaboratorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    GrossAnnualSalary = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    EmployerContributions = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    PayrollExtras = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HourlyRateOverride = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    OverrideJustification = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmCollaboratorYearCosts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FirmTimeSheetPeriodLocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirmTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    LockedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LockedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LockedByDisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LockReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    UnlockedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UnlockedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UnlockedByDisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UnlockReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmTimeSheetPeriodLocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FirmTimeSheetYearSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirmTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    WeeklyRegime = table.Column<int>(type: "int", nullable: false),
                    MaxDailyHours = table.Column<decimal>(type: "decimal(9,3)", precision: 9, scale: 3, nullable: false),
                    MaxWeeklyHours = table.Column<decimal>(type: "decimal(9,3)", precision: 9, scale: 3, nullable: false),
                    AllowFutureEntryDays = table.Column<int>(type: "int", nullable: false),
                    MaxBackdatingDays = table.Column<int>(type: "int", nullable: false),
                    EnforceHardLimits = table.Column<bool>(type: "bit", nullable: false),
                    PaidLeaveDaysPerYear = table.Column<decimal>(type: "decimal(9,3)", precision: 9, scale: 3, nullable: false),
                    PublicHolidayDaysPerYear = table.Column<decimal>(type: "decimal(9,3)", precision: 9, scale: 3, nullable: false),
                    ProductivityRatePercent = table.Column<decimal>(type: "decimal(9,3)", precision: 9, scale: 3, nullable: false),
                    CnssEmployerRate = table.Column<decimal>(type: "decimal(9,3)", precision: 9, scale: 3, nullable: false),
                    TfpRate = table.Column<decimal>(type: "decimal(9,3)", precision: 9, scale: 3, nullable: false),
                    FoprolosRate = table.Column<decimal>(type: "decimal(9,3)", precision: 9, scale: 3, nullable: false),
                    WorkAccidentRate = table.Column<decimal>(type: "decimal(9,3)", precision: 9, scale: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmTimeSheetYearSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FirmTimeSheetEntries_FirmTenantId_UserId_WorkDate",
                table: "FirmTimeSheetEntries",
                columns: new[] { "FirmTenantId", "UserId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FirmCollaboratorProfiles_PayrollEmployeeId",
                table: "FirmCollaboratorProfiles",
                column: "PayrollEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_FirmActivityCodes_FirmTenantId_Code",
                table: "FirmActivityCodes",
                columns: new[] { "FirmTenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FirmCollaboratorYearCosts_FirmTenantId_CollaboratorUserId_Year",
                table: "FirmCollaboratorYearCosts",
                columns: new[] { "FirmTenantId", "CollaboratorUserId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FirmTimeSheetPeriodLocks_FirmTenantId_Year_Month",
                table: "FirmTimeSheetPeriodLocks",
                columns: new[] { "FirmTenantId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FirmTimeSheetYearSettings_FirmTenantId_Year",
                table: "FirmTimeSheetYearSettings",
                columns: new[] { "FirmTenantId", "Year" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FirmActivityCodes");

            migrationBuilder.DropTable(
                name: "FirmCollaboratorYearCosts");

            migrationBuilder.DropTable(
                name: "FirmTimeSheetPeriodLocks");

            migrationBuilder.DropTable(
                name: "FirmTimeSheetYearSettings");

            migrationBuilder.DropIndex(
                name: "IX_FirmTimeSheetEntries_FirmTenantId_UserId_WorkDate",
                table: "FirmTimeSheetEntries");

            migrationBuilder.DropIndex(
                name: "IX_FirmCollaboratorProfiles_PayrollEmployeeId",
                table: "FirmCollaboratorProfiles");

            migrationBuilder.DropColumn(
                name: "ValidatedAt",
                table: "FirmTimeSheetEntries");

            migrationBuilder.DropColumn(
                name: "ValidatedByDisplayName",
                table: "FirmTimeSheetEntries");

            migrationBuilder.DropColumn(
                name: "ValidatedByUserId",
                table: "FirmTimeSheetEntries");

            migrationBuilder.DropColumn(
                name: "PayrollEmployeeId",
                table: "FirmCollaboratorProfiles");

            migrationBuilder.AlterColumn<decimal>(
                name: "Hours",
                table: "FirmTimeSheetEntries",
                type: "decimal(8,2)",
                precision: 8,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,3)",
                oldPrecision: 9,
                oldScale: 3);
        }
    }
}
