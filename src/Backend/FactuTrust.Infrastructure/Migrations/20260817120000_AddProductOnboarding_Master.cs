using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(MasterDbContext))]
    [Migration("20260817120000_AddProductOnboarding_Master")]
    public partial class AddProductOnboarding_Master : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "ProductOnboardingStatus",
                table: "Users",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)2);

            migrationBuilder.AddColumn<int>(
                name: "ProductOnboardingVersion",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "ProductOnboardingChecklistJson",
                table: "Users",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProductOnboardingUpdatedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ProductOnboardingStatus", table: "Users");
            migrationBuilder.DropColumn(name: "ProductOnboardingVersion", table: "Users");
            migrationBuilder.DropColumn(name: "ProductOnboardingChecklistJson", table: "Users");
            migrationBuilder.DropColumn(name: "ProductOnboardingUpdatedAt", table: "Users");
        }
    }
}
