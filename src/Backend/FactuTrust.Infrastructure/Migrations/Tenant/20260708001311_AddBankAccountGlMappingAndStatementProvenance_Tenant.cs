using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddBankAccountGlMappingAndStatementProvenance_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BankAccountId",
                table: "BankStatements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChartOfAccountNumber",
                table: "BankStatements",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ImportMethod",
                table: "BankStatements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SourceFileHash",
                table: "BankStatements",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFileName",
                table: "BankStatements",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValueDate",
                table: "BankStatementLines",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChartOfAccountNumber",
                table: "BankAccounts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "BankAccounts",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "TND");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatements_BankAccountId",
                table: "BankStatements",
                column: "BankAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_BankStatements_BankAccounts_BankAccountId",
                table: "BankStatements",
                column: "BankAccountId",
                principalTable: "BankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BankStatements_BankAccounts_BankAccountId",
                table: "BankStatements");

            migrationBuilder.DropIndex(
                name: "IX_BankStatements_BankAccountId",
                table: "BankStatements");

            migrationBuilder.DropColumn(
                name: "BankAccountId",
                table: "BankStatements");

            migrationBuilder.DropColumn(
                name: "ChartOfAccountNumber",
                table: "BankStatements");

            migrationBuilder.DropColumn(
                name: "ImportMethod",
                table: "BankStatements");

            migrationBuilder.DropColumn(
                name: "SourceFileHash",
                table: "BankStatements");

            migrationBuilder.DropColumn(
                name: "SourceFileName",
                table: "BankStatements");

            migrationBuilder.DropColumn(
                name: "ValueDate",
                table: "BankStatementLines");

            migrationBuilder.DropColumn(
                name: "ChartOfAccountNumber",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "BankAccounts");
        }
    }
}
