using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddBankDeposits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankDepositNumberSequences",
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
                    table.PrimaryKey("PK_BankDepositNumberSequences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BankDeposits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepositNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DepositNumberYear = table.Column<int>(type: "int", nullable: false),
                    DepositNumberSequence = table.Column<int>(type: "int", nullable: false),
                    DepositType = table.Column<int>(type: "int", nullable: false),
                    DepositDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AmountCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    DepositSlipReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CashOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_BankDeposits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankDeposits_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankDeposits_CashExpenses_CashOperationId",
                        column: x => x.CashOperationId,
                        principalTable: "CashExpenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankDepositNumberSequences_TenantId_FiscalYear",
                table: "BankDepositNumberSequences",
                columns: new[] { "TenantId", "FiscalYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankDeposits_BankAccountId",
                table: "BankDeposits",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BankDeposits_CashOperationId",
                table: "BankDeposits",
                column: "CashOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_BankDeposits_DepositDate",
                table: "BankDeposits",
                column: "DepositDate");

            migrationBuilder.CreateIndex(
                name: "IX_BankDeposits_DepositDate_Status",
                table: "BankDeposits",
                columns: new[] { "DepositDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BankDeposits_DepositNumber",
                table: "BankDeposits",
                column: "DepositNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankDepositNumberSequences");

            migrationBuilder.DropTable(
                name: "BankDeposits");
        }
    }
}
