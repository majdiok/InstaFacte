using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddCashOperationTypeAndRevenueCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // All existing rows are debits (0).
            migrationBuilder.AddColumn<int>(
                name: "OperationType",
                table: "CashExpenses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RevenueCategory",
                table: "CashExpenses",
                type: "int",
                nullable: true);

            // Make Category nullable (was required, but credits don't have it).
            migrationBuilder.AlterColumn<int>(
                name: "Category",
                table: "CashExpenses",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_CashExpenses_OperationType",
                table: "CashExpenses",
                column: "OperationType");

            // Drop the old unique index on TenantId+FiscalYear for number sequences.
            migrationBuilder.DropIndex(
                name: "IX_CashExpenseNumberSequences_TenantId_FiscalYear",
                table: "CashExpenseNumberSequences");

            // Add Prefix column (DEP for existing rows).
            migrationBuilder.AddColumn<string>(
                name: "Prefix",
                table: "CashExpenseNumberSequences",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "DEP");

            // Recreate the unique index including Prefix.
            migrationBuilder.CreateIndex(
                name: "IX_CashExpenseNumberSequences_TenantId_FiscalYear_Prefix",
                table: "CashExpenseNumberSequences",
                columns: new[] { "TenantId", "FiscalYear", "Prefix" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CashExpenseNumberSequences_TenantId_FiscalYear_Prefix",
                table: "CashExpenseNumberSequences");

            migrationBuilder.DropColumn(
                name: "Prefix",
                table: "CashExpenseNumberSequences");

            migrationBuilder.CreateIndex(
                name: "IX_CashExpenseNumberSequences_TenantId_FiscalYear",
                table: "CashExpenseNumberSequences",
                columns: new[] { "TenantId", "FiscalYear" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_CashExpenses_OperationType",
                table: "CashExpenses");

            migrationBuilder.AlterColumn<int>(
                name: "Category",
                table: "CashExpenses",
                type: "int",
                nullable: false,
                defaultValue: 99,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "RevenueCategory",
                table: "CashExpenses");

            migrationBuilder.DropColumn(
                name: "OperationType",
                table: "CashExpenses");
        }
    }
}
