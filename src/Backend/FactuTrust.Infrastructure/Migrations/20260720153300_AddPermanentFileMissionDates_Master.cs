using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPermanentFileMissionDates_Master : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LabCompletedAt",
                table: "PermanentFiles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MissionAcceptedAt",
                table: "PermanentFiles",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LabCompletedAt",
                table: "PermanentFiles");

            migrationBuilder.DropColumn(
                name: "MissionAcceptedAt",
                table: "PermanentFiles");
        }
    }
}
