using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations
{
    [DbContext(typeof(MasterDbContext))]
    [Migration("20260810140000_AddFirmCollaboratorPayrollLinkAudit_Master")]
    public partial class AddFirmCollaboratorPayrollLinkAudit_Master : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PayrollLinkSource",
                table: "FirmCollaboratorProfiles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PayrollLinkedAt",
                table: "FirmCollaboratorProfiles",
                type: "datetime2",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PayrollLinkSource",
                table: "FirmCollaboratorProfiles");

            migrationBuilder.DropColumn(
                name: "PayrollLinkedAt",
                table: "FirmCollaboratorProfiles");
        }
    }
}
