using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPermanentFileBilling_Master : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AnnualFeeAmount",
                table: "PermanentFiles",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BillingFrequency",
                table: "PermanentFiles",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnnualFeeAmount",
                table: "PermanentFiles");

            migrationBuilder.DropColumn(
                name: "BillingFrequency",
                table: "PermanentFiles");
        }
    }
}
