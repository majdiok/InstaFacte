using System;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(MasterDbContext))]
    [Migration("20260729180000_EnrichFirmTimeSheetEntry_Master")]
    public partial class EnrichFirmTimeSheetEntry_Master : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "StartTime",
                table: "FirmTimeSheetEntries",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "EndTime",
                table: "FirmTimeSheetEntries",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkLocation",
                table: "FirmTimeSheetEntries",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "FirmTimeSheetEntries",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "FirmTimeSheetEntries",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "TimerStartedAtUtc",
                table: "FirmTimeSheetEntries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE FirmTimeSheetEntries
                SET Status = CASE WHEN IsValidated = 1 THEN 2 ELSE 0 END
                """);

            migrationBuilder.CreateIndex(
                name: "IX_FirmTimeSheetEntries_FirmTenantId_UserId_TimerStartedAtUtc",
                table: "FirmTimeSheetEntries",
                columns: new[] { "FirmTenantId", "UserId", "TimerStartedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FirmTimeSheetEntries_FirmTenantId_UserId_TimerStartedAtUtc",
                table: "FirmTimeSheetEntries");

            migrationBuilder.DropColumn(name: "StartTime", table: "FirmTimeSheetEntries");
            migrationBuilder.DropColumn(name: "EndTime", table: "FirmTimeSheetEntries");
            migrationBuilder.DropColumn(name: "WorkLocation", table: "FirmTimeSheetEntries");
            migrationBuilder.DropColumn(name: "Tags", table: "FirmTimeSheetEntries");
            migrationBuilder.DropColumn(name: "Status", table: "FirmTimeSheetEntries");
            migrationBuilder.DropColumn(name: "TimerStartedAtUtc", table: "FirmTimeSheetEntries");
        }
    }
}
