using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddCashDeskExpenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CashExpenseNumberSequences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalYear = table.Column<int>(type: "int", nullable: false),
                    CurrentSequence = table.Column<int>(type: "int", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashExpenseNumberSequences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CashExpenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpenseNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExpenseNumberPrefix = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ExpenseNumberYear = table.Column<int>(type: "int", nullable: false),
                    ExpenseNumberSequence = table.Column<int>(type: "int", nullable: false),
                    ExpenseDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AmountCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashExpenses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashExpenseNumberSequences_TenantId_FiscalYear",
                table: "CashExpenseNumberSequences",
                columns: new[] { "TenantId", "FiscalYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashExpenses_ExpenseDate",
                table: "CashExpenses",
                column: "ExpenseDate");

            migrationBuilder.CreateIndex(
                name: "IX_CashExpenses_ExpenseNumber",
                table: "CashExpenses",
                column: "ExpenseNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashExpenses_Method",
                table: "CashExpenses",
                column: "Method");

            migrationBuilder.CreateIndex(
                name: "IX_CashExpenses_Reference",
                table: "CashExpenses",
                column: "Reference");

            migrationBuilder.CreateIndex(
                name: "IX_CashExpenses_Status",
                table: "CashExpenses",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashExpenseNumberSequences");

            migrationBuilder.DropTable(
                name: "CashExpenses");
        }
    }
}
