using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFirmLeaveManagement_Master : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FirmLeaveBalances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirmTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    OpeningBalanceDays = table.Column<decimal>(type: "decimal(9,3)", precision: 9, scale: 3, nullable: false),
                    AdjustmentDays = table.Column<decimal>(type: "decimal(9,3)", precision: 9, scale: 3, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmLeaveBalances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FirmLeaveRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirmTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaveTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartUnit = table.Column<int>(type: "int", nullable: false),
                    EndUnit = table.Column<int>(type: "int", nullable: false),
                    Days = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProcessedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmLeaveRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FirmLeaveSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirmTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    DefaultAnnualPaidDays = table.Column<decimal>(type: "decimal(9,3)", precision: 9, scale: 3, nullable: false),
                    AllowHalfDays = table.Column<bool>(type: "bit", nullable: false),
                    MinNoticeDays = table.Column<int>(type: "int", nullable: false),
                    BlockOverlap = table.Column<bool>(type: "bit", nullable: false),
                    CarryOverEnabled = table.Column<bool>(type: "bit", nullable: false),
                    MaxCarryOverDays = table.Column<decimal>(type: "decimal(9,3)", precision: 9, scale: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmLeaveSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FirmLeaveTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirmTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ColorHex = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: false),
                    DeductsBalance = table.Column<bool>(type: "bit", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "bit", nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmLeaveTypes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FirmLeaveBalances_FirmTenantId_UserId_Year",
                table: "FirmLeaveBalances",
                columns: new[] { "FirmTenantId", "UserId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FirmLeaveRequests_FirmTenantId_Status",
                table: "FirmLeaveRequests",
                columns: new[] { "FirmTenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FirmLeaveRequests_FirmTenantId_UserId_StartDate",
                table: "FirmLeaveRequests",
                columns: new[] { "FirmTenantId", "UserId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FirmLeaveSettings_FirmTenantId_Year",
                table: "FirmLeaveSettings",
                columns: new[] { "FirmTenantId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FirmLeaveTypes_FirmTenantId_Code",
                table: "FirmLeaveTypes",
                columns: new[] { "FirmTenantId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FirmLeaveBalances");

            migrationBuilder.DropTable(
                name: "FirmLeaveRequests");

            migrationBuilder.DropTable(
                name: "FirmLeaveSettings");

            migrationBuilder.DropTable(
                name: "FirmLeaveTypes");
        }
    }
}
