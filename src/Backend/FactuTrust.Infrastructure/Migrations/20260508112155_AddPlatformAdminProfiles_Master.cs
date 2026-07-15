using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformAdminProfiles_Master : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlatformAdminProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MfaSecretEncrypted = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MfaConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RecoveryCodesJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    FailedMfaAttempts = table.Column<int>(type: "int", nullable: false),
                    MfaLockoutUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformAdminProfiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAdminProfiles_UserId",
                table: "PlatformAdminProfiles",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformAdminProfiles");
        }
    }
}
