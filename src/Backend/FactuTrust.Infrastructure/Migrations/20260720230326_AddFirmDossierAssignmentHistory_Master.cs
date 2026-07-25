using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFirmDossierAssignmentHistory_Master : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FirmDossierAssignmentHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirmTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirmClientAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountantUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AccountantDisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AssignedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmDossierAssignmentHistories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FirmDossierAssignmentHistories_AccountantUserId",
                table: "FirmDossierAssignmentHistories",
                column: "AccountantUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FirmDossierAssignmentHistories_FirmTenantId_FirmClientAssignmentId_EndedAt",
                table: "FirmDossierAssignmentHistories",
                columns: new[] { "FirmTenantId", "FirmClientAssignmentId", "EndedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FirmDossierAssignmentHistories");
        }
    }
}
