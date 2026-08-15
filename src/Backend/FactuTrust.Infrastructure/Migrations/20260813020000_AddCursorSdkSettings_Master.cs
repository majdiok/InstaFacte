using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(MasterDbContext))]
    [Migration("20260813020000_AddCursorSdkSettings_Master")]
    public partial class AddCursorSdkSettings_Master : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CursorIsEnabled",
                table: "PlatformAiSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CursorDisplayName",
                table: "PlatformAiSettings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CursorEncryptedApiKey",
                table: "PlatformAiSettings",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CursorApiKeyLast4",
                table: "PlatformAiSettings",
                type: "nvarchar(4)",
                maxLength: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CursorIsEnabled", table: "PlatformAiSettings");
            migrationBuilder.DropColumn(name: "CursorDisplayName", table: "PlatformAiSettings");
            migrationBuilder.DropColumn(name: "CursorEncryptedApiKey", table: "PlatformAiSettings");
            migrationBuilder.DropColumn(name: "CursorApiKeyLast4", table: "PlatformAiSettings");
        }
    }
}
