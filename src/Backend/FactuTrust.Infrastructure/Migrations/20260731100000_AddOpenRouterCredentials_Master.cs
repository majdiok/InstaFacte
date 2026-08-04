using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(MasterDbContext))]
    [Migration("20260731100000_AddOpenRouterCredentials_Master")]
    public partial class AddOpenRouterCredentials_Master : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OpenRouterIsEnabled",
                table: "PlatformAiSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OpenRouterDisplayName",
                table: "PlatformAiSettings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OpenRouterBaseUrl",
                table: "PlatformAiSettings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OpenRouterEncryptedApiKey",
                table: "PlatformAiSettings",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OpenRouterApiKeyLast4",
                table: "PlatformAiSettings",
                type: "nvarchar(4)",
                maxLength: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OpenRouterIsEnabled",
                table: "PlatformAiSettings");

            migrationBuilder.DropColumn(
                name: "OpenRouterDisplayName",
                table: "PlatformAiSettings");

            migrationBuilder.DropColumn(
                name: "OpenRouterBaseUrl",
                table: "PlatformAiSettings");

            migrationBuilder.DropColumn(
                name: "OpenRouterEncryptedApiKey",
                table: "PlatformAiSettings");

            migrationBuilder.DropColumn(
                name: "OpenRouterApiKeyLast4",
                table: "PlatformAiSettings");
        }
    }
}
