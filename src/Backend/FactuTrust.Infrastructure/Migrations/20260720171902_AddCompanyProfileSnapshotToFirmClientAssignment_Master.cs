using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyProfileSnapshotToFirmClientAssignment_Master : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompanyProfileCapturedAt",
                table: "FirmClientAssignments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyProfileSnapshotJson",
                table: "FirmClientAssignments",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompanyProfileCapturedAt",
                table: "FirmClientAssignments");

            migrationBuilder.DropColumn(
                name: "CompanyProfileSnapshotJson",
                table: "FirmClientAssignments");
        }
    }
}
