using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddCashExpenseCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "CashExpenses",
                type: "int",
                nullable: false,
                defaultValue: 99);

            migrationBuilder.CreateIndex(
                name: "IX_CashExpenses_Category",
                table: "CashExpenses",
                column: "Category");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CashExpenses_Category",
                table: "CashExpenses");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "CashExpenses");
        }
    }
}
