using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(MasterDbContext))]
    [Migration("20260818180000_AddModalEndpointSettings_Master")]
    public partial class AddModalEndpointSettings_Master : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ModalIsEnabled",
                table: "PlatformAiSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ModalDisplayName",
                table: "PlatformAiSettings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModalBaseUrl",
                table: "PlatformAiSettings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModalEncryptedApiKey",
                table: "PlatformAiSettings",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModalApiKeyLast4",
                table: "PlatformAiSettings",
                type: "nvarchar(4)",
                maxLength: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ModalIsEnabled", table: "PlatformAiSettings");
            migrationBuilder.DropColumn(name: "ModalDisplayName", table: "PlatformAiSettings");
            migrationBuilder.DropColumn(name: "ModalBaseUrl", table: "PlatformAiSettings");
            migrationBuilder.DropColumn(name: "ModalEncryptedApiKey", table: "PlatformAiSettings");
            migrationBuilder.DropColumn(name: "ModalApiKeyLast4", table: "PlatformAiSettings");
        }
    }
}
