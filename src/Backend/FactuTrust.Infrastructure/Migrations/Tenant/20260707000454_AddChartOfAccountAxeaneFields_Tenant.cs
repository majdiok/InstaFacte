using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddChartOfAccountAxeaneFields_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccountType",
                table: "ChartOfAccounts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AffectationAccountNumber",
                table: "ChartOfAccounts",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAuxiliary",
                table: "ChartOfAccounts",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountType",
                table: "ChartOfAccounts");

            migrationBuilder.DropColumn(
                name: "AffectationAccountNumber",
                table: "ChartOfAccounts");

            migrationBuilder.DropColumn(
                name: "IsAuxiliary",
                table: "ChartOfAccounts");
        }
    }
}
